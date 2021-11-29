using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GLTF.Schema;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityGLTF.Extensions;
using CameraType = GLTF.Schema.CameraType;
using WrapMode = GLTF.Schema.WrapMode;

namespace UnityGLTF
{

  public partial class GLTFSceneExporter
  {

    private EXT_LightsImageBasedExtension EXT_LightsImageBased;

    private Material _extIblMaterial;

    // private Material _envMaterialOctRgbe;

    private void ExportIBL(ReflectionProbe probe, GLTFScene scene)
    {

      if (EXT_LightsImageBased == null)
      {
        EXT_LightsImageBased = new EXT_LightsImageBasedExtension();
        DeclareExtensionUsage(EXT_LightsImageBasedExtensionFactory.EXTENSION_NAME, false);
        _root.AddExtension(EXT_LightsImageBasedExtensionFactory.EXTENSION_NAME, EXT_LightsImageBased);
      }

      // LightsImageBased
      if (_extIblMaterial == null)
      {
        var envShader = Resources.Load("EXT_LightImageBasedExport", typeof(Shader)) as Shader;
        _extIblMaterial = new Material(envShader);
        _extIblMaterial.SetVector("_MainTex_HDR", probe.textureHDRDecodeValues);
      }

      ImageBasedLight light = new ImageBasedLight();

      light.LightName = probe.gameObject.name;
      light.Intensity = probe.intensity;

      light.IrradianceCoefficients = GLTFIBLUtils.ExtractSHCoefficients(RenderSettings.ambientProbe);

      light.SpecularImageSize = probe.texture.width;

      // 0 : +X
      // 1 : -X
      // 2 : +Y
      // 3 : -Y
      // 4 : +Z
      // 5 : -Z
      string[] facesName = new string[]{
                "F0_px_rgbd",
                "F1_nx_rgbd",
                "F2_py_rgbd",
                "F3_ny_rgbd",
                "F4_pz_rgbd",
                "F5_nz_rgbd",
            };

      Quaternion[] rotations = new Quaternion[]{
                Quaternion.identity,
                Quaternion.Euler(0.0f,  180f,      0.0f),
                Quaternion.Euler(90.0f,   0f,    -90.0f),
                Quaternion.Euler(-90.0f,     0f,     90.0f),
                Quaternion.Euler(0.0f,    90f,     0.0f),
                Quaternion.Euler(0.0f,   -90f,     0.0f),
            };


      int mipcount = Math.Min(8, probe.texture.mipmapCount);
      int width = probe.texture.width;
      int height = probe.texture.height;
      light.SpecularImages = new ImageId[mipcount][];

      for (var x = 0; x < mipcount; x++)
      {

        light.SpecularImages[x] = new ImageId[6];

        for (var y = 0; y < 6; y++)
        {

          Texture2D exportTexture = new Texture2D(width, height);

          var destRenderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

          Vector4 v4 = new Vector4();
          v4.Set(rotations[y].x, rotations[y].y, rotations[y].z, rotations[y].w);

          _extIblMaterial.SetVector("_Rotation", v4);
          _extIblMaterial.SetFloat("_MipLevel", x);
          Graphics.Blit(probe.texture, destRenderTexture, _extIblMaterial);

          exportTexture.ReadPixels(new Rect(0, 0, destRenderTexture.width, destRenderTexture.height), 0, 0);
          exportTexture.Apply();

          RenderTexture.ReleaseTemporary(destRenderTexture);

          exportTexture.name = "IBL/" + probe.texture.name + "/" + facesName[y] + "_L" + x;

          ImageId imgId = ExportImage(exportTexture, TextureMapType.Main);

          light.SpecularImages[x][y] = imgId;

        }

        width /= 2;
        height /= 2;

      }


      EXT_LightsImageBased.Lights.Add(light);

      var lightId = new ImageBasedLightId()
      {
        Id = EXT_LightsImageBased.Lights.Count - 1,
        Root = _root
      };

      var LightRef = new EXT_LightsImageBasedSceneExtension()
      {
        LightId = lightId
      };

      if (scene.Extensions == null || !scene.Extensions.ContainsKey(EXT_LightsImageBasedExtensionFactory.EXTENSION_NAME))
      {
        scene.AddExtension(EXT_LightsImageBasedExtensionFactory.EXTENSION_NAME, LightRef);
      }

    }

  }

}