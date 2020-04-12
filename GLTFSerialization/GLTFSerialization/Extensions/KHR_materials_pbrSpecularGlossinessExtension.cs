using GLTF.Math;
using GLTF.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GLTF.Schema
{
	/// <summary>
	/// glTF extension that defines the specular-glossiness 
	/// material model from Physically-Based Rendering (PBR) methodology.
	/// 
	/// Spec can be found here:
	/// https://github.com/KhronosGroup/glTF/tree/master/extensions/Khronos/KHR_materials_pbrSpecularGlossiness
	/// </summary>
	public class KHR_materials_pbrSpecularGlossinessExtension : GLTFProperty, IExtension
	{
		public static readonly Vector3 SPEC_FACTOR_DEFAULT = new Vector3(0.2f, 0.2f, 0.2f);
		public static readonly double GLOSS_FACTOR_DEFAULT = 0.5d; 

		/// <summary>
		/// The RGBA components of the reflected diffuse color of the material. 
		/// Metals have a diffuse value of [0.0, 0.0, 0.0]. 
		/// The fourth component (A) is the alpha coverage of the material. 
		/// The <see cref="GLTFMaterial.AlphaMode"/> property specifies how alpha is interpreted. 
		/// The values are linear.
		/// </summary>
		public Color DiffuseFactor = Color.White;

		/// <summary>
		/// The diffuse texture. 
		/// This texture contains RGB(A) components of the reflected diffuse color of the material in sRGB color space. 
		/// If the fourth component (A) is present, it represents the alpha coverage of the 
		/// material. Otherwise, an alpha of 1.0 is assumed. 
		/// The <see cref="GLTFMaterial.AlphaMode"/> property specifies how alpha is interpreted. 
		/// The stored texels must not be premultiplied.
		/// </summary>
		public TextureInfo DiffuseTexture;

		/// <summary>
		/// The specular RGB color of the material. This value is linear
		/// </summary>
		public Vector3 SpecularFactor = SPEC_FACTOR_DEFAULT;

		/// <summary>
		/// The glossiness or smoothness of the material. 
		/// A value of 1.0 means the material has full glossiness or is perfectly smooth. 
		/// A value of 0.0 means the material has no glossiness or is completely rough. 
		/// This value is linear.
		/// </summary>
		public double GlossinessFactor = GLOSS_FACTOR_DEFAULT;

		/// <summary>
		/// The specular-glossiness texture is RGBA texture, containing the specular color of the material (RGB components) and its glossiness (A component). 
		/// The values are in sRGB space.
		/// </summary>
		public TextureInfo SpecularGlossinessTexture;

		public KHR_materials_pbrSpecularGlossinessExtension()
		{

		}

    public KHR_materials_pbrSpecularGlossinessExtension( KHR_materials_pbrSpecularGlossinessExtension ext, GLTFRoot root ) : base( ext, root )
		{
			DiffuseFactor = ext.DiffuseFactor;
			SpecularFactor = ext.SpecularFactor;
			GlossinessFactor = ext.GlossinessFactor;
			DiffuseTexture = new TextureInfo( ext.DiffuseTexture, root );
			SpecularGlossinessTexture = new TextureInfo( ext.SpecularGlossinessTexture, root );
		}

    public KHR_materials_pbrSpecularGlossinessExtension(Color diffuseFactor, TextureInfo diffuseTexture, Vector3 specularFactor, double glossinessFactor, TextureInfo specularGlossinessTexture)
		{
			DiffuseFactor = diffuseFactor;
			DiffuseTexture = diffuseTexture;
			SpecularFactor = specularFactor;
			GlossinessFactor = glossinessFactor;
			SpecularGlossinessTexture = specularGlossinessTexture;
		}

		public IExtension Clone(GLTFRoot gltfRoot)
		{
			return new KHR_materials_pbrSpecularGlossinessExtension( this, gltfRoot );
		}

		// public JProperty Serialize()
		// {
		// 	JProperty jProperty =
		// 		new JProperty(KHR_materials_pbrSpecularGlossinessExtensionFactory.EXTENSION_NAME,
		// 			new JObject(
		// 				new JProperty(KHR_materials_pbrSpecularGlossinessExtensionFactory.DIFFUSE_FACTOR, new JArray(DiffuseFactor.R, DiffuseFactor.G, DiffuseFactor.B, DiffuseFactor.A)),
		// 				new JProperty(KHR_materials_pbrSpecularGlossinessExtensionFactory.DIFFUSE_TEXTURE,
		// 					new JObject(
		// 						new JProperty(TextureInfo.INDEX, DiffuseTexture.Index.Id)
		// 						)
		// 					),
		// 				new JProperty(KHR_materials_pbrSpecularGlossinessExtensionFactory.SPECULAR_FACTOR, new JArray(SpecularFactor.X, SpecularFactor.Y, SpecularFactor.Z)),
		// 				new JProperty(KHR_materials_pbrSpecularGlossinessExtensionFactory.GLOSSINESS_FACTOR, GlossinessFactor),
		// 				new JProperty(KHR_materials_pbrSpecularGlossinessExtensionFactory.SPECULAR_GLOSSINESS_TEXTURE,
		// 					new JObject(
		// 						new JProperty(TextureInfo.INDEX, SpecularGlossinessTexture.Index.Id)
		// 						)
		// 					)
		// 				)
		// 			);

		// 	return jProperty;
		// }


		override public void Serialize(JsonWriter writer)
		{
			writer.WritePropertyName(KHR_materials_pbrSpecularGlossinessExtensionFactory.EXTENSION_NAME);
			writer.WriteStartObject();

			if (DiffuseFactor != Color.White)
			{
				writer.WritePropertyName(KHR_materials_pbrSpecularGlossinessExtensionFactory.DIFFUSE_FACTOR);
				writer.WriteStartArray();
				writer.WriteValue(DiffuseFactor.R);
				writer.WriteValue(DiffuseFactor.G);
				writer.WriteValue(DiffuseFactor.B);
				writer.WriteValue(DiffuseFactor.A);
				writer.WriteEndArray();
			}

			if (SpecularFactor != SPEC_FACTOR_DEFAULT)
			{
				writer.WritePropertyName(KHR_materials_pbrSpecularGlossinessExtensionFactory.SPECULAR_FACTOR);
				writer.WriteStartArray();
				writer.WriteValue(SpecularFactor.X);
				writer.WriteValue(SpecularFactor.Y);
				writer.WriteValue(SpecularFactor.Z);
				writer.WriteEndArray();
			}

			if (GlossinessFactor != GLOSS_FACTOR_DEFAULT)
			{
				writer.WritePropertyName(KHR_materials_pbrSpecularGlossinessExtensionFactory.GLOSSINESS_FACTOR);
				writer.WriteValue(GlossinessFactor);
			}

			if (DiffuseTexture != null)
			{
				writer.WritePropertyName(KHR_materials_pbrSpecularGlossinessExtensionFactory.DIFFUSE_TEXTURE);
				DiffuseTexture.Serialize( writer );
			}

			if (SpecularGlossinessTexture != null)
			{
				writer.WritePropertyName(KHR_materials_pbrSpecularGlossinessExtensionFactory.SPECULAR_GLOSSINESS_TEXTURE);
				SpecularGlossinessTexture.Serialize( writer );
			}

			base.Serialize(writer);

			writer.WriteEndObject();
		}


		public JProperty Serialize()
		{
			JTokenWriter writer = new JTokenWriter();
			Serialize(writer);
			return (JProperty)writer.Token;
		}

	}
}
