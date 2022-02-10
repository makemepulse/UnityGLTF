using System;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class GLTFExportTextureSettings
{

  // TODO
  public enum Resize
  {
    None = 0,
    Quart = 1,
    Half = 2
  }

  // TODO
  [HideInInspector]
  [SerializeField]
  public Resize resize = Resize.None;

  // TODO
  [HideInInspector]
  [SerializeField]
  public bool flipY;

  public bool Compress = false;
  public bool GenerateMipMap = false;

  public bool ExportJPG = false;
  /**
  * Jpeg quality between 0 and 100
  */
  [Range(0, 100)]
  public int Quality = 90;

  public bool Equals(GLTFExportTextureSettings x)
  {
    bool equals = resize == x.resize;
    equals = equals && flipY == x.flipY;
    equals = equals && Compress == x.Compress;
    equals = equals && GenerateMipMap == x.GenerateMipMap;
    equals = equals && ExportJPG == x.ExportJPG;
    equals = equals && Quality == x.Quality;
    return equals;
  }

}