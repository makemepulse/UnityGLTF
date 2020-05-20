
using UnityEngine;

[System.Serializable]
public class GLTFTextureSettingsBinding : ScriptableObject
{

  [SerializeField]
  public Texture2D texture;
  [SerializeField]
  public GLTFExportTextureSettings settings;

  public void Attach(Texture2D tex, GLTFExportTextureSettings set)
  {
    texture = tex;
    settings = set;
  }

}