using System;
using Nokotaro.BlendshapeSyncMapper.Analysis;
using Nokotaro.BlendshapeSyncMapper.UI;
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
        private BlendshapeMatrixView matrix;
        private Vector2 detailScroll;
        private string search = "";
        private MatrixColumnFilter columnFilter;
        private static readonly string[] FilterNames = { "Relevant", "All Source", "Missing" };
        private string scanError;
        private string snapshotMessage = "Press Rescan to analyze the current setup.";

        public BlendshapeSyncAnalysis Analysis => analysis;
        public BlendshapeMatrixView Matrix => matrix;

        private void OnEnable()
        {
            minSize = new Vector2(680, 660);
            InvalidateSnapshot("Press Rescan to analyze the current setup.");
        }

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
            EditorGUILayout.LabelField("Read-only snapshot. Rescan after external changes. Missing includes custom-occupied targets.", EditorStyles.wordWrappedMiniLabel);
            if (analysis.Renderers.Count == 0)
            {
                EditorGUILayout.HelpBox("No SkinnedMeshRenderers found under Target Root.", MessageType.Info);
                return;
            }

            EditorGUI.BeginChangeCheck();
            var nextSearch = EditorGUILayout.TextField("Search BlendShapes", search);
            var nextFilter = (MatrixColumnFilter)EditorGUILayout.Popup("View", (int)columnFilter, FilterNames);
            if (EditorGUI.EndChangeCheck()) SetColumnFilter(nextSearch, nextFilter);
            EditorGUILayout.LabelField("● Synced   ○ Missing exact   △ Missing + custom on Target   - Not available",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("C Related custom mapping   × Related broken mapping   |   Hover cells for binding / remap details",
                EditorStyles.wordWrappedMiniLabel);
            matrix.Draw(GUILayoutUtility.GetRect(0, 10000, 120, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)));
            EditorGUILayout.LabelField("Selected Renderer Details", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(matrix.SelectedRow < 0))
                if (GUILayout.Button("Select in Hierarchy")) matrix.SelectRow(matrix.SelectedRow);
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUILayout.Height(145));
            var details = matrix.SelectedDetails;
            var detailHeight = EditorStyles.wordWrappedLabel.CalcHeight(matrix.SelectedDetailContent, Mathf.Max(100, position.width - 36));
            EditorGUILayout.SelectableLabel(details, EditorStyles.wordWrappedLabel, GUILayout.Height(detailHeight));
            EditorGUILayout.EndScrollView();
        }

        public void Rescan()
        {
            InvalidateSnapshot("Press Rescan to analyze the current setup.");
            var error = BlendshapeSyncScanner.GetInputError(sourceRenderer, targetRoot);
            if (error != null) { scanError = error; return; }
            try
            {
                analysis = BlendshapeSyncScanner.Scan(sourceRenderer, targetRoot);
                matrix = new BlendshapeMatrixView(analysis);
                matrix.SetFilter(search, columnFilter);
            }
            catch (Exception exception)
            {
                analysis = null;
                matrix = null;
                scanError = $"Scan failed: {exception.GetType().Name}: {exception.Message}";
            }
            Repaint();
        }

        public void SetColumnFilter(string query, MatrixColumnFilter filter)
        {
            search = query ?? "";
            columnFilter = filter;
            matrix?.SetFilter(search, columnFilter);
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
            matrix = null;
            detailScroll = Vector2.zero;
            scanError = null;
            snapshotMessage = message;
        }

    }
}
