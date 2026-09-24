using System.Collections.Generic;
using System.Text;
using Nokotaro.BlendshapeSyncMapper.Analysis;
using UnityEditor;
using UnityEngine;

namespace Nokotaro.BlendshapeSyncMapper.UI
{
    // IMGUI presentation cache. Built once per Snapshot, never on Repaint.
    public sealed class BlendshapeMatrixView
    {
        private const float RendererWidth = 200, ColumnWidth = 112, RowHeight = 38, HeaderHeight = 28, Bar = 16;
        private readonly BlendshapeMatrixViewModel model;
        private readonly GUIContent[] headers;
        private readonly GUIContent[] rowLabels;
        private readonly GUIContent[,] cells;
        private readonly GUIContent[] details;
        private static readonly GUIContent NoSelection = new GUIContent("Select a Renderer row to inspect its diagnostics.");
        private GUIStyle cellStyle, headerStyle, rowStyle;
        private bool darkSkin;
        public IReadOnlyList<int> VisibleColumns { get; private set; }
        public int SelectedRow { get; private set; } = -1;
        public Vector2 ScrollPosition { get; set; }
        public BlendshapeMatrixViewModel Model => model;

        public BlendshapeMatrixView(BlendshapeSyncAnalysis snapshot)
        {
            model = new BlendshapeMatrixViewModel(snapshot);
            headers = new GUIContent[model.Columns.Count];
            rowLabels = new GUIContent[model.Rows.Count];
            cells = new GUIContent[model.Rows.Count, model.Columns.Count];
            details = new GUIContent[model.Rows.Count];
            var sourcePath = ObjectPath(snapshot.Source.gameObject);
            for (var c = 0; c < headers.Length; c++)
                headers[c] = new GUIContent(model.Columns[c].Name, $"{model.Columns[c].Name}\nSource Mesh index: {model.Columns[c].Index}");
            for (var r = 0; r < model.Rows.Count; r++)
            {
                var row = model.Rows[r].RendererState;
                rowLabels[r] = new GUIContent($"{row.DisplayName}\n{row.SyncedCount} / {row.CompatibleCount} synced  × {row.BrokenCount}",
                    $"{row.HierarchyPath}\nID {row.RendererInstanceId}\nClick to select in Hierarchy.");
                var bindingText = new Dictionary<AnalyzedBinding, string>();
                foreach (var binding in row.Bindings) bindingText[binding] = DescribeBinding(binding);
                for (var c = 0; c < headers.Length; c++)
                {
                    var cell = model.Rows[r].Cells[c];
                    var name = model.Columns[c].Name;
                    var tip = new StringBuilder(cell.StatusText);
                    tip.Append($"\n\nSource: {sourcePath}\nSource BlendShape: {name}\n\nTarget: {row.HierarchyPath}\nTarget same-name BlendShape: {name}");
                    if (cell.Status == MatrixCellStatus.NotAvailable) tip.Append(" (absent)");
                    tip.Append("\n\nExisting bindings (target occupancy or selected-source mapping):");
                    if (cell.RelatedBindings.Count == 0) tip.Append("\nNone associated. Remap curve: none for this cell. See row Details for other/broken bindings.");
                    foreach (var binding in cell.RelatedBindings) tip.Append("\n\n").Append(bindingText[binding]);
                    cells[r, c] = new GUIContent(cell.Symbol, tip.ToString());
                }
                var text = new StringBuilder($"{row.DisplayName}\nPath: {row.HierarchyPath}\nInstance ID: {row.RendererInstanceId}\n" +
                    $"Source {row.SourceBlendshapeCount} / Target {row.TargetBlendshapes.Count} shapes\n" +
                    $"{row.CompatibleCount} compatible / {row.SyncedCount} synced / {row.MissingCount} missing\n" +
                    $"{row.CustomCount} custom / {row.BrokenCount} broken / {row.Components.Count} MA components");
                if (row.Mesh == null) text.Append("\nTarget Renderer has no sharedMesh.");
                foreach (var diagnostic in row.Diagnostics) text.Append("\nDiagnostic: ").Append(diagnostic);
                foreach (var binding in row.Bindings)
                    if (binding.Kind != BindingKind.ExactSameName) text.Append("\n\n").Append(bindingText[binding]);
                details[r] = new GUIContent(text.ToString());
            }
            SetFilter("", MatrixColumnFilter.Relevant);
        }

        public void SetFilter(string search, MatrixColumnFilter filter)
        {
            VisibleColumns = model.FilterColumns(search, filter);
            ScrollPosition = new Vector2(0, ScrollPosition.y);
        }

        public void SelectRow(int row)
        {
            if (row < 0 || row >= model.Rows.Count) return;
            SelectedRow = row;
            var renderer = model.Rows[row].RendererState.Renderer;
            if (renderer != null) Selection.activeGameObject = renderer.gameObject;
        }

        public GUIContent SelectedDetailContent => SelectedRow < 0 ? NoSelection : details[SelectedRow];
        public string SelectedDetails => SelectedDetailContent.text;
        public GUIContent CellContent(int row, int column) => cells[row, column];

        public void Draw(Rect rect)
        {
            if (cellStyle == null || darkSkin != EditorGUIUtility.isProSkin)
            {
                darkSkin = EditorGUIUtility.isProSkin;
                cellStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
                headerStyle = new GUIStyle(EditorStyles.toolbarButton) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
                rowStyle = new GUIStyle(EditorStyles.miniButton) {
                    alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip,
                    fixedHeight = 0, padding = new RectOffset(6, 4, 3, 3)
                };
            }
            var bodyHeight = Mathf.Max(1, rect.height - HeaderHeight - Bar);
            var matrixWidth = Mathf.Max(1, rect.width - RendererWidth - Bar);
            var contentWidth = VisibleColumns.Count * ColumnWidth;
            var contentHeight = model.Rows.Count * RowHeight;
            var scroll = ScrollPosition;
            if (Event.current.type == EventType.ScrollWheel && rect.Contains(Event.current.mousePosition))
            {
                if (Event.current.shift) scroll.x += Event.current.delta.y * 24;
                else { scroll.y += Event.current.delta.y * 24; scroll.x += Event.current.delta.x * 24; }
                Event.current.Use();
            }
            scroll.x = Mathf.Clamp(scroll.x, 0, Mathf.Max(0, contentWidth - matrixWidth));
            scroll.y = Mathf.Clamp(scroll.y, 0, Mathf.Max(0, contentHeight - bodyHeight));
            scroll.x = GUI.HorizontalScrollbar(new Rect(rect.x + RendererWidth, rect.yMax - Bar, matrixWidth, Bar),
                scroll.x, matrixWidth, 0, Mathf.Max(matrixWidth, contentWidth));
            scroll.y = GUI.VerticalScrollbar(new Rect(rect.xMax - Bar, rect.y + HeaderHeight, Bar, bodyHeight),
                scroll.y, bodyHeight, 0, Mathf.Max(bodyHeight, contentHeight));
            ScrollPosition = scroll;
            GUI.Label(new Rect(rect.x, rect.y, RendererWidth, HeaderHeight), "Renderer (synced / compatible)", EditorStyles.miniBoldLabel);
            var firstColumn = Mathf.Max(0, Mathf.FloorToInt(scroll.x / ColumnWidth));
            var lastColumn = Mathf.Min(VisibleColumns.Count, Mathf.CeilToInt((scroll.x + matrixWidth) / ColumnWidth));
            GUI.BeginGroup(new Rect(rect.x + RendererWidth, rect.y, matrixWidth, HeaderHeight));
            for (var c = firstColumn; c < lastColumn; c++)
                GUI.Label(new Rect(c * ColumnWidth - scroll.x, 0, ColumnWidth, HeaderHeight), headers[VisibleColumns[c]], headerStyle);
            GUI.EndGroup();

            var firstRow = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
            var lastRow = Mathf.Min(model.Rows.Count, Mathf.CeilToInt((scroll.y + bodyHeight) / RowHeight));
            GUI.BeginGroup(new Rect(rect.x, rect.y + HeaderHeight, RendererWidth, bodyHeight));
            for (var r = firstRow; r < lastRow; r++)
            {
                var rowRect = new Rect(0, r * RowHeight - scroll.y, RendererWidth - 2, RowHeight - 1);
                if (GUI.Toggle(rowRect, r == SelectedRow, rowLabels[r], rowStyle) != (r == SelectedRow)) SelectRow(r);
            }
            GUI.EndGroup();
            GUI.BeginGroup(new Rect(rect.x + RendererWidth, rect.y + HeaderHeight, matrixWidth, bodyHeight));
            for (var r = firstRow; r < lastRow; r++)
            {
                var y = r * RowHeight - scroll.y;
                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(new Rect(0, y, matrixWidth, RowHeight - 1), r == SelectedRow
                        ? (darkSkin ? new Color(.2f, .36f, .5f) : new Color(.65f, .8f, .94f))
                        : new Color(0, 0, 0, r % 2 == 0 ? .07f : .02f));
                for (var c = firstColumn; c < lastColumn; c++)
                    GUI.Label(new Rect(c * ColumnWidth - scroll.x, y, ColumnWidth, RowHeight), cells[r, VisibleColumns[c]], cellStyle);
            }
            if (VisibleColumns.Count == 0) GUI.Label(new Rect(8, 4, matrixWidth - 8, 44),
                model.Columns.Count == 0 ? "Source Renderer has no BlendShapes." : "No columns match this search / view.", EditorStyles.wordWrappedLabel);
            GUI.EndGroup();
        }

        private static string DescribeBinding(AnalyzedBinding analyzed)
        {
            var b = analyzed.Binding;
            return $"{analyzed.Kind} — Component {b.ComponentIndex}, Binding {b.BindingIndex}\n" +
                $"Reference: {ObjectPath(b.ReferenceObject)} ({(analyzed.ReferencesSelectedSource ? "selected Source" : "other / unresolved Source")})\n" +
                $"Source BlendShape: {b.SourceName ?? "<null>"} → Target BlendShape: {b.TargetName ?? "<null>"}\n" +
                $"Remap curve: {(b.HasRemapCurve ? "present" : "absent")}; valid flag: {b.RemapCurveIsValid}; keys: {b.RemapKeys.Count}\n" +
                $"Issues: {analyzed.Issues}" + (string.IsNullOrEmpty(b.ResolutionError) ? "" : "\n" + b.ResolutionError);
        }

        private static string ObjectPath(GameObject obj)
        {
            if (obj == null) return "<unresolved>";
            var parts = new List<string>();
            for (var t = obj.transform; t != null; t = t.parent) parts.Add($"{t.name} [{t.GetSiblingIndex()}]");
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
