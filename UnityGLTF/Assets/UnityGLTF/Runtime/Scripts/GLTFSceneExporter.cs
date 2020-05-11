
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GLTF.Schema;
using GLTF.Schema.KHR_lights_punctual;
using UnityEngine;
using UnityEngine.Rendering;
using UnityGLTF.Extensions;
using CameraType = GLTF.Schema.CameraType;
using WrapMode = GLTF.Schema.WrapMode;


namespace UnityGLTF
{

	[Serializable]
	public class MeshExportOptions
	{

		public bool exportNormals = true;
		public bool exportTangents = true;
		public bool exportUv0 = true;
		public bool exportUv1 = true;
		public bool exportUv2 = true;
		public bool exportUv3 = true;
		public bool exportColor = true;

	}

	public class ExportOptions
	{
		public GLTFSceneExporter.RetrieveTexturePathDelegate TexturePathRetriever = (texture) => texture.name;
		public bool ExportInactivePrimitives = true;

		public bool exportGlb = false;
		public bool embedTextures = false;
	}

	public partial class GLTFSceneExporter
	{
		public delegate string RetrieveTexturePathDelegate(Texture texture);



		private Transform[] _rootTransforms;
		private GLTFRoot _root;
		private BufferId _bufferId;
		private GLTFBuffer _buffer;
		private BinaryWriter _bufferWriter;
		private List<ImageInfo> _imageInfos;
		private List<Texture> _textures;
		private List<Material> _materials;
		private Dictionary<int, NodeId> _nodesByInstanceId;
		private List<Transform> _skinnedNodes;
		private List<Transform> _animatedNodes;
		private bool _shouldUseInternalBufferForImages;

		private ExportOptions _exportOptions;

		private Material _metalGlossChannelSwapMaterial;
		private Material _normalChannelMaterial;

		private const uint MagicGLTF = 0x46546C67;
		private const uint Version = 2;
		private const uint MagicJson = 0x4E4F534A;
		private const uint MagicBin = 0x004E4942;
		private const int GLTFHeaderSize = 12;
		private const int SectionHeaderSize = 8;

		protected struct PrimKey
		{
			public Mesh Mesh;
			public Material Material;
		}
		private readonly Dictionary<PrimKey, MeshId> _primOwner = new Dictionary<PrimKey, MeshId>();
		private readonly Dictionary<Mesh, MeshPrimitive[]> _meshToPrims = new Dictionary<Mesh, MeshPrimitive[]>();

		// Settings
		public static bool ExportNames = true;
		public static bool ExportFullPath = true;

		/// <summary>
		/// Create a GLTFExporter that exports out a transform
		/// </summary>
		/// <param name="rootTransforms">Root transform of object to export</param>
		[Obsolete("Please switch to GLTFSceneExporter(Transform[] rootTransforms, ExportOptions options).  This constructor is deprecated and will be removed in a future release.")]
		public GLTFSceneExporter(Transform[] rootTransforms, RetrieveTexturePathDelegate texturePathRetriever)
			: this(rootTransforms, new ExportOptions { TexturePathRetriever = texturePathRetriever })
		{
		}

		/// <summary>
		/// Create a GLTFExporter that exports out a transform
		/// </summary>
		/// <param name="rootTransforms">Root transform of object to export</param>
		public GLTFSceneExporter(Transform[] rootTransforms, ExportOptions options)
		{
			_exportOptions = options;

			var metalGlossChannelSwapShader = Resources.Load("MetalGlossChannelSwap", typeof(Shader)) as Shader;
			_metalGlossChannelSwapMaterial = new Material(metalGlossChannelSwapShader);

			var normalChannelShader = Resources.Load("NormalChannel", typeof(Shader)) as Shader;
			_normalChannelMaterial = new Material(normalChannelShader);

			_rootTransforms = rootTransforms;
			_root = new GLTFRoot
			{
				Accessors = new List<Accessor>(),
				Animations = new List<GLTFAnimation>(),
				Asset = new Asset
				{
					Version = "2.0",
					Generator = "UnityGLTF (" + Application.unityVersion + ")"
				},
				Buffers = new List<GLTFBuffer>(),
				BufferViews = new List<BufferView>(),
				Cameras = new List<GLTFCamera>(),
				Images = new List<GLTFImage>(),
				Materials = new List<GLTFMaterial>(),
				Meshes = new List<GLTFMesh>(),
				Nodes = new List<Node>(),
				Samplers = new List<Sampler>(),
				Scenes = new List<GLTFScene>(),
				Skins = new List<Skin>(),
				Textures = new List<GLTFTexture>()
			};

			_imageInfos = new List<ImageInfo>();
			_materials = new List<Material>();
			_textures = new List<Texture>();
			_skinnedNodes = new List<Transform>();
			_animatedNodes = new List<Transform>();
			_nodesByInstanceId = new Dictionary<int, NodeId>();

			_buffer = new GLTFBuffer();
			_bufferId = new BufferId
			{
				Id = _root.Buffers.Count,
				Root = _root
			};
			_root.Buffers.Add(_buffer);
		}

		/// <summary>
		/// Gets the root object of the exported GLTF
		/// </summary>
		/// <returns>Root parsed GLTF Json</returns>
		public GLTFRoot GetRoot()
		{
			return _root;
		}

		/// <summary>
		/// Writes a binary GLB file with filename at path.
		/// </summary>
		/// <param name="path">File path for saving the binary file</param>
		/// <param name="fileName">The name of the GLTF file</param>
		public void SaveGLB(string path, string fileName)
		{
			_shouldUseInternalBufferForImages = true;
			string fullPath = Path.Combine(path, Path.ChangeExtension(fileName, "glb"));
			
			using (FileStream glbFile = new FileStream(fullPath, FileMode.Create))
			{
				SaveGLBToStream(glbFile, fileName);
			}

			if (!_shouldUseInternalBufferForImages)
			{
				ExportImages(path);
			}
		}

		/// <summary>
		/// In-memory GLB creation helper. Useful for platforms where no filesystem is available (e.g. WebGL).
		/// </summary>
		/// <param name="sceneName"></param>
		/// <returns></returns>
		public byte[] SaveGLBToByteArray(string sceneName)
		{
			using (var stream = new MemoryStream())
			{
				SaveGLBToStream(stream, sceneName);
				return stream.ToArray();
			}
		}

		/// <summary>
		/// Writes a binary GLB file into a stream (memory stream, filestream, ...)
		/// </summary>
		/// <param name="path">File path for saving the binary file</param>
		/// <param name="fileName">The name of the GLTF file</param>
		public void SaveGLBToStream(Stream stream, string sceneName)
		{
			Stream binStream = new MemoryStream();
			Stream jsonStream = new MemoryStream();

			_bufferWriter = new BinaryWriter(binStream);

			TextWriter jsonWriter = new StreamWriter(jsonStream, Encoding.ASCII);

			_root.Scene = ExportScene(sceneName, _rootTransforms);

			_buffer.ByteLength = CalculateAlignment((uint)_bufferWriter.BaseStream.Length, 4);

			_root.Serialize(jsonWriter, true);

			_bufferWriter.Flush();
			jsonWriter.Flush();

			// align to 4-byte boundary to comply with spec.
			AlignToBoundary(jsonStream);
			AlignToBoundary(binStream, 0x00);

			int glbLength = (int)(GLTFHeaderSize + SectionHeaderSize +
				jsonStream.Length + SectionHeaderSize + binStream.Length);

			BinaryWriter writer = new BinaryWriter(stream);

			// write header
			writer.Write(MagicGLTF);
			writer.Write(Version);
			writer.Write(glbLength);

			// write JSON chunk header.
			writer.Write((int)jsonStream.Length);
			writer.Write(MagicJson);

			jsonStream.Position = 0;
			CopyStream(jsonStream, writer);

			writer.Write((int)binStream.Length);
			writer.Write(MagicBin);

			binStream.Position = 0;
			CopyStream(binStream, writer);

			writer.Flush();
		}

		/// <summary>
		/// Convenience function to copy from a stream to a binary writer, for
		/// compatibility with pre-.NET 4.0.
		/// Note: Does not set position/seek in either stream. After executing,
		/// the input buffer's position should be the end of the stream.
		/// </summary>
		/// <param name="input">Stream to copy from</param>
		/// <param name="output">Stream to copy to.</param>
		private static void CopyStream(Stream input, BinaryWriter output)
		{
			byte[] buffer = new byte[8 * 1024];
			int length;
			while ((length = input.Read(buffer, 0, buffer.Length)) > 0)
			{
				output.Write(buffer, 0, length);
			}
		}

		/// <summary>
		/// Pads a stream with additional bytes.
		/// </summary>
		/// <param name="stream">The stream to be modified.</param>
		/// <param name="pad">The padding byte to append. Defaults to ASCII
		/// space (' ').</param>
		/// <param name="boundary">The boundary to align with, in bytes.
		/// </param>
		private static void AlignToBoundary(Stream stream, byte pad = (byte)' ', uint boundary = 4)
		{
			uint currentLength = (uint)stream.Length;
			uint newLength = CalculateAlignment(currentLength, boundary);
			for (int i = 0; i < newLength - currentLength; i++)
			{
				stream.WriteByte(pad);
			}
		}

		/// <summary>
		/// Calculates the number of bytes of padding required to align the
		/// size of a buffer with some multiple of byteAllignment.
		/// </summary>
		/// <param name="currentSize">The current size of the buffer.</param>
		/// <param name="byteAlignment">The number of bytes to align with.</param>
		/// <returns></returns>
		public static uint CalculateAlignment(uint currentSize, uint byteAlignment)
		{
			return (currentSize + byteAlignment - 1) / byteAlignment * byteAlignment;
		}


		/// <summary>
		/// Specifies the path and filename for the GLTF Json and binary
		/// </summary>
		/// <param name="path">File path for saving the GLTF and binary files</param>
		/// <param name="fileName">The name of the GLTF file</param>
		public void SaveGLTFandBin(string path, string fileName)
		{
			_shouldUseInternalBufferForImages = false;
			var binFile = File.Create(Path.Combine(path, fileName + ".bin"));
			_bufferWriter = new BinaryWriter(binFile);

			_root.Scene = ExportScene(fileName, _rootTransforms);
			AlignToBoundary(_bufferWriter.BaseStream, 0x00);
			_buffer.Uri = fileName + ".bin";
			_buffer.ByteLength = CalculateAlignment((uint)_bufferWriter.BaseStream.Length, 4);

			var gltfFile = File.CreateText(Path.Combine(path, fileName + ".gltf"));
			_root.Serialize(gltfFile);

#if WINDOWS_UWP
			gltfFile.Dispose();
			binFile.Dispose();
#else
			gltfFile.Close();
			binFile.Close();
#endif
			ExportImages(path);

		}

		private void DeclareExtensionUsage(string extension, bool isRequired=false)
		{
			if( _root.ExtensionsUsed == null ){
				_root.ExtensionsUsed = new List<string>();
			}
			if(!_root.ExtensionsUsed.Contains(extension))
			{
				_root.ExtensionsUsed.Add(extension);
			}

			if(isRequired){

				if( _root.ExtensionsRequired == null ){
					_root.ExtensionsRequired = new List<string>();
				}
				if( !_root.ExtensionsRequired.Contains(extension))
				{
					_root.ExtensionsRequired.Add(extension);
				}
			}
		}

		private SceneId ExportScene(string name, Transform[] rootObjTransforms)
		{
			var scene = new GLTFScene();

			if (ExportNames)
			{
				scene.Name = name;
			}

			scene.Nodes = new List<NodeId>(rootObjTransforms.Length);
			foreach (var transform in rootObjTransforms)
			{
        NodeId nodeid = ExportNode(transform);
				if( nodeid != null ) {
          scene.Nodes.Add( nodeid );
        }
			}


			ExportAnimation();
			
			for (int i = 0; i < _skinnedNodes.Count; ++i)
			{
				ExportSkin(_skinnedNodes[i]);
			}

			_root.Scenes.Add(scene);

			return new SceneId
			{
				Id = _root.Scenes.Count - 1,
				Root = _root
			};
		}

		private bool IsEnabledNode( Transform nodeTransform ){
			return nodeTransform.gameObject.activeSelf;
		}

		private bool IsAnimatedNode( Transform nodeTransform ){
			return (nodeTransform.GetComponent<UnityEngine.Animation>() || nodeTransform.GetComponent<UnityEngine.Animator>());
		}

		private bool IsSkinnedNode( Transform nodeTransform ){
			var smr = nodeTransform.GetComponent<SkinnedMeshRenderer>();
			return( smr != null && smr.rootBone != null );
		}
	
		private bool IsLightNode( Transform nodeTransform ){
			var light = nodeTransform.GetComponent<Light>();
			return( light != null && light.enabled && (
				light.type == UnityEngine.LightType.Directional ||
				light.type == UnityEngine.LightType.Spot ||
				light.type == UnityEngine.LightType.Point
			));
		}

		private NodeId ExportNode(Transform nodeTransform)
		{

      if( ! IsEnabledNode(nodeTransform) ) return null;

			var node = new Node();

			if (ExportNames)
			{
				node.Name = nodeTransform.name;
			}
			
			if(IsAnimatedNode(nodeTransform))
			{
				_animatedNodes.Add(nodeTransform);
			}

			if(IsSkinnedNode(nodeTransform))
			{
				  _skinnedNodes.Add(nodeTransform);
			}

			if( IsLightNode( nodeTransform) ){
        node.AddChild( CreateLightNode( nodeTransform.GetComponent<Light>() ) );
			}

			// export camera attached to node
  		// Create additional sub node to flip camera Z
			Camera unityCamera = nodeTransform.GetComponent<Camera>();
			if( unityCamera != null ){
        node.AddChild( CreateCameraNode( unityCamera ) );
      }

			// If object is on top of the selection, use global transform
			bool useLocal = !Array.Exists(_rootTransforms, element => element == nodeTransform);
			node.SetUnityTransform(nodeTransform, useLocal);

			var id = new NodeId
			{
				Id = _root.Nodes.Count,
				Root = _root
			};
			_root.Nodes.Add(node);

			_nodesByInstanceId.Add(nodeTransform.GetInstanceID(), id);

			// children that are primitives get put in a mesh
			GameObject[] primitives, nonPrimitives;
			FilterPrimitives(nodeTransform, out primitives, out nonPrimitives);
			if (primitives.Length > 0)
			{
				var mesh = ExportMesh(nodeTransform.name, primitives);
				if( mesh != null ){
					node.Mesh = mesh;

					// associate unity meshes with gltf mesh id
					foreach (var prim in primitives)
					{
						_primOwner[new PrimKey { Mesh = GetObjectMesh(prim), Material = GetObjectMaterial(prim) }] = node.Mesh;
					}
				}
			}




      foreach (var child in nonPrimitives)
      {
        NodeId nodeid = ExportNode(child.transform);
				if( nodeid != null ) {
          node.AddChild( nodeid );
        }
      }

			return id;
		}


		private CameraId ExportCamera(Camera unityCamera)
		{
			GLTFCamera camera = new GLTFCamera();
			//name
			camera.Name = unityCamera.name;

			//type
			bool isOrthographic = unityCamera.orthographic;
			camera.Type = isOrthographic ? CameraType.orthographic : CameraType.perspective;
			Matrix4x4 matrix = unityCamera.projectionMatrix;

			//matrix properties: compute the fields from the projection matrix
			if (isOrthographic)
			{
				CameraOrthographic ortho = new CameraOrthographic();

				ortho.XMag = 1 / matrix[0, 0];
				ortho.YMag = 1 / matrix[1, 1];

				float farClip = (matrix[2, 3] / matrix[2, 2]) - (1 / matrix[2, 2]);
				float nearClip = farClip + (2 / matrix[2, 2]);
				ortho.ZFar = farClip;
				ortho.ZNear = nearClip;

				camera.Orthographic = ortho;
			}
			else
			{
				CameraPerspective perspective = new CameraPerspective();
				float fov = 2 * Mathf.Atan(1 / matrix[1, 1]);
				float aspectRatio = matrix[1, 1] / matrix[0, 0];
				perspective.YFov = fov;
				perspective.AspectRatio = aspectRatio;

				if (matrix[2, 2] == -1)
				{
					//infinite projection matrix
					float nearClip = matrix[2, 3] * -0.5f;
					perspective.ZNear = nearClip;
				}
				else
				{
					//finite projection matrix
					float farClip = matrix[2, 3] / (matrix[2, 2] + 1);
					float nearClip = farClip * (matrix[2, 2] + 1) / (matrix[2, 2] - 1);
					perspective.ZFar = farClip;
					perspective.ZNear = nearClip;
				}
				camera.Perspective = perspective;
			}

			var id = new CameraId
			{
				Id = _root.Cameras.Count,
				Root = _root
			};

			_root.Cameras.Add(camera);

			return id;
		}


		private NodeId CreateCameraNode( Camera unityCamera ){
			Node subNode = new Node();
			var id = new NodeId
			{
				Id = _root.Nodes.Count,
				Root = _root
			};
			subNode.Rotation = new GLTF.Math.Quaternion(0f, 1f, 0f, 0f);
			_root.Nodes.Add(subNode);
			subNode.Camera = ExportCamera(unityCamera);
			return id;
		}

		
		private KHR_LightsPunctualExtension PunctualLightsExtension;

		private GLTF.Schema.KHR_lights_punctual.LightType GetGLTFLightType( Light light ){
			switch( light.type ){
				case UnityEngine.LightType.Directional : return GLTF.Schema.KHR_lights_punctual.LightType.directional;
				case UnityEngine.LightType.Spot        : return GLTF.Schema.KHR_lights_punctual.LightType.spot       ;
				case UnityEngine.LightType.Point       : return GLTF.Schema.KHR_lights_punctual.LightType.point      ;
			}
			throw new Exception( $"Unsupported lighttype {light.type}");
		}


		private NodeId CreateLightNode( Light unityLight ){
			
			if( PunctualLightsExtension == null ){
				PunctualLightsExtension = new KHR_LightsPunctualExtension();
				DeclareExtensionUsage( KHR_lights_punctualExtensionFactory.EXTENSION_NAME, true );
				_root.AddExtension( KHR_lights_punctualExtensionFactory.EXTENSION_NAME, PunctualLightsExtension );
			}


			Node subNode = new Node();
			var id = new NodeId
			{
				Id = _root.Nodes.Count,
				Root = _root
			};
			subNode.Rotation = new GLTF.Math.Quaternion(0f, 1f, 0f, 0f);
			_root.Nodes.Add(subNode);
			
			
			var extEntry = new PunctualLight(){
				Color = unityLight.color.ToNumericsColorLinear(),
				Intensity = unityLight.intensity*unityLight.intensity,
				Name = unityLight.name,
				Range = unityLight.range,
				Type = GetGLTFLightType( unityLight ),
			};

			if( unityLight.type == UnityEngine.LightType.Spot ){
				extEntry.Spot = new Spot(){
					InnerConeAngle = unityLight.innerSpotAngle * Mathf.Deg2Rad * .5f,
					OuterConeAngle = unityLight.spotAngle * Mathf.Deg2Rad * .5f
				};
			}

			var lightId = new PunctualLightId(){
				Id = PunctualLightsExtension.Lights.Count,
				Root = _root
			};

			PunctualLightsExtension.Lights.Add( extEntry );

			var LightRef = new KHR_LightsPunctualNodeExtension(){
				LightId = lightId
			};

			subNode.AddExtension( ExtTextureTransformExtensionFactory.EXTENSION_NAME, LightRef );
			return id;
		}


		private UnityEngine.Material GetObjectMaterial(GameObject gameObject)
		{
			if (gameObject.GetComponent<MeshRenderer>())
			{
				return gameObject.GetComponent<MeshRenderer>().sharedMaterial;
			}

			if (gameObject.GetComponent<SkinnedMeshRenderer>())
			{
				return gameObject.GetComponent<SkinnedMeshRenderer>().sharedMaterial;
			}

			return null;
		}

		private UnityEngine.Material[] GetObjectMaterials(GameObject gameObject)
		{
			if (gameObject.GetComponent<MeshRenderer>())
			{
				return gameObject.GetComponent<MeshRenderer>().sharedMaterials;
			}

			if (gameObject.GetComponent<SkinnedMeshRenderer>())
			{
				return gameObject.GetComponent<SkinnedMeshRenderer>().sharedMaterials;
			}

			return null;
		}


		private long AppendToBufferMultiplyOf4(long byteOffset, long byteLength)
		{
			
		    var moduloOffset = byteLength % 4;
		    if (moduloOffset > 0)
		    {
			for (int i = 0; i < (4 - moduloOffset); i++)
			{
			    _bufferWriter.Write((byte)0x00);
			}
			byteLength = _bufferWriter.BaseStream.Position - byteOffset;
		    }

		    return byteLength;
		}


		private BufferViewId ExportBufferView(uint byteOffset, uint byteLength)
		{
			var bufferView = new BufferView
			{
				Buffer = _bufferId,
				ByteOffset = byteOffset,
				ByteLength = byteLength
			};

			var id = new BufferViewId
			{
				Id = _root.BufferViews.Count,
				Root = _root
			};

			_root.BufferViews.Add(bufferView);

			return id;
		}

		public MaterialId GetMaterialId(GLTFRoot root, Material materialObj)
		{
			for (var i = 0; i < _materials.Count; i++)
			{
				if (_materials[i] == materialObj)
				{
					return new MaterialId
					{
						Id = i,
						Root = root
					};
				}
			}

			return null;
		}

		public TextureId GetTextureId(GLTFRoot root, Texture textureObj)
		{
			for (var i = 0; i < _textures.Count; i++)
			{
				if (_textures[i] == textureObj)
				{
					return new TextureId
					{
						Id = i,
						Root = root
					};
				}
			}

			return null;
		}

		public ImageId GetImageId(GLTFRoot root, Texture imageObj)
		{
			for (var i = 0; i < _imageInfos.Count; i++)
			{
				if (_imageInfos[i].texture == imageObj)
				{
					return new ImageId
					{
						Id = i,
						Root = root
					};
				}
			}

			return null;
		}

		public SamplerId GetSamplerId(GLTFRoot root, Texture textureObj)
		{
			for (var i = 0; i < root.Samplers.Count; i++)
			{
				bool filterIsNearest = root.Samplers[i].MinFilter == MinFilterMode.Nearest
					|| root.Samplers[i].MinFilter == MinFilterMode.NearestMipmapNearest
					|| root.Samplers[i].MinFilter == MinFilterMode.NearestMipmapLinear;

				bool filterIsLinear = root.Samplers[i].MinFilter == MinFilterMode.Linear
					|| root.Samplers[i].MinFilter == MinFilterMode.LinearMipmapNearest;

				bool filterMatched = textureObj.filterMode == FilterMode.Point && filterIsNearest
					|| textureObj.filterMode == FilterMode.Bilinear && filterIsLinear
					|| textureObj.filterMode == FilterMode.Trilinear && root.Samplers[i].MinFilter == MinFilterMode.LinearMipmapLinear;

				bool wrapMatched = textureObj.wrapMode == TextureWrapMode.Clamp && root.Samplers[i].WrapS == WrapMode.ClampToEdge
					|| textureObj.wrapMode == TextureWrapMode.Repeat && root.Samplers[i].WrapS == WrapMode.Repeat
					|| textureObj.wrapMode == TextureWrapMode.Mirror && root.Samplers[i].WrapS == WrapMode.MirroredRepeat;

				if (filterMatched && wrapMatched)
				{
					return new SamplerId
					{
						Id = i,
						Root = root
					};
				}
			}

			return null;
		}

		protected static DrawMode GetDrawMode(MeshTopology topology)
		{
			switch (topology)
			{
				case MeshTopology.Points: return DrawMode.Points;
				case MeshTopology.Lines: return DrawMode.Lines;
				case MeshTopology.LineStrip: return DrawMode.LineStrip;
				case MeshTopology.Triangles: return DrawMode.Triangles;
			}

			throw new Exception("glTF does not support Unity mesh topology: " + topology);
		}
	}
}
