using UnityEditor;
using UnityEngine;

public class GLTFAssetsInspector : EditorWindow {
  string myString = "Hello World";
  bool groupEnabled;
  bool myBool = true;
  float myFloat = 1.23f;

  Vector2 scrollView;

  // Add menu named "My Window" to the Window menu
  [MenuItem ("GLTF/Assets Inspector")]
  static void Init () {
    // Get existing open window or if none, make a new one:
    GLTFAssetsInspector window = (GLTFAssetsInspector) EditorWindow.GetWindow (typeof (GLTFAssetsInspector));
    window.Show ();
  }

  void OnGUI () {
    GUILayout.Label ("Base Settings", EditorStyles.boldLabel);
    myString = EditorGUILayout.TextField ("Text Field", myString);

    groupEnabled = EditorGUILayout.BeginToggleGroup ("Optional Settings", groupEnabled);
    myBool = EditorGUILayout.Toggle ("Toggle", myBool);
    myFloat = EditorGUILayout.Slider ("Slider", myFloat, -3, 3);
    EditorGUILayout.EndToggleGroup ();

    var textures = Selection.GetFiltered<Texture2D> (SelectionMode.Assets);
    scrollView = GUILayout.BeginScrollView (scrollView);
    GUILayout.Label ("Selected Textures", EditorStyles.boldLabel);
    foreach (var texture in textures) {
      EditorGUILayout.InspectorTitlebar (true, texture);
      DrawInspector (texture);
    }

    GUILayout.EndScrollView ();
    Repaint ();

  }

  private void DrawInspector (Texture2D texture) {
    GUILayout.Label ("Add your custom inspection here");
    var path = AssetDatabase.GetAssetPath( texture );
    EditorGUILayout.TextField ("Text Field", path );
    var assets = AssetDatabase.LoadAllAssetsAtPath( path );

    EditorGUI.indentLevel++;
    foreach (var o in assets)
    {
      GUILayout.Label (o.name);

    }

    if(GUILayout.Button("Add Settings"))
    {

      var settings = ScriptableObject.CreateInstance<GLTFExportTextureSettings>();
      settings.hideFlags = HideFlags.HideInHierarchy;
      // AssetDatabase.CreateAsset (settings, path+".settings.asset");
      // AssetDatabase.AddObjectToAsset( texture, settings );
      // AssetDatabase.Refresh();
      AssetDatabase.AddObjectToAsset( settings, texture );
      // AssetDatabase.Refresh();
    }
    EditorGUI.indentLevel--;
  }
}