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
            if (GUILayout.Button("Add Missing Routes And Restore Levers"))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Add Missing Routes And Restore Levers");
                generator.Generate();
                EditorUtility.SetDirty(generator.gameObject);
            }

            if (GUILayout.Button("Clear Generated Routes"))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Clear Generated Routes");
                generator.ClearGenerated();
                EditorUtility.SetDirty(generator.gameObject);
            }

            if (GUILayout.Button("Refresh Existing Routes And Levers"))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Refresh Existing Routes And Levers");
                generator.RebuildExistingRoutes();
                EditorUtility.SetDirty(generator.gameObject);
            }
        }
    }
}
