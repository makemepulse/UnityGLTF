

using System.Collections.Generic;
using UnityEngine;

public class TextureMap : Dictionary<string, GLTFExportTextureSettings> { }

[System.Serializable]
public class TextureSettingsLink
{
  public string guid;
  public GLTFExportTextureSettings settings;
}



[System.Serializable]
public class GLTFExportSettingsData
{

  [SerializeField]
  private bool m_PreserveHierarchy;
  public bool PreserveHierarchy
  {
    get { return m_PreserveHierarchy; }
  }


  [SerializeField]
  private GLTFExportMeshSettingsSerializable m_meshSettings;
  public GLTFExportMeshSettingsSerializable MeshSettings
  {
    get { return m_meshSettings; }
  }

  [SerializeField]
  private GLTFExportAnimationsSettingsSerializable m_animationSettings;
  public GLTFExportAnimationsSettingsSerializable AnimationsSettings
  {
    get { return m_animationSettings; }
  }

  [SerializeField]
  public GLTFExportTextureSettings m_textureSettingsDefault;
  public GLTFExportTextureSettings TextureSettingsDefaults
  {
    get { return m_textureSettingsDefault; }
  }

  [SerializeField]
  public GLTFTexturesRegistry m_texRegistry;
  public GLTFTexturesRegistry TexturesRegistry
  {
    get { return m_texRegistry; }
  }

}

