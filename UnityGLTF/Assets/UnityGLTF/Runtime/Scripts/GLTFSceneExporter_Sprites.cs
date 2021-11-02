using System;
using System.Collections.Generic;
using GLTF.Schema;
using UnityEngine;
// using UnityEngine.U2D.Animation;
using UnityEngine.U2D;
using System.Reflection;
using UnityEditor;
using UnityEditor.Sprites;
using SpriteAccess = UnityEngine.U2D.SpriteDataAccessExtensions;
using Newtonsoft.Json.Linq;
using UnityEngine.U2D.Animation;

namespace UnityGLTF
{
  public partial class GLTFSceneExporter
  {

    private Dictionary<int, Mesh> _spriteMeshes;
    private Dictionary<int, Material> _spriteMaterials;
    private Dictionary<int, Material> _spriteMaterialsCache;
    private List<SpriteAtlas> _spriteAtlases;
    private bool _atlasWarned = false;
    private List<SpritesheetAnimation.Animation> _spritesheetAnimations;


    private void InitializeSpriteRegistries()
    {

      _spriteMeshes = new Dictionary<int, Mesh>();
      _spriteMaterials = new Dictionary<int, Material>();
      _spriteMaterialsCache = new Dictionary<int, Material>();

      _spriteAtlases = new List<SpriteAtlas>();
      _spritesheetAnimations = new List<SpritesheetAnimation.Animation>();

      string[] allAtlases = AssetDatabase.FindAssets("t:SpriteAtlas");
      for (int i = 0; i < allAtlases.Length; i++)
      {
        string atlasName = allAtlases[i];
        string path = AssetDatabase.GUIDToAssetPath(atlasName);
        _spriteAtlases.Add((SpriteAtlas)AssetDatabase.LoadAssetAtPath(path, typeof(SpriteAtlas)));
      }

    }

    private void SetLayeredNode(ref Node node, Transform nodeTransform)
    {
      SpriteRenderer renderer = nodeTransform.GetComponent<SpriteRenderer>();
      if (!renderer)
        return;

      JObject extra = node.Extras as JObject;
      if (extra == null)
      {
        extra = new JObject();
        node.Extras = extra;
      }

      extra.Add("renderOrder", renderer.sortingOrder);
      extra.Add("renderLayer", renderer.sortingLayerName);

    }

    private Mesh GetSpriteMesh(GameObject gameObject)
    {

      SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();

      if (renderer == null)
        return null;

      Sprite sprite = renderer.sprite;

      // Atlas check
      bool inAtlas = _spriteAtlases.Find((v) => v.GetSprite(sprite.name) != null) != null;
      if (inAtlas && !Application.isPlaying && !_atlasWarned)
      {
        Debug.LogWarning("[Sprite] Careful if you're using atlas you need to export object during play mode to be effective");
        _atlasWarned = true;
      }

      Mesh mesh;
      if (_spriteMeshes.TryGetValue(sprite.GetHashCode(), out mesh))
      {
        return mesh;
      }

      mesh = new Mesh();


      bool hasPosition = SpriteAccess.HasVertexAttribute(renderer.sprite, UnityEngine.Rendering.VertexAttribute.Position);
      bool hasNormals = SpriteAccess.HasVertexAttribute(renderer.sprite, UnityEngine.Rendering.VertexAttribute.Normal);
      bool hasTangents = SpriteAccess.HasVertexAttribute(renderer.sprite, UnityEngine.Rendering.VertexAttribute.Tangent);
      bool hasBlendWeights = SpriteAccess.HasVertexAttribute(renderer.sprite, UnityEngine.Rendering.VertexAttribute.BlendWeight);
      hasBlendWeights = hasBlendWeights && gameObject.GetComponent<SpriteSkin>();

      var vertices = SpriteAccess.GetVertexAttribute<Vector3>(renderer.sprite, UnityEngine.Rendering.VertexAttribute.Position).ToArray();
      var tangents = SpriteAccess.GetVertexAttribute<Vector4>(renderer.sprite, UnityEngine.Rendering.VertexAttribute.Tangent).ToArray();
      var normals = SpriteAccess.GetVertexAttribute<Vector3>(renderer.sprite, UnityEngine.Rendering.VertexAttribute.Normal).ToArray();
      var boneWeights = SpriteAccess.GetVertexAttribute<BoneWeight>(renderer.sprite, UnityEngine.Rendering.VertexAttribute.BlendWeight).ToArray();

      var triangles = sprite.triangles;
      var uvs = sprite.uv;

      // Skinned
      Matrix4x4[] poses = SpriteAccess.GetBindPoses(sprite).ToArray();
      bool isSkinned = poses.Length != 0;
      if (isSkinned)
      {
        mesh.bindposes = poses;
      }

      if (!isSkinned)
      {
        // Convert coordinate space
        Quaternion rotation = Quaternion.Euler(0.0f, 180f, 0.0f);
        for (var i = 0; i < vertices.Length; i++)
        {
          // vertices[i] = rotation * vertices[i];
          vertices[i].x = -vertices[i].x;
        }
      }
      mesh.vertices = vertices;

      if (hasBlendWeights)
        mesh.boneWeights = boneWeights;

      mesh.SetUVs(0, uvs);
      mesh.SetTriangles(triangles, 0);
      mesh.OptimizeIndexBuffers();
      mesh.OptimizeReorderVertexBuffer();

      Material material;
      Texture2D spriteTex = sprite.texture;
      if (!_spriteMaterialsCache.TryGetValue(spriteTex.GetHashCode(), out material))
      {
        material = new Material(renderer.sharedMaterial.shader);
        material.name = renderer.sharedMaterial.name;
        material.CopyPropertiesFromMaterial(renderer.sharedMaterial);

        int w = NearestPowerOf2(spriteTex.width);
        int h = NearestPowerOf2(spriteTex.height);
        var destRenderTexture = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(spriteTex, destRenderTexture);

        var exportTexture = new Texture2D(w, h);
        exportTexture.wrapMode = spriteTex.wrapMode;
        exportTexture.filterMode = spriteTex.filterMode;
        exportTexture.name = _exportOptions.TexturePathRetriever(spriteTex);
        exportTexture.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        exportTexture.Apply();
        material.SetTexture("_MainTex", spriteTex);
        _spriteMaterialsCache[spriteTex.GetHashCode()] = material;
      }

      _spriteMaterials[sprite.GetHashCode()] = material;
      _spriteMeshes[sprite.GetHashCode()] = mesh;

      return mesh;

    }

    bool IsPowerOf2(int n)
    {
      return (n != 0 && (n & (n - 1)) == 0);
    }

    int NearestPowerOf2(int n)
    {
      if (IsPowerOf2(n)) return n;
      if (n % 2 == 1) n++;
      float fn = (float)n;
      return (int)Math.Pow(2.0, Math.Round(Math.Log(fn) / Math.Log(2.0)));
    }

    private void ExportSkinSprite(Transform transform)
    {

      PrimKey key = new PrimKey();
      UnityEngine.Mesh mesh = GetObjectMesh(transform.gameObject);
      key.Mesh = mesh;
      key.Material = GetObjectMaterial(transform.gameObject);
      MeshId val;
      if (!_primOwner.TryGetValue(key, out val))
      {
        Debug.Log("No mesh found for skin");
        return;
      }
      UnityEngine.U2D.Animation.SpriteSkin skin = transform.GetComponent<UnityEngine.U2D.Animation.SpriteSkin>();
      GLTF.Schema.Skin gltfSkin = new Skin();
      gltfSkin.Joints = new List<NodeId>(skin.boneTransforms.Length);

      for (int i = 0; i < skin.boneTransforms.Length; ++i)
      {
        gltfSkin.Joints.Add(
          new NodeId(_nodesByInstanceId[skin.boneTransforms[i].GetInstanceID()], _root)
        );
      }

      gltfSkin.InverseBindMatrices = ExportAccessor(mesh.bindposes);

      var skinnedNode = _root.Nodes[_nodesByInstanceId[transform.GetInstanceID()].Id];
      skinnedNode.Skin = new SkinId() { Id = _root.Skins.Count, Root = _root };
      _root.Skins.Add(gltfSkin);


    }

    private void ExportSpritesheetAnimation(Node animRoot, GameObject primitive)
    {

      var spriteanim = primitive.GetComponent<SpritesheetAnimation.SpritesheetAnimationBinding>();
      if (spriteanim == null)
        return;

      const string SCENE_EXTENSION_PROP = "spritesheetAnimations";
      const string NODE_EXTENSION_PROP = "spritesheetAnimations";

      spriteanim.Bind();

      string controllername = spriteanim.GetControllerName();
      string filename = controllername + "-animations.json";
      var animation = _spritesheetAnimations.Find((v) => v.name == controllername);

      JObject sceneExtra = _root.Extras as JObject;
      if (sceneExtra == null)
      {
        sceneExtra = new JObject();
        _root.Extras = sceneExtra;
      }

      JArray array = sceneExtra.Value<JArray>(SCENE_EXTENSION_PROP);
      if (array == null)
      {
        array = new JArray();
        sceneExtra.Add(SCENE_EXTENSION_PROP, array);
      }

      JObject nodeExtra = animRoot.Extras as JObject;
      if (nodeExtra == null)
      {
        nodeExtra = new JObject();
        animRoot.Extras = nodeExtra;
      }

      var animlist = array.ToObject<List<string>>();
      int idx = animlist.Count;

      if (animation != null)
      {
        idx = animlist.IndexOf(filename);
        nodeExtra.Add(NODE_EXTENSION_PROP, idx);
        return;
      }

      animation = spriteanim.CreateAnimation();

      _spritesheetAnimations.Add(animation);
      array.Add(filename);
      nodeExtra.Add(NODE_EXTENSION_PROP, idx);

      AnimationClip[] clips = spriteanim.GetClips();
      for (int i = 0; i < clips.Length; i++)
      {

        var track = spriteanim.Init(clips[i]);
        animation.tracks.Add(track);

        for (int frameIdx = 0; frameIdx < track.keyframes.Count; frameIdx++)
        {
          spriteanim.Sample(frameIdx);
          MeshId id = ExportSpriteMeshDirect(primitive, spriteanim.GetCurrentSpriteName());
          var info = track.keyframes[frameIdx];
          info.meshId = id.Id;
          track.keyframes[frameIdx] = info;
        }

        spriteanim.Finish();

      }

    }

    private MeshId ExportSpriteMeshDirect(GameObject tgt, string name = null)
    {

      MeshId existingMeshId = null;
      var key = new PrimKey();
      key.Mesh = GetObjectMesh(tgt);
      key.Material = GetObjectMaterial(tgt);
      MeshId tempMeshId;
      if (_primOwner.TryGetValue(key, out tempMeshId) && (existingMeshId == null || tempMeshId == existingMeshId))
      {
        existingMeshId = tempMeshId;
      }
      else
      {
        existingMeshId = null;
      }
      if (existingMeshId != null)
      {
        return existingMeshId;
      }

      // if not, create new mesh and return its id
      var mesh = new GLTFMesh();
      mesh.Primitives = new List<MeshPrimitive>(1);
      MeshPrimitive[] meshPrimitives = ExportPrimitive(tgt, mesh);
      if (meshPrimitives != null)
      {
        mesh.Primitives.AddRange(meshPrimitives);
      }
      // Don't export meshes without primitives, since it's not valid
      if (mesh.Primitives.Count == 0)
      {
        return null;
      }

      var id = new MeshId
      {
        Id = _root.Meshes.Count,
        Root = _root
      };
      if (name != null)
        mesh.Name = name;
      _root.Meshes.Add(mesh);
      _primOwner[new PrimKey { Mesh = GetObjectMesh(tgt), Material = GetObjectMaterial(tgt) }] = id;
      return id;

    }

  }

}