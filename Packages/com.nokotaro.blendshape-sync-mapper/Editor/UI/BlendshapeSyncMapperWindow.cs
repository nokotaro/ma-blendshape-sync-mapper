using System;
using Nokotaro.BlendshapeSyncMapper.Analysis;
using UnityEditor;
using UnityEngine;

namespace Nokotaro.BlendshapeSyncMapper
{
    public sealed class BlendshapeSyncMapperWindow : EditorWindow
    {
        private const string WindowTitle = "MA Blendshape Sync Mapper";

        [SerializeField] private SkinnedMeshRenderer sourceRenderer;
        [SerializeField] private GameObject targetRoot;

        private BlendshapeSyncAnalysis analysis;
        private Vector2 scrollPosition;
        private string scanError;
        private string snapshotMessage = "Press Rescan to analyze the current setup.";

        public BlendshapeSyncAnalysis Analysis => analysis;

        [MenuItem("Tools/MA Blendshape Sync Mapper")]
        public static void OpenWindow()
        {
            GetWindow<BlendshapeSyncMapperWindow>(WindowTitle);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(WindowTitle, EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            sourceRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
                "Source Renderer", sourceRenderer, typeof(SkinnedMeshRenderer), true);
            targetRoot = (GameObject)EditorGUILayout.ObjectField(
                "Target Root", targetRoot, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck()) InvalidateSnapshot("Inputs changed. Press Rescan.");
            ValidateSnapshot();

            EditorGUILayout.Space();
            var inputError = BlendshapeSyncScanner.GetInputError(sourceRenderer, targetRoot);
            using (new EditorGUI.DisabledScope(inputError != null))
            {
                if (GUILayout.Button("Rescan")) Rescan();
            }
            if (inputError != null) { EditorGUILayout.HelpBox(inputError, MessageType.Info); return; }
            if (scanError != null) { EditorGUILayout.HelpBox(scanError, MessageType.Error); return; }
            if (analysis == null) { EditorGUILayout.HelpBox(snapshotMessage, MessageType.Info); return; }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"{analysis.Renderers.Count} Renderers", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"{analysis.CompatibleCount} compatible / {analysis.SyncedCount} synced / {analysis.MissingCount} missing",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField($"{analysis.CustomCount} custom / {analysis.BrokenCount} broken");
            EditorGUILayout.HelpBox("Read-only snapshot. Rescan after editing meshes or MA bindings. " +
                "Missing means no exact same-name sync; existing custom mappings are preserved.", MessageType.Info);
            if (analysis.Renderers.Count == 0)
            {
                EditorGUILayout.HelpBox("No SkinnedMeshRenderers found under Target Root.", MessageType.Info);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            foreach (var row in analysis.Renderers) DrawRenderer(row);
            EditorGUILayout.EndScrollView();
        }

        public void Rescan()
        {
            InvalidateSnapshot("Press Rescan to analyze the current setup.");
            var error = BlendshapeSyncScanner.GetInputError(sourceRenderer, targetRoot);
            if (error != null) { scanError = error; return; }
            try { analysis = BlendshapeSyncScanner.Scan(sourceRenderer, targetRoot); }
            catch (Exception exception) { scanError = $"Scan failed: {exception.GetType().Name}: {exception.Message}"; }
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (ValidateSnapshot()) Repaint();
        }

        private bool ValidateSnapshot()
        {
            if (analysis == null || !analysis.IsStale(sourceRenderer, targetRoot)) return false;
            InvalidateSnapshot("Inputs or referenced objects changed. Press Rescan.");
            return true;
        }

        private void InvalidateSnapshot(string message)
        {
            analysis = null;
            scanError = null;
            snapshotMessage = message;
        }

        private static void DrawRenderer(BlendshapeSyncRendererState row)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var status = row.BrokenCount > 0 || row.MissingCount > 0 ? "[!]"
                    : row.CompatibleCount > 0 ? "[OK]" : "[-]";
                if (GUILayout.Button(new GUIContent($"{status} {row.DisplayName}", "Select this Renderer in the Hierarchy."),
                        EditorStyles.miniButton) && row.Renderer != null)
                    Selection.activeGameObject = row.Renderer.gameObject;
                EditorGUILayout.LabelField($"{row.HierarchyPath} (ID {row.RendererInstanceId})", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField($"Source {row.SourceBlendshapeCount} / Target {row.TargetBlendshapes.Count} shapes");
                EditorGUILayout.LabelField($"{row.CompatibleCount} compatible / {row.SyncedCount} synced / {row.MissingCount} missing",
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField($"{row.CustomCount} custom / {row.BrokenCount} broken / {row.Components.Count} MA components",
                    EditorStyles.wordWrappedLabel);
                if (row.Mesh == null) EditorGUILayout.HelpBox("Target Renderer has no sharedMesh.", MessageType.Info);
                foreach (var diagnostic in row.Diagnostics) EditorGUILayout.HelpBox(diagnostic, MessageType.Warning);
            }
        }
    }
}
