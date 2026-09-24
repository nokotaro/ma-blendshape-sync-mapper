using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nokotaro.BlendshapeSyncMapper.Analysis
{
    public sealed class BlendshapeEntry
    {
        public string Name { get; }
        public int Index { get; }
        public BlendshapeEntry(string name, int index) { Name = name; Index = index; }
    }

    public sealed class CompatibleBlendshape
    {
        public string Name { get; }
        public int SourceIndex { get; }
        public int TargetIndex { get; }
        public IReadOnlyList<AnalyzedBinding> ExistingTargetBindings { get; }
        public bool IsSynced { get; }
        public bool HasExistingTargetBinding => ExistingTargetBindings.Count != 0;

        public CompatibleBlendshape(string name, int sourceIndex, int targetIndex,
            IEnumerable<AnalyzedBinding> existingBindings)
        {
            Name = name;
            SourceIndex = sourceIndex;
            TargetIndex = targetIndex;
            ExistingTargetBindings = existingBindings.ToList().AsReadOnly();
            IsSynced = ExistingTargetBindings.Any(b => b.Kind == BindingKind.ExactSameName);
        }
    }

    public sealed class BlendshapeSyncRendererState
    {
        public SkinnedMeshRenderer Renderer { get; }
        public Mesh Mesh { get; }
        public string DisplayName { get; }
        public string HierarchyPath { get; }
        public int RendererInstanceId { get; }
        public int SourceBlendshapeCount { get; }
        public IReadOnlyList<BlendshapeEntry> TargetBlendshapes { get; }
        public IReadOnlyList<CompatibleBlendshape> CompatibleShapes { get; }
        public IReadOnlyList<AnalyzedBinding> Bindings { get; }
        public IReadOnlyList<Component> Components { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public int CompatibleCount => CompatibleShapes.Count;
        public int SyncedCount { get; }
        public int MissingCount => CompatibleCount - SyncedCount;
        public int CustomCount { get; }
        public int BrokenCount { get; }

        public BlendshapeSyncRendererState(SkinnedMeshRenderer renderer, string hierarchyPath, int sourceCount,
            IEnumerable<BlendshapeEntry> shapes, IEnumerable<CompatibleBlendshape> compatible,
            IEnumerable<AnalyzedBinding> bindings, IEnumerable<Component> components, IEnumerable<string> diagnostics)
        {
            Renderer = renderer;
            Mesh = renderer.sharedMesh;
            DisplayName = renderer.name;
            HierarchyPath = hierarchyPath;
            RendererInstanceId = renderer.GetInstanceID();
            SourceBlendshapeCount = sourceCount;
            TargetBlendshapes = shapes.ToList().AsReadOnly();
            CompatibleShapes = compatible.ToList().AsReadOnly();
            Bindings = bindings.ToList().AsReadOnly();
            Components = components.ToList().AsReadOnly();
            Diagnostics = diagnostics.ToList().AsReadOnly();
            SyncedCount = CompatibleShapes.Count(shape => shape.IsSynced);
            CustomCount = Bindings.Count(binding => binding.Kind == BindingKind.CustomMapping);
            BrokenCount = Bindings.Count(binding => binding.Kind == BindingKind.Broken);
        }
    }
}
