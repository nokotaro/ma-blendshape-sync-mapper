using System;
using System.Collections.Generic;
using Nokotaro.BlendshapeSyncMapper.Analysis;
using nadena.dev.modular_avatar.core;
using UnityEngine;

namespace Nokotaro.BlendshapeSyncMapper.ModularAvatar
{
    public sealed class ModularAvatarReadResult
    {
        public IReadOnlyList<Component> Components { get; }
        public IReadOnlyList<BlendshapeSyncBindingState> Bindings { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        internal ModularAvatarReadResult(List<Component> components,
            List<BlendshapeSyncBindingState> bindings, List<string> diagnostics)
        {
            Components = components.AsReadOnly();
            Bindings = bindings.AsReadOnly();
            Diagnostics = diagnostics.AsReadOnly();
        }
    }

    public static class ModularAvatarBlendshapeSyncReader
    {
        public static ModularAvatarReadResult Read(SkinnedMeshRenderer target)
        {
            var components = new List<Component>();
            var bindings = new List<BlendshapeSyncBindingState>();
            var diagnostics = new List<string>();
            var syncs = target.GetComponents<ModularAvatarBlendshapeSync>();
            for (var componentIndex = 0; componentIndex < syncs.Length; componentIndex++)
            {
                var sync = syncs[componentIndex];
                components.Add(sync);
                // MA resolves its local renderer with GetComponent on this exact GameObject.
                if (sync.GetComponent<SkinnedMeshRenderer>() != target)
                {
                    diagnostics.Add($"Component {componentIndex}: MA targets another Renderer on this GameObject.");
                    continue;
                }
                if (sync.Bindings == null)
                {
                    diagnostics.Add($"Component {componentIndex}: Bindings list is null.");
                    continue;
                }
                for (var bindingIndex = 0; bindingIndex < sync.Bindings.Count; bindingIndex++)
                    bindings.Add(ReadBinding(sync, componentIndex, bindingIndex, sync.Bindings[bindingIndex]));
            }
            return new ModularAvatarReadResult(components, bindings, diagnostics);
        }

        private static BlendshapeSyncBindingState ReadBinding(ModularAvatarBlendshapeSync component,
            int componentIndex, int bindingIndex, BlendshapeBinding binding)
        {
            GameObject referenceObject = null;
            SkinnedMeshRenderer referenceRenderer = null;
            var issues = BindingIssue.None;
            string error = null;
            if (binding.ReferenceMesh == null)
                issues |= BindingIssue.MissingReference;
            else
            {
                try
                {
                    // Get mutates MA's non-serialized cache. Resolve a public clone to leave even that cache alone.
                    referenceObject = binding.ReferenceMesh.Clone().Get(component);
                    if (referenceObject == null) issues |= BindingIssue.UnresolvedReference;
                    else
                    {
                        referenceRenderer = referenceObject.GetComponent<SkinnedMeshRenderer>();
                        if (referenceRenderer == null) issues |= BindingIssue.MissingReferenceRenderer;
                        else if (referenceRenderer.sharedMesh == null) issues |= BindingIssue.MissingReferenceMesh;
                    }
                }
                catch (Exception exception)
                {
                    issues |= BindingIssue.ReferenceResolutionFailed;
                    error = $"{exception.GetType().Name}: {exception.Message}";
                }
            }
            return new BlendshapeSyncBindingState(component, componentIndex, bindingIndex,
                referenceObject, referenceRenderer, binding.Blendshape, binding.LocalBlendshape,
                binding.RemapCurveIsValid, binding.RemapCurve, issues, error);
        }
    }
}
