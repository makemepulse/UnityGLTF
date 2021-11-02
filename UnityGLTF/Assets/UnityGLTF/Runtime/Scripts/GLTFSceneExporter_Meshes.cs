
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


		private static bool ContainsValidRenderer (GameObject gameObject)
		{

		return (
				gameObject.GetComponent<MeshFilter>() != null && 
				gameObject.GetComponent<MeshFilter>().sharedMesh != null && 
				gameObject.GetComponent<MeshRenderer>() != null
			) ||
			(
				gameObject.GetComponent<SkinnedMeshRenderer>() != null && 
				gameObject.GetComponent<SkinnedMeshRenderer>().sharedMesh != null
			) ||
			(
				gameObject.GetComponent<SpriteRenderer>() != null &&
				gameObject.GetComponent<SpriteRenderer>().sprite != null
			)
			;
		}


		private void FilterPrimitives(Transform transform, out GameObject[] primitives, out GameObject[] nonPrimitives)
		{
			var childCount = transform.childCount;
			var prims = new List<GameObject>(childCount + 1);
			var nonPrims = new List<GameObject>(childCount);

			// add another primitive if the root object also has a mesh
			if (transform.gameObject.activeSelf)
			{
				if (ContainsValidRenderer(transform.gameObject))
				{
					prims.Add(transform.gameObject);
				}
			}
			for (var i = 0; i < childCount; i++)
			{
				var go = transform.GetChild(i).gameObject;
				if (IsPrimitive(go) && !GLTFExportSettings.Defaults.info.PreserveHierarchy)
					prims.Add(go);
				else
					nonPrims.Add(go);
			}

			primitives = prims.ToArray();
			nonPrimitives = nonPrims.ToArray();
		}

		private static bool IsPrimitive(GameObject gameObject)
		{
			/*
			 * Primitives have the following properties:
			 * - have no children
			 * - have no non-default local transform properties
			 * - have MeshFilter and MeshRenderer components OR has SkinnedMeshRenderer component
			 */
			return gameObject.transform.childCount == 0
				&& gameObject.transform.localPosition == Vector3.zero
				&& gameObject.transform.localRotation == Quaternion.identity
				&& gameObject.transform.localScale == Vector3.one
				&& ContainsValidRenderer(gameObject);

		}


		private UnityEngine.Mesh GetObjectMesh(GameObject gameObject)
		{
			if(gameObject.GetComponent<MeshFilter>())
			{
				return gameObject.GetComponent<MeshFilter>().sharedMesh;
			}

			SkinnedMeshRenderer skinMesh = gameObject.GetComponent<SkinnedMeshRenderer>();
			if (skinMesh)
			{
				// if(!_exportAnimation && _bakeSkinnedMeshes)
				// {
				// 	if(!_bakedMeshes.ContainsKey(skinMesh))
				// 	{
				// 		UnityEngine.Mesh bakedMesh = new UnityEngine.Mesh();
				// 		skinMesh.BakeMesh(bakedMesh);
				// 		_bakedMeshes.Add(skinMesh, bakedMesh);
				// 	}

				// 	return _bakedMeshes[skinMesh];
				// }

				return skinMesh.sharedMesh;
			}

			SpriteRenderer spriteMesh = gameObject.GetComponent<SpriteRenderer>();
			if(spriteMesh){
				return GetSpriteMesh(gameObject);
			}

			return null;
		}



		private MeshId ExportMesh(string name, GameObject[] primitives)
		{
			// check if this set of primitives is already a mesh
			MeshId existingMeshId = null;
			var key = new PrimKey();
			foreach (var prim in primitives)
			{
				
				key.Mesh = GetObjectMesh(prim);
				key.Material = GetObjectMaterial(prim);

				MeshId tempMeshId;
				if (_primOwner.TryGetValue(key, out tempMeshId) && (existingMeshId == null || tempMeshId == existingMeshId))
				{
					existingMeshId = tempMeshId;
				}
				else
				{
					existingMeshId = null;
					break;
				}
			}

			// if so, return that mesh id
			if (existingMeshId != null)
			{
				return existingMeshId;
			}

			// if not, create new mesh and return its id
			var mesh = new GLTFMesh();

			if (ExportNames)
			{
				mesh.Name = name;
			}

			mesh.Primitives = new List<MeshPrimitive>(primitives.Length);
			foreach (var prim in primitives)
			{
				MeshPrimitive[] meshPrimitives = ExportPrimitive(prim, mesh);
				if (meshPrimitives != null)
				{
					mesh.Primitives.AddRange(meshPrimitives);
				}
			}
			
			// Don't export meshes without primitives, since it's not valid
			if( mesh.Primitives.Count == 0){
				return null;
			}

			var id = new MeshId
			{
				Id = _root.Meshes.Count,
				Root = _root
			};
			_root.Meshes.Add(mesh);

			return id;
		}

		// a mesh *might* decode to multiple prims if there are submeshes
		private MeshPrimitive[] ExportPrimitive(GameObject gameObject, GLTFMesh mesh)
		{
			Mesh meshObj = GetObjectMesh(gameObject);

			if (meshObj == null)
			{
				Debug.LogError(string.Format("MeshFilter.sharedMesh on gameobject:{0} is missing , skipping", gameObject.name));
				return null;
			}
      
			var materialsObj = GetObjectMaterials(gameObject);

			var prims = new MeshPrimitive[meshObj.subMeshCount];

			// don't export any more accessors if this mesh is already exported
			MeshPrimitive[] primVariations;
			if (_meshToPrims.TryGetValue(meshObj, out primVariations)
				&& meshObj.subMeshCount == primVariations.Length)
			{
				for (var i = 0; i < primVariations.Length; i++)
				{
					prims[i] = new MeshPrimitive(primVariations[i], _root)
					{
						Material = ExportMaterial(materialsObj[i])
					};
				}

				return prims;
			}

			AccessorId aPosition = null, aNormal = null, aTangent = null,
				aTexcoord0 = null, aTexcoord1 = null, aColor0 = null,
				aWeights0 = null, aJoints0 = null;
				
			aPosition = ExportAccessor(SchemaExtensions.ConvertVector3CoordinateSpaceAndCopy(meshObj.vertices, SchemaExtensions.CoordinateSpaceConversionScale));

			if (meshObj.normals.Length != 0)
				aNormal = ExportAccessor(SchemaExtensions.ConvertVector3CoordinateSpaceAndCopy(meshObj.normals, SchemaExtensions.CoordinateSpaceConversionScale));

			if (meshObj.tangents.Length != 0)
				aTangent = ExportAccessor(SchemaExtensions.ConvertVector4CoordinateSpaceAndCopy(meshObj.tangents, SchemaExtensions.TangentSpaceConversionScale));

			if (meshObj.uv.Length != 0)
				aTexcoord0 = ExportAccessor(SchemaExtensions.FlipTexCoordArrayVAndCopy(meshObj.uv));

			if (meshObj.uv2.Length != 0)
				aTexcoord1 = ExportAccessor(SchemaExtensions.FlipTexCoordArrayVAndCopy(meshObj.uv2));

			if (meshObj.colors.Length != 0)
				aColor0 = ExportAccessor(meshObj.colors);

			if( meshObj.boneWeights.Length != 0 ){
				ExtractSkinDatasFromBoneWeights( meshObj.boneWeights, out Vector4[]  joints, out Vector4[] weights );
				aJoints0  = ExportAccessorUint(joints);
				aWeights0 = ExportAccessor(weights);
			}

			MaterialId lastMaterialId = null;

			for (var submesh = 0; submesh < meshObj.subMeshCount; submesh++)
			{
				var primitive = new MeshPrimitive();

				var topology = meshObj.GetTopology(submesh);
				var indices = meshObj.GetIndices(submesh);
				if (topology == MeshTopology.Triangles) SchemaExtensions.FlipTriangleFaces(indices);

				primitive.Mode = GetDrawMode(topology);
				primitive.Indices = ExportAccessor(indices, true);

				primitive.Attributes = new Dictionary<string, AccessorId>();
				primitive.Attributes.Add(SemanticProperties.POSITION, aPosition);

				if (aNormal != null)
					primitive.Attributes.Add(SemanticProperties.NORMAL, aNormal);
				if (aTangent != null)
					primitive.Attributes.Add(SemanticProperties.TANGENT, aTangent);
				if (aTexcoord0 != null)
					primitive.Attributes.Add(SemanticProperties.TEXCOORD_0, aTexcoord0);
				if (aTexcoord1 != null)
					primitive.Attributes.Add(SemanticProperties.TEXCOORD_1, aTexcoord1);
				if (aColor0 != null)
					primitive.Attributes.Add(SemanticProperties.COLOR_0, aColor0);
				if (aJoints0 != null)
					primitive.Attributes.Add(SemanticProperties.JOINTS_0, aJoints0);
				if (aWeights0 != null)
					primitive.Attributes.Add(SemanticProperties.WEIGHTS_0, aWeights0);

				if (submesh < materialsObj.Length)
				{
					primitive.Material = ExportMaterial(materialsObj[submesh]);
					lastMaterialId = primitive.Material;
				}
				else
				{
					primitive.Material = lastMaterialId;
				}

				ExportBlendShapes( meshObj, primitive, mesh);

				prims[submesh] = primitive;
			}

			_meshToPrims[meshObj] = prims;

			return prims;
		}

		private void ExtractSkinDatasFromBoneWeights(BoneWeight[] bw, out Vector4[] bones, out Vector4[] weights)
		{
			bones = new Vector4[bw.Length];
			weights = new Vector4[bw.Length];
			for (int i = 0; i < bw.Length; ++i)
			{
				var b = bw[i];
				bones[i]   = new Vector4(b.boneIndex0, b.boneIndex1, b.boneIndex2, b.boneIndex3);
				weights[i] = new Vector4(b.weight0, b.weight1, b.weight2, b.weight3);
			}

		}

		private void ExportSkin(Transform transform)
		{

			PrimKey key = new PrimKey();
			UnityEngine.Mesh mesh = GetObjectMesh(transform.gameObject);
			key.Mesh = mesh;
			key.Material = GetObjectMaterial(transform.gameObject);
			MeshId val;
			if(!_primOwner.TryGetValue(key, out val))
			{
				Debug.Log("No mesh found for skin");
				return;
			}
			SkinnedMeshRenderer skin = transform.GetComponent<SkinnedMeshRenderer>();
			GLTF.Schema.Skin gltfSkin = new Skin();
      gltfSkin.Joints = new List<NodeId>(skin.bones.Length);

			for (int i = 0; i < skin.bones.Length; ++i)
			{
				gltfSkin.Joints.Add(
          new NodeId( _nodesByInstanceId[skin.bones[i].GetInstanceID()], _root )
        );
			}

			gltfSkin.InverseBindMatrices = ExportAccessor(mesh.bindposes);

			var skinnedNode = _root.Nodes[_nodesByInstanceId[transform.GetInstanceID()].Id];
			skinnedNode.Skin = new SkinId() { Id = _root.Skins.Count, Root = _root };
			_root.Skins.Add(gltfSkin);
		}

		// Blend Shapes / Morph Targets
		// Adopted from Gary Hsu (bghgary)
		// https://github.com/bghgary/glTF-Tools-for-Unity/blob/master/UnityProject/Assets/Gltf/Editor/Exporter.cs
		private void ExportBlendShapes(Mesh meshObj, MeshPrimitive primitive, GLTFMesh mesh)
		{
			if (meshObj.blendShapeCount > 0)
			{
				List<Dictionary<string, AccessorId>> targets = new List<Dictionary<string, AccessorId>>(meshObj.blendShapeCount);
				List<Double> weights = new List<double>(meshObj.blendShapeCount);
				List<string> targetNames = new List<string>(meshObj.blendShapeCount);

				for (int blendShapeIndex = 0; blendShapeIndex < meshObj.blendShapeCount; blendShapeIndex++)
				{

					targetNames.Add(meshObj.GetBlendShapeName(blendShapeIndex));
					// As described above, a blend shape can have multiple frames.  Given that glTF only supports a single frame
					// per blend shape, we'll always use the final frame (the one that would be for when 100% weight is applied).
					int frameIndex = meshObj.GetBlendShapeFrameCount(blendShapeIndex) - 1;

					var deltaVertices = new Vector3[meshObj.vertexCount];
					var deltaNormals = new Vector3[meshObj.vertexCount];
					var deltaTangents = new Vector3[meshObj.vertexCount];
					meshObj.GetBlendShapeFrameVertices(blendShapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);

					targets.Add(new Dictionary<string, AccessorId>
						{
							{ SemanticProperties.POSITION, ExportAccessor(SchemaExtensions.ConvertVector3CoordinateSpaceAndCopy( deltaVertices, SchemaExtensions.CoordinateSpaceConversionScale)) },
							{ SemanticProperties.NORMAL, ExportAccessor(SchemaExtensions.ConvertVector3CoordinateSpaceAndCopy(deltaNormals,SchemaExtensions.CoordinateSpaceConversionScale))},
							{ SemanticProperties.TANGENT, ExportAccessor(SchemaExtensions.ConvertVector3CoordinateSpaceAndCopy(deltaTangents, SchemaExtensions.CoordinateSpaceConversionScale)) },
						});

					// We need to get the weight from the SkinnedMeshRenderer because this represents the currently
					// defined weight by the user to apply to this blend shape.  If we instead got the value from
					// the unityMesh, it would be a _per frame_ weight, and for a single-frame blend shape, that would
					// always be 100.  A blend shape might have more than one frame if a user wanted to more tightly
					// control how a blend shape will be animated during weight changes (e.g. maybe they want changes
					// between 0-50% to be really minor, but between 50-100 to be extreme, hence they'd have two frames
					// where the first frame would have a weight of 50 (meaning any weight between 0-50 should be relative
					// to the values in this frame) and then any weight between 50-100 would be relevant to the weights in
					// the second frame.  See Post 20 for more info:
					// https://forum.unity3d.com/threads/is-there-some-method-to-add-blendshape-in-editor.298002/#post-2015679

          // the weight info comming from SkinnedMeshRenderer should be set on Node.weights instead of Mesh.weights
          // there is no such Mesh.weights data in unity so it will always be all 0

					// weights.Add(smr.GetBlendShapeWeight(blendShapeIndex) / 100);
					weights.Add(0.0);
				}

				mesh.Weights = weights;
				primitive.Targets = targets;
				primitive.TargetNames = targetNames;
			}
		}

	}
}