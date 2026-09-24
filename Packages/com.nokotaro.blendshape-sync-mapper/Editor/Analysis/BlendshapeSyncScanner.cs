using System;
using System.Collections.Generic;
using Nokotaro.BlendshapeSyncMapper.ModularAvatar;
using UnityEngine;

namespace Nokotaro.BlendshapeSyncMapper.Analysis
{
    public static class BlendshapeSyncScanner
    {
        private sealed class ShapeCatalog
        {
            public readonly List<BlendshapeEntry> Entries = new List<BlendshapeEntry>();
            public readonly Dictionary<string, int> Indices = new Dictionary<string, int>(StringComparer.Ordinal);

            public ShapeCatalog(Mesh mesh)
            {
                if (mesh == null) return;
                for (var index = 0; index < mesh.blendShapeCount; index++)
                {
                    var name = mesh.GetBlendShapeName(index);
                    Entries.Add(new BlendshapeEntry(name, index));
                    // Preserve the raw entries, but use Unity's own name resolution for name-based bindings.
                    if (name != null && !Indices.ContainsKey(name)) Indices.Add(name, mesh.GetBlendShapeIndex(name));
                }
            }

            public bool Contains(string name) => name != null && Indices.TryGetValue(name, out var index) && index >= 0;
        }

        public static string GetInputError(SkinnedMeshRenderer source, GameObject targetRoot)
        {
            if (source == null) return "Select a Source Renderer.";
            if (targetRoot == null) return "Select a Target Root.";
            return source.sharedMesh == null ? "Source Renderer has no sharedMesh. Assign a Mesh before scanning." : null;
        }

        public static BlendshapeSyncAnalysis Scan(SkinnedMeshRenderer source, GameObject targetRoot)
        {
            var inputError = GetInputError(source, targetRoot);
            if (inputError != null) throw new ArgumentException(inputError);

            var catalogs = new Dictionary<Mesh, ShapeCatalog>();
            var empty = new ShapeCatalog(null);
            ShapeCatalog Catalog(Mesh mesh)
            {
                if (mesh == null) return empty;
                if (!catalogs.TryGetValue(mesh, out var catalog)) catalogs.Add(mesh, catalog = new ShapeCatalog(mesh));
                return catalog;
            }

            var sourceShapes = Catalog(source.sharedMesh);
            var renderers = new List<BlendshapeSyncRendererState>();
            foreach (var target in targetRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (target == source) continue;
                var targetShapes = Catalog(target.sharedMesh);
                var read = ModularAvatarBlendshapeSyncReader.Read(target);
                var bindings = new List<AnalyzedBinding>();
                var byTargetName = new Dictionary<string, List<AnalyzedBinding>>(StringComparer.Ordinal);
                foreach (var binding in read.Bindings)
                {
                    var issues = binding.Issues;
                    if (binding.ReferenceMesh != null && !Catalog(binding.ReferenceMesh).Contains(binding.SourceName))
                        issues |= BindingIssue.MissingSourceBlendshape;
                    if (target.sharedMesh == null) issues |= BindingIssue.MissingTargetMesh;
                    else if (!targetShapes.Contains(binding.TargetName)) issues |= BindingIssue.MissingTargetBlendshape;
                    var analyzed = new AnalyzedBinding(binding, issues, binding.ReferenceObject == source.gameObject);
                    bindings.Add(analyzed);
                    if (binding.TargetName == null) continue;
                    if (!byTargetName.TryGetValue(binding.TargetName, out var list))
                        byTargetName.Add(binding.TargetName, list = new List<AnalyzedBinding>());
                    list.Add(analyzed);
                }

                var compatible = new List<CompatibleBlendshape>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var shape in sourceShapes.Entries)
                {
                    if (!seen.Add(shape.Name) || !sourceShapes.Contains(shape.Name) || !targetShapes.Contains(shape.Name)) continue;
                    byTargetName.TryGetValue(shape.Name, out var existing);
                    compatible.Add(new CompatibleBlendshape(shape.Name, sourceShapes.Indices[shape.Name],
                        targetShapes.Indices[shape.Name], existing ?? new List<AnalyzedBinding>()));
                }
                renderers.Add(new BlendshapeSyncRendererState(target, HierarchyPath(target.transform),
                    sourceShapes.Entries.Count, targetShapes.Entries, compatible, bindings, read.Components, read.Diagnostics));
            }
            return new BlendshapeSyncAnalysis(source, targetRoot, sourceShapes.Entries, renderers);
        }

        private static string HierarchyPath(Transform transform)
        {
            var segments = new List<string>();
            while (transform != null)
            {
                segments.Add($"{transform.name} [{transform.GetSiblingIndex()}]");
                transform = transform.parent;
            }
            segments.Reverse();
            return string.Join("/", segments);
        }
    }
}
