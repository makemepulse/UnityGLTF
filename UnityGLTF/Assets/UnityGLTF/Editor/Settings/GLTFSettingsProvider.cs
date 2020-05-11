using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GLTFSettingsProvider {



  [SettingsProvider]
  static SettingsProvider CreateSettingsProvider () {

    var keywords = new HashSet<string> (new [] { "Gltf", "export" });

    var settings = GLTFExportSettings.Defaults;
    if( settings == null ){
      Debug.Log("Null default setting");
      return null;
    }
    var provider = AssetSettingsProvider.CreateProviderFromObject ("Project/Test/export", GLTFExportSettings.Defaults, keywords);
    

    #if UNITY_2019_1_OR_NEWER
    provider.inspectorUpdateHandler += () => 
    #else
    provider.activateHandler += (searchContext, rootElement) => 
    #endif // UNITY_2019_1_OR_NEWER

    {
      if (provider.settingsEditor != null &&
        provider.settingsEditor.serializedObject.UpdateIfRequiredOrScript ()) {
        provider.Repaint ();
      }
    };
    
    provider.label = "GLTF Export Settings";

    return provider;
  }
}