using ColorMelt.Core;
using UnityEditor;
using UnityEngine;

namespace ColorMelt.Editor
{
    [CustomEditor(typeof(LevelDatabase))]
    public class LevelDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Levels are played in this order. Create a level with Tools > Color Melt > Create New Level; " +
                "each level's inspector shows its optimal solution.", MessageType.None);
            DrawDefaultInspector();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create New Level")) LevelDataEditor.CreateLevel();
                if (GUILayout.Button("Validate All")) LevelDataEditor.ValidateAll();
            }
        }
    }
}
