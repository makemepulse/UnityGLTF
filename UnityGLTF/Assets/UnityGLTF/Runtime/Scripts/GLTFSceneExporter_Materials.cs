
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GLTF.Schema;
using UnityEngine;
using UnityEngine.Rendering;
using UnityGLTF.Extensions;
using CameraType = GLTF.Schema.CameraType;
using WrapMode = GLTF.Schema.WrapMode;

namespace UnityGLTF
{
	
	public partial class GLTFSceneExporter
	{


		private MaterialId ExportMaterial(Material materialObj)
		{
			MaterialId id = GetMaterialId(_root, materialObj);
			if (id != null)
			{
				return id;
			}

			var material = new GLTFMaterial();

			if (ExportNames)
			{
				material.Name = materialObj.name;
			}

			if (materialObj.HasProperty("_Cutoff"))
			{
				material.AlphaCutoff = materialObj.GetFloat("_Cutoff");
			}

			switch (materialObj.GetTag("RenderType", false, ""))
			{
				case "TransparentCutout":
					material.AlphaMode = AlphaMode.MASK;
					break;
				case "Transparent":
					material.AlphaMode = AlphaMode.BLEND;
					break;
				default:
					material.AlphaMode = AlphaMode.OPAQUE;
					break;
			}

			material.DoubleSided = materialObj.HasProperty("_Cull") &&
				materialObj.GetInt("_Cull") == (float)CullMode.Off;

			if(materialObj.IsKeywordEnabled("_EMISSION"))
			{ 
				if (materialObj.HasProperty("_EmissionColor"))
				{
					material.EmissiveFactor = materialObj.GetColor("_EmissionColor").ToNumericsColorRaw();
				}

				if (materialObj.HasProperty("_EmissionMap"))
				{
					var emissionTex = materialObj.GetTexture("_EmissionMap");

					if (emissionTex != null)
					{
						if(emissionTex is Texture2D)
						{
							material.EmissiveTexture = ExportTextureInfo(emissionTex, TextureMapType.Emission);

							ExportTextureTransform(material.EmissiveTexture, materialObj, "_EmissionMap");
						}
						else
						{
							Debug.LogErrorFormat("Can't export a {0} emissive texture in material {1}", emissionTex.GetType(), materialObj.name);
						}

					}
				}
			}
			if (materialObj.HasProperty("_BumpMap") && materialObj.IsKeywordEnabled("_NORMALMAP"))
			{
				var normalTex = materialObj.GetTexture("_BumpMap");

				if (normalTex != null)
				{
					if(normalTex is Texture2D)
					{
						material.NormalTexture = ExportNormalTextureInfo(normalTex, TextureMapType.Bump, materialObj);
						ExportTextureTransform(material.NormalTexture, materialObj, "_BumpMap");
					}
					else
					{
						Debug.LogErrorFormat("Can't export a {0} normal texture in material {1}", normalTex.GetType(), materialObj.name);
					}
				}
			}

			if (materialObj.HasProperty("_OcclusionMap"))
			{
				var occTex = materialObj.GetTexture("_OcclusionMap");
				if (occTex != null)
				{
					if(occTex is Texture2D)
					{
						material.OcclusionTexture = ExportOcclusionTextureInfo(occTex, TextureMapType.Occlusion, materialObj);
						ExportTextureTransform(material.OcclusionTexture, materialObj, "_OcclusionMap");
					}
					else
					{
						Debug.LogErrorFormat("Can't export a {0} occlusion texture in material {1}", occTex.GetType(), materialObj.name);
					}
				}
			}

			if( IsUnlit(materialObj)){

				ExportUnlit( material, materialObj );
			}
			else if (IsPBRMetallicRoughness(materialObj))
			{
				material.PbrMetallicRoughness = ExportPBRMetallicRoughness(materialObj);
			}
			else if (IsPBRSpecularGlossiness(materialObj))
			{
				ExportPBRSpecularGlossiness( material, materialObj );
			}
			else if (IsCommonConstant(materialObj))
			{
				material.CommonConstant = ExportCommonConstant(materialObj);
			}

			_materials.Add(materialObj);

			id = new MaterialId
			{
				Id = _root.Materials.Count,
				Root = _root
			};
			_root.Materials.Add(material);

			return id;
		}



		private bool IsPBRMetallicRoughness(Material material)
		{
			return material.HasProperty("_Metallic") && material.HasProperty("_MetallicGlossMap");
		}

		private bool IsUnlit(Material material)
		{
			return material.shader.name.ToLowerInvariant().Contains("unlit");
		}

		private bool IsPBRSpecularGlossiness(Material material)
		{
			return material.HasProperty("_SpecColor") && material.HasProperty("_SpecGlossMap");
		}

		private bool IsCommonConstant(Material material)
		{
			return material.HasProperty("_AmbientFactor") &&
			material.HasProperty("_LightMap") &&
			material.HasProperty("_LightFactor");
		}


		private void ExportTextureTransform(TextureInfo def, Material mat, string texName)
		{
			Vector2 offset = mat.GetTextureOffset(texName);
			Vector2 scale = mat.GetTextureScale(texName);

      // most material only use the main tex transform to sample all textures
			if (offset == Vector2.zero && scale == Vector2.one) {
		
			  offset = mat.GetTextureOffset("_MainTex");
			  scale = mat.GetTextureScale("_MainTex");
			  
        if (offset == Vector2.zero && scale == Vector2.one) 
          return;
      }

			DeclareExtensionUsage( ExtTextureTransformExtensionFactory.EXTENSION_NAME, true );


			def.AddExtension( ExtTextureTransformExtensionFactory.EXTENSION_NAME, new ExtTextureTransformExtension(
				new GLTF.Math.Vector2(offset.x, -offset.y),
				0, // TODO: support rotation
				new GLTF.Math.Vector2(scale.x, scale.y),
				0 // TODO: support UV channels
			));
		}

		private NormalTextureInfo ExportNormalTextureInfo(
			Texture texture,
			TextureMapType textureMapType,
			Material material)
		{
			var info = new NormalTextureInfo();

			info.Index = ExportTexture(texture, textureMapType);

			if (material.HasProperty("_BumpScale"))
			{
				info.Scale = material.GetFloat("_BumpScale");
			}

			return info;
		}

		private OcclusionTextureInfo ExportOcclusionTextureInfo(
			Texture texture,
			TextureMapType textureMapType,
			Material material)
		{
			var info = new OcclusionTextureInfo();

			info.Index = ExportTexture(texture, textureMapType);

			if (material.HasProperty("_OcclusionStrength"))
			{
				info.Strength = material.GetFloat("_OcclusionStrength");
			}

			return info;
		}

		private PbrMetallicRoughness ExportPBRMetallicRoughness(Material material)
		{
			var pbr = new PbrMetallicRoughness();

			if (material.HasProperty("_Color"))
			{
				pbr.BaseColorFactor = material.GetColor("_Color").ToNumericsColorLinear();
			}

			if (material.HasProperty("_MainTex"))
			{
				var mainTex = material.GetTexture("_MainTex");

				if (mainTex != null)
				{
					if(mainTex is Texture2D)
					{
						pbr.BaseColorTexture = ExportTextureInfo(mainTex, TextureMapType.Main);
						ExportTextureTransform(pbr.BaseColorTexture, material, "_MainTex");
					}
					else
					{
						Debug.LogErrorFormat("Can't export a {0} base texture in material {1}", mainTex.GetType(), material.name);
					}
				}
			}

			if (material.HasProperty("_Metallic"))
			{
				var metallicGlossMap = material.GetTexture("_MetallicGlossMap");
				pbr.MetallicFactor = (metallicGlossMap != null) ? 1.0 : material.GetFloat("_Metallic");
			}

			if (material.HasProperty("_Glossiness"))
			{
				var metallicGlossMap = material.GetTexture("_MetallicGlossMap");
				pbr.RoughnessFactor = (metallicGlossMap != null) ? 1.0 : 1.0 - material.GetFloat("_Glossiness");
			}

			if (material.HasProperty("_MetallicGlossMap"))
			{
				var mrTex = material.GetTexture("_MetallicGlossMap");

				if (mrTex != null)
				{
					if(mrTex is Texture2D)
					{
						pbr.MetallicRoughnessTexture = ExportTextureInfo(mrTex, TextureMapType.MetallicGloss);
						ExportTextureTransform(pbr.MetallicRoughnessTexture, material, "_MetallicGlossMap");
					}
					else
					{
						Debug.LogErrorFormat("Can't export a {0} metallic smoothness texture in material {1}", mrTex.GetType(), material.name);
					}
				}
			}
			else if (material.HasProperty("_SpecGlossMap"))
			{
				var mgTex = material.GetTexture("_SpecGlossMap");

				if (mgTex != null)
				{
					if(mgTex is Texture2D)
					{
						pbr.MetallicRoughnessTexture = ExportTextureInfo(mgTex, TextureMapType.SpecGloss);
						ExportTextureTransform(pbr.MetallicRoughnessTexture, material, "_SpecGlossMap");
					}
					else
					{
						Debug.LogErrorFormat("Can't export a {0} metallic roughness texture in material {1}", mgTex.GetType(), material.name);
					}
				}
			}

			return pbr;
		}

		private void ExportUnlit(GLTFMaterial def, Material material){
			
			const string extname = KHR_MaterialsUnlitExtensionFactory.EXTENSION_NAME;
			DeclareExtensionUsage( extname, true );
			def.AddExtension( extname, new KHR_MaterialsUnlitExtension());

			var pbr = new PbrMetallicRoughness();

			if (material.HasProperty("_Color"))
			{
				pbr.BaseColorFactor = material.GetColor("_Color").ToNumericsColorLinear();
			}

			if (material.HasProperty("_MainTex"))
			{
				var mainTex = material.GetTexture("_MainTex");
				if (mainTex != null)
				{
					if(mainTex is Texture2D)
					{
						pbr.BaseColorTexture = ExportTextureInfo(mainTex, TextureMapType.Main);
						ExportTextureTransform(pbr.BaseColorTexture, material, "_MainTex");
					}
					else
					{
						Debug.LogErrorFormat("Can't export a {0} base texture in material {1}", mainTex.GetType(), material.name);
					}
				}
			}

			def.PbrMetallicRoughness = pbr;

		}
		

		private void ExportPBRSpecularGlossiness(GLTFMaterial def, Material material){
			const string extname = KHR_materials_pbrSpecularGlossinessExtensionFactory.EXTENSION_NAME;

			DeclareExtensionUsage( extname, true );

			GLTF.Math.Color baseColor = GLTF.Math.Color.Black;
			if (material.HasProperty("_Color"))
			{
				baseColor = material.GetColor("_Color").ToNumericsColorLinear();
			}





			TextureInfo colorTexture = null;
			var albedoTex = material.GetTexture("_MainTex");
			if (albedoTex != null)
			{
				if(albedoTex is Texture2D)
				{
					colorTexture = ExportTextureInfo(albedoTex, TextureMapType.SpecGloss);
					ExportTextureTransform(colorTexture, material, "_MainTex");
				}
				else
				{
					Debug.LogErrorFormat("Can't export a {0} color texture texture in material {1}", albedoTex.GetType(), material.name);
				}
			}
			

			TextureInfo specGlossTexture = null;
			GLTF.Math.Vector3 specColor = GLTF.Math.Vector3.One;

			double glossFactor = 1d;

      
			var sgTex = material.GetTexture("_SpecGlossMap");
			if (sgTex != null)
			{
				if(sgTex is Texture2D)
				{
					if (material.HasProperty("_GlossMapScale"))
          {
            glossFactor = material.GetFloat("_GlossMapScale");
          }

					if( Array.Exists( material.shaderKeywords, (string s)=>s=="_SMOOTHNESS_TEXTURE_ALBEDO_CH" ) ){
						Debug.LogWarning("Specular setup - glossiness in albedo alpha not supported");
					}

					specGlossTexture = ExportTextureInfo(sgTex, TextureMapType.SpecGloss);
					ExportTextureTransform(specGlossTexture, material, "_SpecGlossMap");
				}
				else
				{
					Debug.LogErrorFormat("Can't export a {0} specular glossiness texture in material {1}", sgTex.GetType(), material.name);
				}
			} else {

				if (material.HasProperty("_SpecColor"))
				{
          if (material.HasProperty("_Glossiness"))
          {
            glossFactor = material.GetFloat("_Glossiness");
          }

					var specProperty = material.GetColor("_SpecColor").linear;
					specColor.X = specProperty.r;
					specColor.Y = specProperty.g;
					specColor.Z = specProperty.b;
				}
			}
			

			def.AddExtension( extname, new KHR_materials_pbrSpecularGlossinessExtension(
				baseColor, 
				colorTexture, 
				specColor,
				glossFactor,
				specGlossTexture
			));
		}

		private MaterialCommonConstant ExportCommonConstant(Material materialObj)
		{

			DeclareExtensionUsage( "KHR_materials_common", true );
			
			var constant = new MaterialCommonConstant();

			if (materialObj.HasProperty("_AmbientFactor"))
			{
				constant.AmbientFactor = materialObj.GetColor("_AmbientFactor").ToNumericsColorRaw();
			}

			if (materialObj.HasProperty("_LightMap"))
			{
				var lmTex = materialObj.GetTexture("_LightMap");

				if (lmTex != null)
				{
					constant.LightmapTexture = ExportTextureInfo(lmTex, TextureMapType.Light);
					ExportTextureTransform(constant.LightmapTexture, materialObj, "_LightMap");
				}

			}

			if (materialObj.HasProperty("_LightFactor"))
			{
				constant.LightmapFactor = materialObj.GetColor("_LightFactor").ToNumericsColorRaw();
			}

			return constant;
		}

	}
}