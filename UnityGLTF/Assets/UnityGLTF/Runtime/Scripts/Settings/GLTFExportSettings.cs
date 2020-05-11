using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;


[CustomEditor(typeof(GLTFExportSettings))]
internal class SettingsEditor : UnityEditor.Editor {
  Vector2 scrollPos = Vector2.zero;
  const float LabelWidth = 150;
  const float SelectableLabelMinWidth = 90;
  const float BrowseButtonWidth = 25;
  const float FieldOffset = 18;
  const float BrowseButtonOffset = 5;



  private string[] exportFormatOptions = new string[] { "GLB", "GLTF" };

  public List<SerializedProperty> GetSections( SerializedProperty infoProperty ){

    infoProperty.Next(true);
    var sectionProperty = infoProperty.Copy();

    var res = new List<SerializedProperty>();
    do {
      res.Add( sectionProperty.Copy() );
    } while( sectionProperty.Next(false) );
    return res;
  }

  public override void OnInspectorGUI() {
    
    serializedObject.Update();
    
    GLTFExportSettings exportSettings = (GLTFExportSettings)target;
    GLTFExportSettingsData data = exportSettings.info;
    SerializedProperty infoProperty = serializedObject.FindProperty("m_info");
    
    var sections = GetSections( infoProperty );

    // Increasing the label width so that none of the text gets cut off
    EditorGUIUtility.labelWidth = LabelWidth;

    GUILayout.BeginVertical ();
    var w = EditorGUIUtility.currentViewWidth;
    foreach (var section in sections)
    {
      section.isExpanded = true;
      var h = EditorGUI.GetPropertyHeight( section, true );
      var myRect = GUILayoutUtility.GetRect(20, h+10);
      EditorGUI.PropertyField(myRect, section, true);
    }

    GUILayout.EndVertical ();

    serializedObject.ApplyModifiedProperties();
  }
}

#endif


public class GLTFExportSettings : SerializedSettings<GLTFExportSettingsData> {

  // [SerializeField]
  // public SettingsSerializable directSettings;

  static readonly string k_ConfigObjectName = "com.plepers.unitygltf.ExportSettings";
  static readonly string k_SettingsPath = "Assets/GltfExportSettings.asset";

  private static GLTFExportSettings m_defaults = null;

  public static GLTFExportSettings Defaults {
    get {
      if (m_defaults == null) {
        m_defaults = LoadDefaults ();
      }
      return m_defaults;
    }
  }

  private static GLTFExportSettings LoadDefaults() {
    GLTFExportSettings settings;

    if (!EditorBuildSettings.TryGetConfigObject (k_ConfigObjectName, out settings)) {
      settings = ScriptableObject.CreateInstance<GLTFExportSettings> ();
      AssetDatabase.CreateAsset (settings, k_SettingsPath);
      EditorBuildSettings.AddConfigObject (k_ConfigObjectName, settings, true);
    }
    return settings;
  }

}