using System;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class GLTFExportTextureSettings
{

  // TODO
  [HideInInspector]
  [SerializeField]
  public bool flipY;

  public bool Compress = false;
  public bool ExportWebp = false;
  /**
  * Jpeg quality between -1 and 100
  */
  [Range(-1, 100)]
  public int WebpQuality = 90;
  public bool ExportJPG = false;
  /**
  * Jpeg quality between 0 and 100
  */
  [Range(0, 100)]
  public int Quality = 90;

  public bool Equals(GLTFExportTextureSettings x)
  {
    bool equals = flipY == x.flipY;
    equals = equals && Compress == x.Compress;
    equals = equals && ExportWebp == x.ExportWebp;
    equals = equals && WebpQuality == x.WebpQuality;
    equals = equals && ExportJPG == x.ExportJPG;
    equals = equals && Quality == x.Quality;
    return equals;
  }

}