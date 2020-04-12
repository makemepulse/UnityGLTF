using System;
using Newtonsoft.Json.Linq;
using GLTF.Math;
using Newtonsoft.Json;
using GLTF.Extensions;

namespace GLTF.Schema
{
	public class KHR_materials_pbrSpecularGlossinessExtensionFactory : ExtensionFactory
	{
		public const string EXTENSION_NAME = "KHR_materials_pbrSpecularGlossiness";
		public const string DIFFUSE_FACTOR = "diffuseFactor";
		public const string DIFFUSE_TEXTURE = "diffuseTexture";
		public const string SPECULAR_FACTOR = "specularFactor";
		public const string GLOSSINESS_FACTOR = "glossinessFactor";
		public const string SPECULAR_GLOSSINESS_TEXTURE = "specularGlossinessTexture";

		public KHR_materials_pbrSpecularGlossinessExtensionFactory()
		{
			ExtensionName = EXTENSION_NAME;
		}

		public override IExtension Deserialize(GLTFRoot root, JProperty extensionToken)
		{

      var extension = new KHR_materials_pbrSpecularGlossinessExtension();

			if (extensionToken != null)
			{
#if DEBUG
				// Broken on il2cpp. Don't ship debug DLLs there.
				System.Diagnostics.Debug.WriteLine(extensionToken.Value.ToString());
				System.Diagnostics.Debug.WriteLine(extensionToken.Value.Type);
#endif

				JToken diffuseFactorToken = extensionToken.Value[DIFFUSE_FACTOR];
        if( diffuseFactorToken != null ){
          extension.DiffuseFactor = diffuseFactorToken.DeserializeAsColor();
        }
        
        JToken diffuseTextureToken = extensionToken.Value[DIFFUSE_TEXTURE];
        if( diffuseTextureToken != null ){
          extension.DiffuseTexture = diffuseTextureToken.DeserializeAsTexture(root);
        }

				JToken specularFactorToken = extensionToken.Value[SPECULAR_FACTOR];
        if( specularFactorToken != null ){
          extension.SpecularFactor = specularFactorToken.DeserializeAsVector3();
        }

				JToken glossinessFactorToken = extensionToken.Value[GLOSSINESS_FACTOR];
        if( glossinessFactorToken != null ){
          extension.GlossinessFactor = glossinessFactorToken.DeserializeAsDouble();
        }

        JToken specGlossTextureToken = extensionToken.Value[SPECULAR_GLOSSINESS_TEXTURE];
        if( specGlossTextureToken != null ){
          extension.SpecularGlossinessTexture = specGlossTextureToken.DeserializeAsTexture(root);
        }
			}

			return extension;
		}
	}
}
