using System;
using UnityEngine;


[System.Serializable]
public class GLTFExportMeshSettingsSerializable
{

  public enum LODExportType {
    Highest = 0,
    Lowest  = 1,
  }

  [Flags]
  public enum ExportedAttributes {
    Position = 1<<0,
    Normal = 1<<1,
    Tangent = 1<<2,
    UV0 = 1<<3,
    UV1 = 1<<4,
    UV2 = 1<<5,
    UV3 = 1<<6,
  }

  private const ExportedAttributes ALL_ATTRIBUTES = ExportedAttributes.UV3 | (ExportedAttributes.UV3-1);



  [SerializeField]
  public bool exportMeshes;

  [SerializeField]
  public ExportedAttributes attributes = ALL_ATTRIBUTES;

  [SerializeField]
  public LODExportType lod;

}

public class GLTFExportMeshSettings : SerializedSettings<GLTFExportMeshSettingsSerializable>{}
