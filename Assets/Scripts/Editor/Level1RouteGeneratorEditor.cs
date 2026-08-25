using UnityEditor;
using UnityEngine;

namespace ColorMelt.Core.Editor
{
    [CustomEditor(typeof(Level1RouteGenerator))]
    public class Level1RouteGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            var generator = (Level1RouteGenerator)target;
            if (GUILayout.Button("Generate Level 1 Routes"))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Generate Level 1 Routes");
                generator.Generate();
                EditorUtility.SetDirty(generator.gameObject);
            }

            if (GUILayout.Button("Clear Generated Routes"))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Clear Generated Routes");
                generator.ClearGenerated();
                EditorUtility.SetDirty(generator.gameObject);
            }
        }
    }
}
