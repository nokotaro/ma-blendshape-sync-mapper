using System;
using System.Linq;
using Nokotaro.BlendshapeSyncMapper.Analysis;
using Nokotaro.BlendshapeSyncMapper.UI;
using NUnit.Framework;
using UnityEngine;

namespace Nokotaro.BlendshapeSyncMapper.Tests
{
    public sealed partial class BlendshapeSyncScannerTests
    {
        private static AnalyzedBinding DetachedBinding(string source, string target, bool selected = true,
            BindingIssue issue = BindingIssue.None)
        {
            var data = new BlendshapeSyncBindingState(null, 0, 0, null, null, source, target,
                false, null, issue, null);
            return new AnalyzedBinding(data, issue, selected);
        }

        [Test]
        public void MatrixCellsPreserveExactMissingAndCustomOccupancySemantics()
        {
            var exact = DetachedBinding("A", "A");
            var custom = DetachedBinding("B", "A");
            var other = DetachedBinding("A", "A", false);
            var available = new CompatibleBlendshape("A", 0, 0, Array.Empty<AnalyzedBinding>());
            var synced = new CompatibleBlendshape("A", 0, 0, new[] { exact });
            Assert.That(new BlendshapeMatrixCell("A", synced, new[] { exact }).Symbol, Is.EqualTo("●"));
            Assert.That(new BlendshapeMatrixCell("A", available, Array.Empty<AnalyzedBinding>()).Symbol, Is.EqualTo("○"));
            Assert.That(new BlendshapeMatrixCell("A", null, Array.Empty<AnalyzedBinding>()).Symbol, Is.EqualTo("-"));
            var occupied = new BlendshapeMatrixCell("A", available, new[] { custom });
            Assert.That(occupied.Symbol, Is.EqualTo("△"));
            Assert.That(occupied.Status, Is.EqualTo(MatrixCellStatus.Missing));
            Assert.That(occupied.HasCustomTargetBinding, Is.True);
            var outgoing = new BlendshapeMatrixCell("A", available, new[] { DetachedBinding("A", "B") });
            Assert.That(outgoing.Symbol, Is.EqualTo("○ C"));
            Assert.That(outgoing.HasCustomTargetBinding, Is.False);
            Assert.That(new BlendshapeMatrixCell("A", synced, new[] { exact, custom }).Symbol, Is.EqualTo("● C"));
            Assert.That(new BlendshapeMatrixCell("A", available, new[] { other }).Status, Is.EqualTo(MatrixCellStatus.Missing));
        }

        private BlendshapeSyncRendererState MatrixRow(string[] targets, CompatibleBlendshape[] compatible,
            params AnalyzedBinding[] bindings)
        {
            // Precomputed detached analysis: no Scanner or MA Reader is involved in View Model tests.
            var renderer = Renderer("MatrixRow");
            return new BlendshapeSyncRendererState(renderer, "Avatar/MatrixRow", 5,
                targets.Select((name, i) => new BlendshapeEntry(name, i)), compatible, bindings,
                Array.Empty<Component>(), Array.Empty<string>());
        }

        [Test]
        public void MatrixFiltersSearchAndRetainsSourceColumnOrder()
        {
            var names = new[] { "Breast_big", "Breast_small", "CustomSource", "BrokenSource", "Unrelated" };
            var exact = DetachedBinding(names[0], names[0]);
            var custom = DetachedBinding(names[2], names[1]);
            var broken = DetachedBinding(names[3], "Gone", true, BindingIssue.MissingTargetBlendshape);
            var row = MatrixRow(names.Take(2).ToArray(), new[] {
                new CompatibleBlendshape(names[0], 0, 0, new[] { exact }),
                new CompatibleBlendshape(names[1], 1, 1, new[] { custom }) }, exact, custom, broken);
            var model = new BlendshapeMatrixViewModel(names.Select((name, i) => new BlendshapeEntry(name, i)), new[] { row });
            Assert.That(model.FilterColumns("", MatrixColumnFilter.Relevant), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(model.FilterColumns("", MatrixColumnFilter.AllSource), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
            Assert.That(model.FilterColumns("", MatrixColumnFilter.Missing), Is.EqualTo(new[] { 1 }));
            Assert.That(model.FilterColumns("bReAsT", MatrixColumnFilter.Relevant), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(model.FilterColumns("bReAsT", MatrixColumnFilter.Missing), Is.EqualTo(new[] { 1 }));
            Assert.That(model.FilterColumns("nothing", MatrixColumnFilter.AllSource), Is.Empty);
            Assert.That(model.Rows[0].Cells[1].Symbol, Is.EqualTo("△"));
            Assert.That(model.Rows[0].Cells[2].Symbol, Is.EqualTo("- C"));
            Assert.That(model.Rows[0].Cells[3].Symbol, Is.EqualTo("- ×"));
            Assert.That(row.MissingCount, Is.EqualTo(1));
        }

        [Test]
        public void MatrixDoesNotGuessForeignOrUnresolvedMappingsIntoSourceCells()
        {
            var foreign = DetachedBinding("A", "Elsewhere", false);
            var unresolved = DetachedBinding("A", "A", false, BindingIssue.UnresolvedReference);
            var missingSource = DetachedBinding("A", "Elsewhere", true, BindingIssue.MissingSourceBlendshape);
            var row = MatrixRow(Array.Empty<string>(), Array.Empty<CompatibleBlendshape>(), foreign, unresolved, missingSource);
            var model = new BlendshapeMatrixViewModel(new[] { new BlendshapeEntry("A", 0) }, new[] { row });
            Assert.That(model.FilterColumns("", MatrixColumnFilter.Relevant), Is.Empty);
            Assert.That(model.Rows[0].Cells[0].RelatedBindings, Is.Empty);
            Assert.That(model.Rows[0].RendererState.Bindings.Count, Is.EqualTo(3), "All findings remain available to row Details.");
        }

        [Test]
        public void MatrixShowsForeignCustomTargetOccupancyWithoutClaimingOutgoingMapping()
        {
            var foreign = DetachedBinding("B", "A", false);
            var row = MatrixRow(new[] { "A" }, new[] { new CompatibleBlendshape("A", 0, 0, new[] { foreign }) }, foreign);
            var model = new BlendshapeMatrixViewModel(new[] { new BlendshapeEntry("A", 0), new BlendshapeEntry("B", 1) }, new[] { row });
            Assert.That(model.Rows[0].Cells[0].Symbol, Is.EqualTo("△"));
            Assert.That(model.Rows[0].Cells[1].Symbol, Is.EqualTo("-"));
            Assert.That(model.FilterColumns("", MatrixColumnFilter.Relevant), Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void MatrixFiltersUnionAcrossRowsAndPreservesDuplicateSourceIndices()
        {
            var exact = DetachedBinding("A", "A");
            var first = MatrixRow(new[] { "A" }, new[] { new CompatibleBlendshape("A", 0, 0, new[] { exact }) }, exact);
            var second = MatrixRow(new[] { "A" }, new[] { new CompatibleBlendshape("A", 0, 0, Array.Empty<AnalyzedBinding>()) });
            var model = new BlendshapeMatrixViewModel(new[] { new BlendshapeEntry("A", 0), new BlendshapeEntry("A", 1) }, new[] { first, second });
            Assert.That(model.FilterColumns("", MatrixColumnFilter.Missing), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(model.Columns[1].Index, Is.EqualTo(1));
            Assert.That(model.Rows[0].Cells[0].RelatedBindings.Count, Is.EqualTo(1), "Source and Target association must not duplicate a binding.");
        }

        [Test]
        public void MatrixHandlesZeroRenderersAndZeroSourceShapes()
        {
            var emptyRows = new BlendshapeMatrixViewModel(new[] { new BlendshapeEntry("A", 0) }, Array.Empty<BlendshapeSyncRendererState>());
            Assert.That(emptyRows.Rows, Is.Empty);
            Assert.That(emptyRows.FilterColumns("", MatrixColumnFilter.AllSource), Is.EqualTo(new[] { 0 }));
            Assert.That(emptyRows.FilterColumns("", MatrixColumnFilter.Relevant), Is.Empty);
            Assert.That(emptyRows.FilterColumns("", MatrixColumnFilter.Missing), Is.Empty);
            var emptyShapes = new BlendshapeMatrixViewModel(Array.Empty<BlendshapeEntry>(),
                new[] { MatrixRow(Array.Empty<string>(), Array.Empty<CompatibleBlendshape>()) });
            Assert.That(emptyShapes.Rows[0].Cells, Is.Empty);
            Assert.That(emptyShapes.FilterColumns("", MatrixColumnFilter.AllSource), Is.Empty);
        }
    }
}
