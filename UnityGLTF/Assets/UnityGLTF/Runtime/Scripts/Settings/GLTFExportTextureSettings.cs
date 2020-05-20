﻿using System;
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

}