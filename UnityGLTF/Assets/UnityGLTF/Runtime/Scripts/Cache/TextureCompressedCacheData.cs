using GLTF.Schema;
using System;
using UnityEngine;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityGLTF.Cache
{

  [System.Serializable]
  public class TextureCompressedCacheData : IDisposable
  {

    public Texture Texture;
    public string hash;
    public GLTFExportTextureSettings exportSettings;
    public TextureFormat format;

#if UNITY_EDITOR
    public static string GetHashForTexture(Texture texture)
    {
      return AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(texture)).ToString();
    }
#else
    public static string GetHashForTexture(Texture texture)
    {
      return (new Hash128()).ToString();
    }
#endif

    public TextureCompressedCacheData(Texture tex, GLTFExportTextureSettings settings, TextureFormat fmt)
    {
      Texture = tex;
      exportSettings = settings;
      format = fmt;
      hash = GetHashForTexture(tex);
    }

    public void WriteBytes(byte[] data)
    {
      File.WriteAllBytes(GetPath(), data);
    }

    public byte[] ReadBytes()
    {
      return File.ReadAllBytes(GetPath());
    }

    public bool Validate(GLTFExportTextureSettings settings)
    {
      bool valid = GetHashForTexture(Texture) == hash;
      valid = valid && File.Exists(GetPath());
      valid = valid && exportSettings.Equals(settings);
      if (!valid)
      {
        Clear();
        exportSettings = settings;
        hash = GetHashForTexture(Texture);
      }
      return valid;
    }

    public string GetPath()
    {
      var filenamePath = Path.Combine(Application.dataPath + "/" + GLTFTexturesRegistry.CACHE_LOCATION, hash);
      var file = new FileInfo(filenamePath);
      file.Directory.Create();
      var outPath = Path.ChangeExtension(filenamePath, "." + format.ToString());
      return outPath;
    }

    public void Clear()
    {
      try
      {
        File.Delete(GetPath());
      }
      catch (Exception e)
      {
        Debug.LogWarning("[CompressedCache] No file deleted : " + e.ToString());
      }
    }

    public void Dispose()
    {
      Clear();
    }


  }

}