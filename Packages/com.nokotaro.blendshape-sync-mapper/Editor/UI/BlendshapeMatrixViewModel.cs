using System;
using System.Collections.Generic;
using System.Linq;
using Nokotaro.BlendshapeSyncMapper.Analysis;

namespace Nokotaro.BlendshapeSyncMapper.UI
{
    public enum MatrixColumnFilter { Relevant, AllSource, Missing }
    public enum MatrixCellStatus { NotAvailable, Missing, Synced }

    // Derived, non-serialized UI data. No live MA objects or mutable curves are read here.
    public sealed class BlendshapeMatrixCell
    {
        public MatrixCellStatus Status { get; }
        public bool HasCustomTargetBinding { get; }
        public bool HasCustomMapping { get; }
        public bool HasBrokenMapping { get; }
        public bool IsRelevant => Status != MatrixCellStatus.NotAvailable || RelatedBindings.Count != 0;
        public IReadOnlyList<AnalyzedBinding> RelatedBindings { get; }
        public string Symbol { get; }
        public string StatusText { get; }

        public BlendshapeMatrixCell(string sourceName, CompatibleBlendshape compatible,
            IEnumerable<AnalyzedBinding> relatedBindings)
        {
            RelatedBindings = relatedBindings.Distinct().ToList().AsReadOnly();
            Status = compatible == null ? MatrixCellStatus.NotAvailable
                : compatible.IsSynced ? MatrixCellStatus.Synced : MatrixCellStatus.Missing;
            HasCustomTargetBinding = RelatedBindings.Any(b => b.Kind == BindingKind.CustomMapping
                && string.Equals(b.Binding.TargetName, sourceName, StringComparison.Ordinal));
            HasCustomMapping = RelatedBindings.Any(b => b.Kind == BindingKind.CustomMapping);
            HasBrokenMapping = RelatedBindings.Any(b => b.Kind == BindingKind.Broken);
            var missingCustom = Status == MatrixCellStatus.Missing && HasCustomTargetBinding;
            Symbol = Status == MatrixCellStatus.Synced ? "●" : Status == MatrixCellStatus.Missing
                ? missingCustom ? "△" : "○" : "-";
            if (HasCustomMapping && !missingCustom) Symbol += " C";
            if (HasBrokenMapping) Symbol += " ×";
            StatusText = Status == MatrixCellStatus.Synced ? "Synced (exact same-name)"
                : Status == MatrixCellStatus.Missing ? missingCustom
                    ? "Missing exact sync + existing custom target mapping"
                    : "Available but not synced (Missing exact)"
                : "Not available (no same-name Target shape)";
        }
    }

    public sealed class BlendshapeMatrixRow
    {
        public BlendshapeSyncRendererState RendererState { get; }
        public IReadOnlyList<BlendshapeMatrixCell> Cells { get; }

        internal BlendshapeMatrixRow(BlendshapeSyncRendererState renderer, List<BlendshapeMatrixCell> cells)
        {
            RendererState = renderer;
            Cells = cells.AsReadOnly();
        }
    }

    public sealed class BlendshapeMatrixViewModel
    {
        public IReadOnlyList<BlendshapeEntry> Columns { get; }
        public IReadOnlyList<BlendshapeMatrixRow> Rows { get; }
        private readonly bool[] relevant;
        private readonly bool[] missing;

        public BlendshapeMatrixViewModel(BlendshapeSyncAnalysis analysis)
            : this(analysis.SourceBlendshapes, analysis.Renderers) { }

        // This overload also allows testing all classification/filtering without a Scene or Scanner.
        public BlendshapeMatrixViewModel(IEnumerable<BlendshapeEntry> sourceShapes,
            IEnumerable<BlendshapeSyncRendererState> renderers)
        {
            Columns = sourceShapes.ToList().AsReadOnly();
            relevant = new bool[Columns.Count];
            missing = new bool[Columns.Count];
            var rows = new List<BlendshapeMatrixRow>();
            foreach (var renderer in renderers)
            {
                var compatible = renderer.CompatibleShapes.ToDictionary(s => s.Name, StringComparer.Ordinal);
                var related = new Dictionary<string, List<AnalyzedBinding>>(StringComparer.Ordinal);
                foreach (var binding in renderer.Bindings)
                {
                    // Target occupancy is distinct from an outgoing mapping from the selected Source.
                    // Unresolved/foreign broken references belong in row Details, never guessed into a cell.
                    if (binding.Kind != BindingKind.Broken) Add(related, binding.Binding.TargetName, binding);
                    if (binding.ReferencesSelectedSource
                        && (binding.Issues & (BindingIssue.MissingReference | BindingIssue.UnresolvedReference
                            | BindingIssue.ReferenceResolutionFailed | BindingIssue.MissingReferenceRenderer
                            | BindingIssue.MissingReferenceMesh | BindingIssue.MissingSourceBlendshape)) == 0)
                        Add(related, binding.Binding.SourceName, binding);
                }
                var cells = new List<BlendshapeMatrixCell>(Columns.Count);
                for (var c = 0; c < Columns.Count; c++)
                {
                    var name = Columns[c].Name;
                    compatible.TryGetValue(name, out var match);
                    related.TryGetValue(name, out var bindings);
                    var cell = new BlendshapeMatrixCell(name, match,
                        bindings ?? (IEnumerable<AnalyzedBinding>)Array.Empty<AnalyzedBinding>());
                    cells.Add(cell);
                    relevant[c] |= cell.IsRelevant;
                    missing[c] |= cell.Status == MatrixCellStatus.Missing;
                }
                rows.Add(new BlendshapeMatrixRow(renderer, cells));
            }
            Rows = rows.AsReadOnly();
        }

        private static void Add(Dictionary<string, List<AnalyzedBinding>> index, string name, AnalyzedBinding binding)
        {
            if (name == null) return;
            if (!index.TryGetValue(name, out var list)) index.Add(name, list = new List<AnalyzedBinding>());
            list.Add(binding);
        }

        // Called only when the filter/search changes; the returned indices address original Snapshot columns.
        public IReadOnlyList<int> FilterColumns(string search, MatrixColumnFilter filter)
        {
            var result = new List<int>();
            for (var c = 0; c < Columns.Count; c++)
            {
                if (filter == MatrixColumnFilter.Relevant && !relevant[c]) continue;
                if (filter == MatrixColumnFilter.Missing && !missing[c]) continue;
                if (!string.IsNullOrEmpty(search)
                    && (Columns[c].Name ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                result.Add(c);
            }
            return result.AsReadOnly();
        }
    }
}
