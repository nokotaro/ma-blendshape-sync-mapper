using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nokotaro.BlendshapeSyncMapper.Analysis
{
    // Window-owned, non-serialized and rebuilt on every explicit scan.
    public sealed class BlendshapeSyncAnalysis
    {
        private readonly Object[] observedObjects;
        public SkinnedMeshRenderer Source { get; }
        public GameObject TargetRoot { get; }
        public Mesh SourceMesh { get; }
        public IReadOnlyList<BlendshapeEntry> SourceBlendshapes { get; }
        public IReadOnlyList<BlendshapeSyncRendererState> Renderers { get; }
        public int CompatibleCount { get; }
        public int SyncedCount { get; }
        public int MissingCount { get; }
        public int CustomCount { get; }
        public int BrokenCount { get; }

        public BlendshapeSyncAnalysis(SkinnedMeshRenderer source, GameObject targetRoot,
            IEnumerable<BlendshapeEntry> shapes, IEnumerable<BlendshapeSyncRendererState> renderers)
        {
            Source = source;
            TargetRoot = targetRoot;
            SourceMesh = source.sharedMesh;
            SourceBlendshapes = shapes.ToList().AsReadOnly();
            Renderers = renderers.ToList().AsReadOnly();
            CompatibleCount = Renderers.Sum(r => r.CompatibleCount);
            SyncedCount = Renderers.Sum(r => r.SyncedCount);
            MissingCount = Renderers.Sum(r => r.MissingCount);
            CustomCount = Renderers.Sum(r => r.CustomCount);
            BrokenCount = Renderers.Sum(r => r.BrokenCount);

            var observed = new HashSet<Object> { source, targetRoot, SourceMesh };
            foreach (var row in Renderers)
            {
                observed.Add(row.Renderer);
                observed.Add(row.Mesh);
                foreach (var component in row.Components) observed.Add(component);
                foreach (var binding in row.Bindings)
                {
                    observed.Add(binding.Binding.ReferenceObject);
                    observed.Add(binding.Binding.ReferenceRenderer);
                    observed.Add(binding.Binding.ReferenceMesh);
                }
            }
            // Originally unresolved references are valid broken findings, not stale snapshots.
            observedObjects = observed.Where(obj => obj != null).ToArray();
        }

        public bool IsStale(SkinnedMeshRenderer source, GameObject targetRoot)
        {
            return source != Source || targetRoot != TargetRoot || observedObjects.Any(obj => obj == null)
                || Source.sharedMesh != SourceMesh || Renderers.Any(row => row.Renderer.sharedMesh != row.Mesh);
        }
    }
}
