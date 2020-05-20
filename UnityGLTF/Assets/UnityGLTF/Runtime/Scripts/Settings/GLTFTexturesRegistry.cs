
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif



[System.Serializable]
public class GLTFTexturesRegistry
{

  [SerializeField]
  private List<GLTFTextureSettingsBinding> m_bindings;
  public List<GLTFTextureSettingsBinding> Bindings
  {
    get
    {
      if (m_bindings == null)
      {
        m_bindings = new List<GLTFTextureSettingsBinding>();
      }
      return m_bindings;
    }
  }


#if UNITY_EDITOR

  private string GetGUID(Object obj)
  {
    string guid;
    long localid;
    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out localid);
    return guid;
  }

  public GLTFTextureSettingsBinding GetBinding(Texture2D tex)
  {
    GLTFTextureSettingsBinding binding = null;
    for (var i = 0; i < Bindings.Count; i++)
    {
      if (m_bindings[i].texture == tex)
      {
        binding = m_bindings[i];
      }
    }
    return binding;
  }


  public GLTFExportTextureSettings GetSettings(Texture2D texture)
  {

    GLTFTextureSettingsBinding bin = GetBinding(texture);
    if (bin == null)
      return GLTFExportSettings.Defaults.info.TextureSettingsDefaults;
    return bin.settings;

  }


  public GLTFExportTextureSettings CreateSettings(Texture2D texture)
  {

    GLTFTextureSettingsBinding binding = GetBinding(texture);
    if (binding != null)
    {
      Debug.LogWarning("Texture " + texture.name + " already has settings");
      return null;
    }

    binding = ScriptableObject.CreateInstance<GLTFTextureSettingsBinding>();
    binding.hideFlags = HideFlags.None;

    GLTFExportTextureSettings settings = new GLTFExportTextureSettings();
    binding.Attach(texture, settings);
    m_bindings.Add(binding);

    binding.name = texture.name + "_settings_" + GetGUID(texture);

    AssetDatabase.AddObjectToAsset(binding, GLTFExportSettings.Defaults);

    EditorUtility.SetDirty(binding);
    EditorUtility.SetDirty(GLTFExportSettings.Defaults);
    AssetDatabase.SaveAssets();

    return settings;

  }


  public void RemoveSettings(Texture2D texture)
  {

    GLTFTextureSettingsBinding binding = GetBinding(texture);

    if (binding == null)
      return;

    AssetDatabase.RemoveObjectFromAsset(binding);
    binding.settings = null;
    m_bindings.Remove(binding);
    binding = null;

    EditorUtility.SetDirty(GLTFExportSettings.Defaults);
    AssetDatabase.SaveAssets();

  }

#endif

}