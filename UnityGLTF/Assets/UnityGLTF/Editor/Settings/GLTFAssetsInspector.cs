using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityGLTF;

public class GLTFAssetsInspector : EditorWindow
{

  Vector2 scrollView;

  Dictionary<int, bool> foldStates = new Dictionary<int, bool>();

  // Add menu named "My Window" to the Window menu
  [MenuItem("GLTF/Assets Inspector")]
  static void Init()
  {
    // Get existing open window or if none, make a new one:
    GLTFAssetsInspector window = (GLTFAssetsInspector)EditorWindow.GetWindow(typeof(GLTFAssetsInspector));
    window.Show();
  }

  void AddMaterialTextures(ref List<Texture2D> textures, Material material)
  {
    if (material == null)
      return;
    Shader shader = material.shader;
    for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
    {
      if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
      {
        Texture2D texture = material.GetTexture(ShaderUtil.GetPropertyName(shader, i)) as Texture2D;
        if (textures.IndexOf(texture) == -1 && texture != null)
        {
          textures.Add(texture);
        }
      }
    }
  }

  void OnGUI()
  {

    var gameObjects = Selection.GetFiltered<GameObject>(SelectionMode.Editable);
    var gltfSettings = Selection.GetFiltered<GLTFExportSettings>(SelectionMode.Assets);
    scrollView = GUILayout.BeginScrollView(scrollView);

    if (gltfSettings.Length > 0)
    {
      EditorGUILayout.InspectorTitlebar(true, gltfSettings[0]);
      var so = new SerializedObject(gltfSettings);
      EditorGUILayout.PropertyField(so.FindProperty("m_info"), new GUIContent("Edit Settings"));
      so.ApplyModifiedProperties();
      GUILayout.Space(10);
    }


    List<Texture2D> textures = new List<Texture2D>();

    // GAME OBJECTS
    // =========
    if (gameObjects.Length > 0)
    {
      GUILayout.Label("Selected GameObject", EditorStyles.boldLabel);

      foreach (var gameObject in gameObjects)
      {
        EditorGUILayout.InspectorTitlebar(true, gameObject);
        Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
          foreach (Material material in renderers[i].sharedMaterials)
          {
            AddMaterialTextures(ref textures, material);
          }
        }
      }
    }

    // TEXTURES
    // =========
    var selectedTextures = Selection.GetFiltered<Texture2D>(SelectionMode.Assets);
    foreach (var texture in selectedTextures)
    {
      if (textures.IndexOf(texture) == -1)
      {
        textures.Add(texture);
      }
    }
    GUILayoutOption[] btnLayout = new GUILayoutOption[]{
      GUILayout.MaxWidth(150),
      GUILayout.MinHeight(50),
    };

    bool hasTex = textures.Count > 0;
    if (hasTex)
    {
      EditorGUI.indentLevel++;
      GUILayout.Label("Selected Textures", EditorStyles.boldLabel);
      foreach (var texture in textures)
      {
        DrawTextureInspector(texture);
      }
      EditorGUI.indentLevel--;
    }

    GUILayout.Space(10);
    GUILayout.BeginHorizontal();
    GUILayout.FlexibleSpace();

    if (hasTex)
    {
      if (GUILayout.Button("Export Textures", btnLayout))
      {
        var exporter = new GLTFSceneExporter(Selection.transforms, new ExportOptions());
        var path = EditorUtility.OpenFolderPanel("Textures export path", "", "");
        if (path != "")
        {
          foreach (Texture2D tex in textures)
          {
            exporter.ExportTexture(tex, path);
            exporter.ExportCompressed(tex, tex, path);
          }
        }
      }
    }
    GUILayout.FlexibleSpace();
    GUILayout.EndHorizontal();

    GUILayout.BeginHorizontal();
    GUILayout.FlexibleSpace();

    if (GUILayout.Button("Export GLTF", btnLayout))
    {
      GLTFExportMenu.ExportSelected();
    }

    GUILayout.FlexibleSpace();
    GUILayout.EndHorizontal();

    GUILayout.EndScrollView();
    Repaint();

  }


  private void DrawTextureInspector(Texture2D texture)
  {

    GLTFTexturesRegistry reg = GLTFExportSettings.Defaults.info.TexturesRegistry;

    GLTFTextureSettingsBinding binding = reg.GetBinding(texture);

    int ID = texture.GetInstanceID();
    if (!foldStates.ContainsKey(ID))
    {
      foldStates.Add(ID, false);
    }

    bool foldState;
    foldStates.TryGetValue(ID, out foldState);
    foldState = EditorGUILayout.InspectorTitlebar(foldState, texture);
    foldStates[ID] = foldState;
    if (!foldState)
      return;

    GUILayoutOption[] btnLayout = new GUILayoutOption[]{
      GUILayout.MaxWidth(150)
    };

    EditorGUILayout.BeginVertical();
    GUILayout.Space(10);

    if (binding != null)
    {

      var so = new SerializedObject(binding);
      EditorGUILayout.ObjectField(texture, typeof(Texture2D), true);
      EditorGUILayout.PropertyField(so.FindProperty("settings.Compress"), true);
      EditorGUILayout.PropertyField(so.FindProperty("settings.GenerateMipMap"), true);
      so.ApplyModifiedProperties();

      GUILayout.Space(5);
      GUILayout.BeginHorizontal();
      GUILayout.Space(25);

      if (GUILayout.Button("Remove Settings", btnLayout))
      {
        reg.RemoveSettings(texture);
      }
      GUILayout.EndHorizontal();

    }
    else
    {
      GUILayout.BeginHorizontal();
      GUILayout.Space(25);
      if (GUILayout.Button("Add Settings", btnLayout))
      {
        reg.CreateSettings(texture);
      }
      GUILayout.EndHorizontal();
    }

    GUILayout.Space(10);
    EditorGUILayout.EndVertical();

  }
}