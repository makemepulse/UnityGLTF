using UnityEngine;


[System.Serializable]
public class GLTFExportAnimationsSettingsSerializable
{
  [SerializeField]
  public bool exportAnimations;
}

public class GLTFExportAnimationsSettings : SerializedSettings<GLTFExportAnimationsSettingsSerializable>{}
