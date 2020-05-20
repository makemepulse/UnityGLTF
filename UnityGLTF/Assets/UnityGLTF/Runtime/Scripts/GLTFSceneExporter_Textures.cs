
using System;
using System.IO;
using GLTF.Schema;
using Newtonsoft.Json.Linq;
using UnityEngine;
using WrapMode = GLTF.Schema.WrapMode;

namespace UnityGLTF
{

  public partial class GLTFSceneExporter
  {


    private enum IMAGETYPE
    {
      RGB,
      RGBA,
      R,
      G,
      B,
      A,
      G_INVERT
    }

    private enum TextureMapType
    {
      Main,
      Bump,
      SpecGloss,
      Emission,
      MetallicGloss,
      Light,
      Occlusion
    }

    private struct ImageInfo
    {
      public Texture2D texture;
      public TextureMapType textureMapType;
    }


    private void ExportImages(string outputPath)
    {

      for (int t = 0; t < _imageInfos.Count; ++t)
      {

        var image = _imageInfos[t].texture;
        int height = image.height;
        int width = image.width;
        Texture2D currentTex = null;

        switch (_imageInfos[t].textureMapType)
        {
          case TextureMapType.MetallicGloss:
            currentTex = ExportMetallicGlossTexture(image, outputPath);
            break;
          case TextureMapType.Bump:
            currentTex = ExportNormalTexture(image, outputPath);
            break;
          default:
            currentTex = ExportTexture(image, outputPath);
            break;
        }

        currentTex.name = image.name;
        ExportCompressed(image, currentTex, outputPath);

        if (Application.isEditor)
        {
          GameObject.DestroyImmediate(currentTex);
        }
        else
        {
          GameObject.Destroy(currentTex);
        }

      }
    }

    /// <summary>
    /// This converts Unity's metallic-gloss texture representation into GLTF's metallic-roughness specifications.
    /// Unity's metallic-gloss A channel (glossiness) is inverted and goes into GLTF's metallic-roughness G channel (roughness).
    /// Unity's metallic-gloss R channel (metallic) goes into GLTF's metallic-roughess B channel.
    /// </summary>
    /// <param name="texture">Unity's metallic-gloss texture to be exported</param>
    /// <param name="outputPath">The location to export the texture</param>
    private Texture2D ExportMetallicGlossTexture(Texture2D texture, string outputPath)
    {
      var destRenderTexture = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

      Graphics.Blit(texture, destRenderTexture, _metalGlossChannelSwapMaterial);

      var exportTexture = new Texture2D(texture.width, texture.height);
      exportTexture.ReadPixels(new Rect(0, 0, destRenderTexture.width, destRenderTexture.height), 0, 0);
      exportTexture.Apply();

      var finalFilenamePath = ConstructImageFilenamePath(texture, outputPath);
      File.WriteAllBytes(finalFilenamePath, exportTexture.EncodeToPNG());

      RenderTexture.ReleaseTemporary(destRenderTexture);
      // if (Application.isEditor)
      // {
      // 	GameObject.DestroyImmediate(exportTexture);
      // }
      // else
      // {
      // 	GameObject.Destroy(exportTexture);
      // }
      return exportTexture;
    }

    /// <summary>
    /// This export's the normal texture. If a texture is marked as a normal map, the values are stored in the A and G channel.
    /// To output the correct normal texture, the A channel is put into the R channel.
    /// </summary>
    /// <param name="texture">Unity's normal texture to be exported</param>
    /// <param name="outputPath">The location to export the texture</param>
    private Texture2D ExportNormalTexture(Texture2D texture, string outputPath)
    {
      var destRenderTexture = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

      Graphics.Blit(texture, destRenderTexture, _normalChannelMaterial);

      var exportTexture = new Texture2D(texture.width, texture.height);
      exportTexture.ReadPixels(new Rect(0, 0, destRenderTexture.width, destRenderTexture.height), 0, 0);
      exportTexture.Apply();

      var finalFilenamePath = ConstructImageFilenamePath(texture, outputPath);
      File.WriteAllBytes(finalFilenamePath, exportTexture.EncodeToPNG());


      RenderTexture.ReleaseTemporary(destRenderTexture);
      // if (Application.isEditor)
      // {
      // 	GameObject.DestroyImmediate(exportTexture);
      // }
      // else
      // {
      // 	GameObject.Destroy(exportTexture);
      // }
      return exportTexture;
    }

    public Texture2D ExportTexture(Texture2D texture, string outputPath)
    {
      var destRenderTexture = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

      Graphics.Blit(texture, destRenderTexture);

      var exportTexture = new Texture2D(texture.width, texture.height);
      exportTexture.ReadPixels(new Rect(0, 0, destRenderTexture.width, destRenderTexture.height), 0, 0);
      exportTexture.Apply();

      var finalFilenamePath = ConstructImageFilenamePath(texture, outputPath);
      File.WriteAllBytes(finalFilenamePath, exportTexture.EncodeToPNG());

      RenderTexture.ReleaseTemporary(destRenderTexture);
      // if (Application.isEditor)
      // {
      // 	GameObject.DestroyImmediate(exportTexture);
      // }
      // else
      // {
      // 	GameObject.Destroy(exportTexture);
      // }
      return exportTexture;
    }

    private string ConstructImageFilenamePath(Texture2D texture, string outputPath)
    {
      var imagePath = _exportOptions.TexturePathRetriever(texture);
      if (string.IsNullOrEmpty(imagePath))
      {
        imagePath = Path.Combine(outputPath, texture.name);
      }

      var filenamePath = Path.Combine(outputPath, imagePath);
      if (!ExportFullPath)
      {
        filenamePath = outputPath + "/" + texture.name;
      }
      var file = new FileInfo(filenamePath);
      file.Directory.Create();
      return Path.ChangeExtension(filenamePath, ".png");
    }


    private TextureInfo ExportTextureInfo(Texture texture, TextureMapType textureMapType)
    {
      var info = new TextureInfo();

      info.Index = ExportTexture(texture, textureMapType);

      return info;
    }

    private TextureId ExportTexture(Texture textureObj, TextureMapType textureMapType)
    {
      TextureId id = GetTextureId(_root, textureObj);
      if (id != null)
      {
        return id;
      }

      var texture = new GLTFTexture();

      //If texture name not set give it a unique name using count
      if (textureObj.name == "")
      {
        textureObj.name = (_root.Textures.Count + 1).ToString();
      }

      if (ExportNames)
      {
        texture.Name = textureObj.name;
      }

      if (_shouldUseInternalBufferForImages)
      {
        texture.Source = ExportImageInternalBuffer(textureObj, textureMapType);
      }
      else
      {
        texture.Source = ExportImage(textureObj, textureMapType);
      }

			// EXTRA CUSTOM
			// MMP_compressed_texture
			// ================
      GLTFTexturesRegistry reg = GLTFExportSettings.Defaults.info.TexturesRegistry;
      GLTFExportTextureSettings setting = reg.GetSettings(textureObj as Texture2D);
      if (setting.Compress)
      {

        JObject extra = texture.Source.Value.Extras as JObject;
        if (extra == null)
        {
          extra = new JObject();
          texture.Source.Value.Extras = extra;
        }

        extra.Add(
          "MMP_compressed_texture",
          true
        );

      }




      texture.Sampler = ExportSampler(textureObj);

      _textures.Add(textureObj);

      id = new TextureId
      {
        Id = _root.Textures.Count,
        Root = _root
      };

      _root.Textures.Add(texture);

      return id;
    }

    private ImageId ExportImage(Texture texture, TextureMapType texturMapType)
    {
      ImageId id = GetImageId(_root, texture);
      if (id != null)
      {
        return id;
      }

      var image = new GLTFImage();

      if (ExportNames)
      {
        image.Name = texture.name;
      }

      _imageInfos.Add(new ImageInfo
      {
        texture = texture as Texture2D,
        textureMapType = texturMapType
      });

      var imagePath = _exportOptions.TexturePathRetriever(texture);
      if (string.IsNullOrEmpty(imagePath))
      {
        imagePath = texture.name;
      }

      var filenamePath = Path.ChangeExtension(imagePath, ".png");
      if (!ExportFullPath)
      {
        filenamePath = Path.ChangeExtension(texture.name, ".png");
      }
      image.Uri = Uri.EscapeUriString(filenamePath);

      id = new ImageId
      {
        Id = _root.Images.Count,
        Root = _root
      };

      _root.Images.Add(image);

      return id;
    }

    private ImageId ExportImageInternalBuffer(UnityEngine.Texture texture, TextureMapType texturMapType)
    {

      if (texture == null)
      {
        throw new Exception("texture can not be NULL.");
      }

      var image = new GLTFImage();
      image.MimeType = "image/png";

      var byteOffset = _bufferWriter.BaseStream.Position;

      {//
        var destRenderTexture = RenderTexture.GetTemporary(texture.width, texture.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        GL.sRGBWrite = true;
        switch (texturMapType)
        {
          case TextureMapType.MetallicGloss:
            Graphics.Blit(texture, destRenderTexture, _metalGlossChannelSwapMaterial);
            break;
          case TextureMapType.Bump:
            Graphics.Blit(texture, destRenderTexture, _normalChannelMaterial);
            break;
          default:
            Graphics.Blit(texture, destRenderTexture);
            break;
        }

        var exportTexture = new Texture2D(texture.width, texture.height, TextureFormat.ARGB32, false);
        exportTexture.ReadPixels(new Rect(0, 0, destRenderTexture.width, destRenderTexture.height), 0, 0);
        exportTexture.Apply();

        var pngImageData = exportTexture.EncodeToPNG();
        _bufferWriter.Write(pngImageData);

        RenderTexture.ReleaseTemporary(destRenderTexture);

        GL.sRGBWrite = false;
        if (Application.isEditor)
        {
          UnityEngine.Object.DestroyImmediate(exportTexture);
        }
        else
        {
          UnityEngine.Object.Destroy(exportTexture);
        }
      }

      var byteLength = _bufferWriter.BaseStream.Position - byteOffset;

      byteLength = AppendToBufferMultiplyOf4(byteOffset, byteLength);

      image.BufferView = ExportBufferView((uint)byteOffset, (uint)byteLength);


      var id = new ImageId
      {
        Id = _root.Images.Count,
        Root = _root
      };
      _root.Images.Add(image);

      return id;
    }
    private SamplerId ExportSampler(Texture texture)
    {
      var samplerId = GetSamplerId(_root, texture);
      if (samplerId != null)
        return samplerId;

      var sampler = new Sampler();

      switch (texture.wrapMode)
      {
        case TextureWrapMode.Clamp:
          sampler.WrapS = WrapMode.ClampToEdge;
          sampler.WrapT = WrapMode.ClampToEdge;
          break;
        case TextureWrapMode.Repeat:
          sampler.WrapS = WrapMode.Repeat;
          sampler.WrapT = WrapMode.Repeat;
          break;
        case TextureWrapMode.Mirror:
          sampler.WrapS = WrapMode.MirroredRepeat;
          sampler.WrapT = WrapMode.MirroredRepeat;
          break;
        default:
          Debug.LogWarning("Unsupported Texture.wrapMode: " + texture.wrapMode);
          sampler.WrapS = WrapMode.Repeat;
          sampler.WrapT = WrapMode.Repeat;
          break;
      }

      switch (texture.filterMode)
      {
        case FilterMode.Point:
          sampler.MinFilter = MinFilterMode.NearestMipmapNearest;
          sampler.MagFilter = MagFilterMode.Nearest;
          break;
        case FilterMode.Bilinear:
          sampler.MinFilter = MinFilterMode.LinearMipmapNearest;
          sampler.MagFilter = MagFilterMode.Linear;
          break;
        case FilterMode.Trilinear:
          sampler.MinFilter = MinFilterMode.LinearMipmapLinear;
          sampler.MagFilter = MagFilterMode.Linear;
          break;
        default:
          Debug.LogWarning("Unsupported Texture.filterMode: " + texture.filterMode);
          sampler.MinFilter = MinFilterMode.LinearMipmapLinear;
          sampler.MagFilter = MagFilterMode.Linear;
          break;
      }

      samplerId = new SamplerId
      {
        Id = _root.Samplers.Count,
        Root = _root
      };

      _root.Samplers.Add(sampler);

      return samplerId;
    }


  }
}