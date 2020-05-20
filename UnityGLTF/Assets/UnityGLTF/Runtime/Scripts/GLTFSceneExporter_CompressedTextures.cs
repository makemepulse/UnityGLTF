
using System;
using System.Collections.Generic;
using System.IO;
using GLTF.Schema;
using UnityEditor;
using UnityEngine;
using WrapMode = GLTF.Schema.WrapMode;

namespace UnityGLTF
{

  public partial class GLTFSceneExporter
  {


    public static TextureFormat Compressed_GetSourceFormat(TextureFormat targetFormat)
    {

      switch (targetFormat)
      {
        case TextureFormat.RGB24:
        case TextureFormat.DXT1:
        case TextureFormat.PVRTC_RGB2:
        case TextureFormat.PVRTC_RGB4:
        case TextureFormat.ETC_RGB4:
          return TextureFormat.RGB24;
        default:
          return TextureFormat.RGBA32;
      }

    }

    // BOOUUH !! Todo check if there is a better way to handle this
    public static void FlipTextureVertically(Texture2D original)
    {
      var originalPixels = original.GetPixels();

      Color[] newPixels = new Color[originalPixels.Length];

      int width = original.width;
      int rows = original.height;

      for (int x = 0; x < width; x++)
      {
        for (int y = 0; y < rows; y++)
        {
          newPixels[x + y * width] = originalPixels[x + (rows - y - 1) * width];
        }
      }

      original.SetPixels(newPixels);
      original.Apply();
    }


    public void ExportCompressed(Texture2D source, Texture2D texture, string outputPath)
    {

      GLTFTexturesRegistry reg = GLTFExportSettings.Defaults.info.TexturesRegistry;

      GLTFExportTextureSettings setting = reg.GetSettings(source);

      if (!setting.Compress)
        return;


      // ==========
      // FORMATS
      // TODO: update with alpha or configurable maybe
      // ==========
      List<TextureFormat> compressFormats = new List<TextureFormat>();
      compressFormats.Add(TextureFormat.PVRTC_RGB4);
      compressFormats.Add(TextureFormat.DXT1);
      compressFormats.Add(TextureFormat.ETC_RGB4);


      EditorUtility.DisplayProgressBar("Texture Compress", "Compressing textures", 0.0f);
      float startTime = (float)EditorApplication.timeSinceStartup;


      int clen = compressFormats.Count;
      int idx = 0;

      foreach (TextureFormat targetFormat in compressFormats)
      {

        EditorUtility.DisplayProgressBar(
          "Texture Compress (" + Mathf.Floor((float)(EditorApplication.timeSinceStartup - startTime)) + "s)",
          "Compressing texture " + texture.name + " ... ", (float)clen / (float)idx
        );
        idx++;

        var destRenderTexture = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(texture, destRenderTexture);

        TextureFormat tgt = targetFormat;
        TextureFormat fmt = Compressed_GetSourceFormat(tgt);
        bool mip = setting.GenerateMipMap;

        Texture2D exportTexture = new Texture2D(texture.width, texture.height, fmt, mip);

        exportTexture.ReadPixels(new Rect(0, 0, destRenderTexture.width, destRenderTexture.height), 0, 0);
        FlipTextureVertically(exportTexture);
        EditorUtility.CompressTexture(exportTexture, tgt, TextureCompressionQuality.Best);
        exportTexture.Apply();

        try
        {

          // Write data
          byte[] data = KTX.Encode(exportTexture, exportTexture.format);
          var filePath = ConstructImageFilenamePath(source, outputPath);
          string p = filePath + KTX.GetExt(targetFormat);
          File.WriteAllBytes(p, data);

        }
        catch (Exception e)
        {
          EditorUtility.ClearProgressBar();
          throw e;
        }


        RenderTexture.ReleaseTemporary(destRenderTexture);

        if (Application.isEditor)
        {
          GameObject.DestroyImmediate(exportTexture);
        }
        else
        {
          GameObject.Destroy(exportTexture);
        }

      }

      EditorUtility.ClearProgressBar();

    }

  }

}