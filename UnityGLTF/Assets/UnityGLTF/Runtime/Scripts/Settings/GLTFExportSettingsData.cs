

using UnityEngine;

[System.Serializable]
public class GLTFExportSettingsData {

  [SerializeField]
  private GLTFExportMeshSettingsSerializable m_meshSettings;
  public GLTFExportMeshSettingsSerializable MeshSettings {
    get { return m_meshSettings; }
  }

  [SerializeField]
  private GLTFExportAnimationsSettingsSerializable m_animationSettings;
  public GLTFExportAnimationsSettingsSerializable AnimationsSettings {
    get { return m_animationSettings; }
  }
  
}

