using System;
using Nokotaro.BlendshapeSyncMapper.Analysis;
using Nokotaro.BlendshapeSyncMapper.UI;
using Nokotaro.BlendshapeSyncMapper.ModularAvatar;
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
        private BulkAddPreview bulkPreview;
        private bool reviewingBulk, writingBulk;
        private Vector2 previewScroll;
        private BulkAddResult bulkResult;
        public BulkAddPreview BulkPreview => bulkPreview;
        private AddSyncResult writeResult;
        private AddSyncResult selectionCheck;
        private int checkedRow = -1, checkedColumn = -1;
        private string selectedSourcePath;
        private string snapshotMessage = "Press Rescan to analyze the current setup.";

        public BlendshapeSyncAnalysis Analysis => analysis;
        public BlendshapeMatrixView Matrix => matrix;

        private void OnEnable()
        {
            minSize = new Vector2(680, 660);
            InvalidateSnapshot("Press Rescan to analyze the current setup.");
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable() { Undo.undoRedoPerformed -= OnUndoRedo; }

        private void OnUndoRedo()
        {
            if (analysis != null && !writingBulk) Rescan();
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
            EditorGUILayout.LabelField($"Missing: {bulkPreview.MissingCount} / Safe: {bulkPreview.SafeCount} / Require review: {bulkPreview.ReviewCount}");
            if (bulkResult != null)
                EditorGUILayout.HelpBox(bulkResult.Message + $"\n{bulkPreview.ReviewCount} mappings still require review.",
                    bulkResult.Succeeded ? MessageType.Info : MessageType.Warning);
            if (reviewingBulk) { DrawBulkPreview(); return; }
            using (new EditorGUI.DisabledScope(bulkPreview.MissingCount == 0))
                if (GUILayout.Button("Review Safe Changes")) { reviewingBulk = true; GUIUtility.ExitGUI(); }
            EditorGUILayout.LabelField("Select one cell, then Add Sync in Details. Rescan after external changes. Missing includes custom-occupied targets.", EditorStyles.wordWrappedMiniLabel);
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
            DrawSelectedMapping();
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
            var selectedTarget = matrix != null && matrix.SelectedRow >= 0
                ? matrix.Model.Rows[matrix.SelectedRow].RendererState.Renderer : null;
            var selectedColumn = matrix?.SelectedColumn ?? -1;
            var selectedName = selectedColumn >= 0 ? matrix.Model.Columns[selectedColumn].Name : null;
            var scroll = matrix?.ScrollPosition ?? Vector2.zero;
            InvalidateSnapshot("Press Rescan to analyze the current setup.");
            var error = BlendshapeSyncScanner.GetInputError(sourceRenderer, targetRoot);
            if (error != null) { scanError = error; return; }
            try
            {
                analysis = BlendshapeSyncScanner.Scan(sourceRenderer, targetRoot);
                bulkPreview = BulkAddPreview.Create(analysis);
                matrix = new BlendshapeMatrixView(analysis);
                matrix.SetFilter(search, columnFilter);
                matrix.ScrollPosition = scroll;
                for (var r = 0; r < analysis.Renderers.Count; r++)
                {
                    if (analysis.Renderers[r].Renderer != selectedTarget) continue;
                    matrix.SelectRow(r);
                    for (var c = 0; c < matrix.Model.Columns.Count; c++)
                        if (matrix.Model.Columns[c].Name == selectedName) { matrix.SelectCell(r, c); break; }
                    break;
                }
            }
            catch (Exception exception)
            {
                analysis = null;
                matrix = null;
                scanError = $"Scan failed: {exception.GetType().Name}: {exception.Message}";
            }
            Repaint();
        }

        private ExactSyncRequest SelectedRequest()
        {
            if (analysis == null || matrix == null || matrix.SelectedRow < 0 || matrix.SelectedColumn < 0) return null;
            return new ExactSyncRequest(analysis, matrix.Model.Rows[matrix.SelectedRow].RendererState,
                matrix.Model.Columns[matrix.SelectedColumn].Name);
        }

        private void DrawBulkPreview()
        {
            EditorGUILayout.LabelField("Bulk Add Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Includes all missing exact same-name mappings in the scan, regardless of Search / View. Only Safe entries will be added. Require Review entries are left unchanged.", MessageType.Info);
            if (GUILayout.Button("Back to Matrix")) { reviewingBulk = false; GUIUtility.ExitGUI(); }
            using (new EditorGUI.DisabledScope(bulkPreview.SafeCount == 0))
                if (GUILayout.Button($"Add {bulkPreview.SafeCount} Safe Missing Syncs"))
                { AddSafeSyncs(); GUIUtility.ExitGUI(); }
            previewScroll = EditorGUILayout.BeginScrollView(previewScroll);
            BlendshapeSyncRendererState previous = null;
            foreach (var candidate in bulkPreview.Candidates)
            {
                var row = candidate.Request.Row;
                if (row != previous)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(row.HierarchyPath, EditorStyles.boldLabel);
                    previous = row;
                }
                EditorGUILayout.LabelField(candidate.Request.Shape + (candidate.Eligibility.Succeeded ? " — Safe" : " — Require Review"),
                    EditorStyles.wordWrappedLabel);
                if (!candidate.Eligibility.Succeeded)
                    EditorGUILayout.LabelField(candidate.Eligibility.Message, EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        public BulkAddResult AddSafeSyncs()
        {
            BulkAddResult result;
            writingBulk = true;
            try { result = BlendshapeSyncBulkAdd.Execute(bulkPreview, sourceRenderer, targetRoot); }
            finally { writingBulk = false; }
            Rescan();
            bulkResult = result;
            Repaint();
            return result;
        }

        private void DrawSelectedMapping()
        {
            var request = SelectedRequest();
            if (request != null)
            {
                EditorGUILayout.LabelField("Selected Mapping", EditorStyles.boldLabel);
                if (checkedRow != matrix.SelectedRow || checkedColumn != matrix.SelectedColumn || selectionCheck == null)
                {
                    if (checkedRow >= 0 && (checkedRow != matrix.SelectedRow || checkedColumn != matrix.SelectedColumn))
                        writeResult = null;
                    checkedRow = matrix.SelectedRow;
                    checkedColumn = matrix.SelectedColumn;
                    selectionCheck = ModularAvatarBlendshapeSyncWriter.CheckSelection(request);
                    selectedSourcePath = AnimationUtility.CalculateTransformPath(sourceRenderer.transform, null);
                }
                EditorGUILayout.LabelField($"Source: {selectedSourcePath} / {request.Shape}\nTarget: {request.Row.HierarchyPath} / {request.Shape}",
                    EditorStyles.wordWrappedLabel);
                var check = selectionCheck;
                EditorGUILayout.LabelField(matrix.Model.Rows[matrix.SelectedRow].Cells[matrix.SelectedColumn].StatusText,
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(check.Message, EditorStyles.wordWrappedMiniLabel);
                if (check.Succeeded && GUILayout.Button("Add Sync"))
                {
                    AddSelectedSync();
                    GUIUtility.ExitGUI();
                }
            }
            if (writeResult != null)
                EditorGUILayout.HelpBox(writeResult.Message, writeResult.Succeeded ? MessageType.Info : MessageType.Warning);
        }

        public AddSyncResult AddSelectedSync()
        {
            var result = ModularAvatarBlendshapeSyncWriter.TryAddExactSync(SelectedRequest(), sourceRenderer, targetRoot);
            if (result.Succeeded) Rescan();
            writeResult = result.Succeeded ? result : new AddSyncResult(result.Status,
                result.Message + "\nThe setup may have changed since the last scan. Please rescan.");
            Repaint();
            return result;
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
            bulkPreview = null;
            bulkResult = null;
            reviewingBulk = false;
            matrix = null;
            detailScroll = Vector2.zero;
            scanError = null;
            writeResult = null;
            selectionCheck = null;
            checkedRow = checkedColumn = -1;
            snapshotMessage = message;
        }

    }
}
