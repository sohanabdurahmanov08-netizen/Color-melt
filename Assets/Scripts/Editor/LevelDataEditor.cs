using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ColorMelt.Core;
using ColorMelt.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColorMelt.Editor
{
    /// <summary>
    /// Level design tools: every LevelData inspector runs the solver and shows
    /// the optimal solution, warnings, a scene preview and a Play button.
    /// </summary>
    [CustomEditor(typeof(LevelData))]
    public class LevelDataEditor : UnityEditor.Editor
    {
        public const string TestLevelPref = "ColorMelt.TestLevel";
        private const string LevelsFolder = "Assets/Levels";
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        private List<Move> _solution;
        private string _report;
        private MessageType _reportType;

        private void OnEnable() => Analyze((LevelData)target);

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            var level = (LevelData)target;
            if (EditorGUI.EndChangeCheck())
                Analyze(level);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Solver", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_report, _reportType);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview In Scene")) Preview(level);
                if (GUILayout.Button("Clear Preview")) ClearPreview();
            }

            if (GUILayout.Button("▶ Play This Level", GUILayout.Height(28)))
                Play(level);
        }

        private void Analyze(LevelData level)
        {
            var result = Validate(level, out _solution);
            _report = result.message;
            _reportType = result.type;

            var optimal = _solution?.Count ?? -1;
            if (level.optimalMoves != optimal)
            {
                level.optimalMoves = optimal;
                EditorUtility.SetDirty(level);
            }
        }

        public static (string message, MessageType type) Validate(LevelData level, out List<Move> solution)
        {
            solution = null;
            var text = new StringBuilder();
            var warning = false;

            if (level.routes.Count < 2)
                return ("Add at least two routes.", MessageType.Error);
            if (level.BlockCount == 0)
                return ("Add at least one block.", MessageType.Error);

            var start = new FlowModel(level);
            if (start.FindMeltable().Count > 0)
            {
                text.AppendLine("⚠ A block melts before the first move (its channel already has its colour).");
                warning = true;
            }

            solution = LevelSolver.Solve(level);
            if (solution == null)
                return ("✖ Unsolvable: no sequence of pours melts every block.", MessageType.Error);

            text.AppendLine($"Optimal solution: {solution.Count} move(s). Moves given: {level.maxMoves}.");
            if (level.maxMoves < solution.Count)
            {
                text.AppendLine($"✖ Not enough moves: needs at least {solution.Count}.");
                return (text.ToString().TrimEnd(), MessageType.Error);
            }

            var model = new FlowModel(level);
            model.ResolveAll();
            for (var step = 0; step < solution.Count; step++)
            {
                var move = solution[step];
                model.Pour(move.from, move.to);
                var into = model.ChannelColor(move.to);
                var melted = model.ResolveAll();
                text.AppendLine($"{step + 1}. {level.routes[move.from].color} → {level.routes[move.to].color} channel" +
                                (move.from == move.to ? " (back home)" : $" = {into}") +
                                (melted > 0 ? $"  • melts {melted}" : ""));
            }

            return (text.ToString().TrimEnd(), warning ? MessageType.Warning : MessageType.Info);
        }

        private static void Preview(LevelData level)
        {
            var builder = Object.FindAnyObjectByType<LevelBuilder>();
            if (builder == null)
            {
                EditorUtility.DisplayDialog("Color Melt", "Open the Game scene to preview levels.", "OK");
                return;
            }

            var routes = builder.Build(level);
            foreach (var route in routes)
            {
                route.Channel.SetPaint(route.SourceColor);
                route.Channel.SetFillLimit(route.Blocks.Count > 0 ? route.FillLimit(0) : 1f);
                route.Channel.SnapToTarget();
            }
            SceneView.RepaintAll();
        }

        private static void ClearPreview()
        {
            var builder = Object.FindAnyObjectByType<LevelBuilder>();
            if (builder != null) builder.Clear();
            SceneView.RepaintAll();
        }

        private static void Play(LevelData level)
        {
            ClearPreview();
            EditorPrefs.SetString(TestLevelPref, AssetDatabase.GetAssetPath(level));
            if (EditorSceneManager.GetActiveScene().path != GameScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(GameScenePath);
            }
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Tools/Color Melt/Create New Level", priority = 0)]
        public static void CreateLevel()
        {
            if (!AssetDatabase.IsValidFolder(LevelsFolder))
                AssetDatabase.CreateFolder("Assets", "Levels");

            var database = LevelDatabase.Instance;
            var number = (database != null ? database.Count : 0) + 1;
            var path = AssetDatabase.GenerateUniqueAssetPath($"{LevelsFolder}/Level_{number:00}.asset");

            var level = CreateInstance<LevelData>();
            level.maxMoves = 3;
            level.routes = new List<RouteData>
            {
                new RouteData { color = ColorType.Blue },
                new RouteData { color = ColorType.Red, blocks = { new BlockData { color = ColorType.Purple } } },
                new RouteData { color = ColorType.Yellow }
            };
            AssetDatabase.CreateAsset(level, path);

            if (database != null)
            {
                Undo.RecordObject(database, "Add level");
                database.levels.Add(level);
                EditorUtility.SetDirty(database);
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = level;
            EditorGUIUtility.PingObject(level);
        }

        [MenuItem("Tools/Color Melt/Validate All Levels", priority = 1)]
        public static void ValidateAll()
        {
            var database = LevelDatabase.Instance;
            if (database == null)
            {
                Debug.LogError("Color Melt: Resources/LevelDatabase.asset not found.");
                return;
            }

            var report = new StringBuilder("Color Melt level report:\n");
            var errors = 0;
            for (var index = 0; index < database.levels.Count; index++)
            {
                var level = database.levels[index];
                if (level == null)
                {
                    report.AppendLine($"#{index + 1}: empty slot");
                    errors++;
                    continue;
                }

                var (message, type) = Validate(level, out var solution);
                var optimal = solution?.Count ?? -1;
                if (level.optimalMoves != optimal)
                {
                    level.optimalMoves = optimal;
                    EditorUtility.SetDirty(level);
                }
                if (type == MessageType.Error) errors++;
                report.AppendLine($"#{index + 1} {level.name}: routes {level.routes.Count}, blocks {level.BlockCount}, " +
                                  $"optimal {optimal}, moves {level.maxMoves} {(type == MessageType.Error ? "✖ " + message.Split('\n').Last() : "✓")}");
            }

            AssetDatabase.SaveAssets();
            if (errors > 0) Debug.LogWarning(report.ToString());
            else Debug.Log(report.ToString());
        }

        [MenuItem("Tools/Color Melt/Open Level Database", priority = 2)]
        public static void OpenDatabase()
        {
            if (LevelDatabase.Instance != null)
                Selection.activeObject = LevelDatabase.Instance;
        }
    }
}
