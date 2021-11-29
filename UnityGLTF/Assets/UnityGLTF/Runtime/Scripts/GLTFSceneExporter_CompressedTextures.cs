
using System;
using System.Collections.Generic;
using System.IO;
using GLTF.Schema;
using UnityEditor;
using UnityEngine;
using UnityGLTF.Cache;
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

        byte[] data = new byte[0];
        TextureFormat tgt = targetFormat;

        string ext = KTX.GetExt(targetFormat);
        TextureFormat fmt = Compressed_GetSourceFormat(tgt);
        bool mip = setting.GenerateMipMap;

        TextureCompressedCacheData cache;
        bool validCache = reg.GetOrCreateCacheData(source, setting, tgt, out cache);

        if (!validCache)
        {
          Texture2D exportTexture = new Texture2D(texture.width, texture.height, fmt, mip);
          exportTexture.SetPixels32(texture.GetPixels32());

          FlipTextureVertically(exportTexture);
          EditorUtility.CompressTexture(exportTexture, tgt, TextureCompressionQuality.Best);
          exportTexture.Apply();
          // Write data
          data = KTX.Encode(exportTexture, exportTexture.format);
          cache.WriteBytes(data);
          GameObject.DestroyImmediate(exportTexture);
        }
        else
        {
          data = cache.ReadBytes();
        }

        try
        {
          var filePath = ConstructImageFilenamePath(source, outputPath);
          string p = filePath + ext;
          File.WriteAllBytes(p, data);
        }
        catch (Exception e)
        {
          EditorUtility.ClearProgressBar();
          throw e;
        }

      }

      EditorUtility.ClearProgressBar();

    }

  }

}