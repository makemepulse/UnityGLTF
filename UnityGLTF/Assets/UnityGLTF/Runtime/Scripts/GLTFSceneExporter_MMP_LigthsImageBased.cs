using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GLTF.Schema;
using UnityEngine;
using Newtonsoft.Json.Linq;
using UnityEngine.Rendering;
using UnityGLTF.Extensions;
using CameraType = GLTF.Schema.CameraType;
using WrapMode = GLTF.Schema.WrapMode;


namespace UnityGLTF
{

  public partial class GLTFSceneExporter
  {

    private MMP_LightsImageBasedExtension MMP_LightsImageBased;

    private Material _mmpIblMaterial;

    private void ExportMMPIBL(ReflectionProbe probe, GLTFScene scene)
    {

      if (MMP_LightsImageBased == null)
      {
        MMP_LightsImageBased = new MMP_LightsImageBasedExtension();
        DeclareExtensionUsage(MMP_LightsImageBasedExtensionFactory.EXTENSION_NAME, false);
        _root.AddExtension(MMP_LightsImageBasedExtensionFactory.EXTENSION_NAME, MMP_LightsImageBased);
      }

      if (_mmpIblMaterial == null)
      {
        var envOctRgbeShader = Resources.Load("MMP_LightImageBasedExport", typeof(Shader)) as Shader;
        _mmpIblMaterial = new Material(envOctRgbeShader);
        _mmpIblMaterial.SetVector("_MainTex_HDR", probe.textureHDRDecodeValues);
      }

      MMPImageBasedLight light = new MMPImageBasedLight();

      light.LightName = probe.gameObject.name;
      light.Intensity = probe.intensity;

      light.IrradianceCoefficients = GLTFIBLUtils.ExtractSHCoefficients(RenderSettings.ambientProbe);

      light.SpecularImageSize = probe.texture.width;

      int width = probe.texture.width * 2;
      int height = probe.texture.height * 8;

      Texture2D exportTexture = new Texture2D(width, height);
      var destRenderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
      Graphics.Blit(probe.texture, destRenderTexture, _mmpIblMaterial);

      exportTexture.ReadPixels(new Rect(0, 0, destRenderTexture.width, destRenderTexture.height), 0, 0);
      exportTexture.Apply();

      RenderTexture.ReleaseTemporary(destRenderTexture);

      exportTexture.name = "IBL/" + probe.texture.name + "/oct_rgbe";

      ImageId imgId = ExportImage(exportTexture, TextureMapType.Main);
      light.SpecularImage = imgId;

      MMP_LightsImageBased.Lights.Add(light);
      
      var lightId = new MMPImageBasedLightId()
      {
        Id = MMP_LightsImageBased.Lights.Count - 1,
        Root = _root
      };

      var LightRef = new MMP_LightsImageBasedSceneExtension()
      {
        LightId = lightId
      };

      if (scene.Extensions == null || !scene.Extensions.ContainsKey(MMP_LightsImageBasedExtensionFactory.EXTENSION_NAME))
      {
        scene.AddExtension(MMP_LightsImageBasedExtensionFactory.EXTENSION_NAME, LightRef);
      }

    }

  }

}