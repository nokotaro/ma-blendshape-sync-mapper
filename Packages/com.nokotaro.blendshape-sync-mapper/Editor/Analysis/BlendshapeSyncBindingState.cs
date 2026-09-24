using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nokotaro.BlendshapeSyncMapper.Analysis
{
    [Flags]
    public enum BindingIssue
    {
        None = 0,
        MissingReference = 1,
        UnresolvedReference = 2,
        ReferenceResolutionFailed = 4,
        MissingReferenceRenderer = 8,
        MissingReferenceMesh = 16,
        MissingSourceBlendshape = 32,
        MissingTargetMesh = 64,
        MissingTargetBlendshape = 128
    }

    public enum BindingKind { ExactSameName, CustomMapping, OtherSource, Broken }

    // A detached record. It never exposes an MA binding or its mutable curve.
    public sealed class BlendshapeSyncBindingState
    {
        public Component Component { get; }
        public int ComponentIndex { get; }
        public int BindingIndex { get; }
        public GameObject ReferenceObject { get; }
        public SkinnedMeshRenderer ReferenceRenderer { get; }
        public Mesh ReferenceMesh { get; }
        public string SourceName { get; }
        public string SerializedLocalName { get; }
        public string TargetName { get; }
        public bool RemapCurveIsValid { get; }
        public bool HasRemapCurve { get; }
        public IReadOnlyList<Keyframe> RemapKeys { get; }
        public WrapMode PreWrapMode { get; }
        public WrapMode PostWrapMode { get; }
        public BindingIssue Issues { get; }
        public string ResolutionError { get; }

        public BlendshapeSyncBindingState(Component component, int componentIndex, int bindingIndex,
            GameObject referenceObject, SkinnedMeshRenderer referenceRenderer, string sourceName,
            string serializedLocalName, bool remapCurveIsValid, AnimationCurve curve,
            BindingIssue issues, string resolutionError)
        {
            Component = component;
            ComponentIndex = componentIndex;
            BindingIndex = bindingIndex;
            ReferenceObject = referenceObject;
            ReferenceRenderer = referenceRenderer;
            ReferenceMesh = referenceRenderer != null ? referenceRenderer.sharedMesh : null;
            SourceName = sourceName;
            SerializedLocalName = serializedLocalName;
            TargetName = string.IsNullOrWhiteSpace(serializedLocalName) ? sourceName : serializedLocalName;
            RemapCurveIsValid = remapCurveIsValid;
            HasRemapCurve = curve != null;
            RemapKeys = Array.AsReadOnly(curve != null ? curve.keys : Array.Empty<Keyframe>());
            PreWrapMode = curve != null ? curve.preWrapMode : default;
            PostWrapMode = curve != null ? curve.postWrapMode : default;
            Issues = issues;
            ResolutionError = resolutionError;
        }
    }

    public sealed class AnalyzedBinding
    {
        public BlendshapeSyncBindingState Binding { get; }
        public BindingIssue Issues { get; }
        public BindingKind Kind { get; }
        public bool ReferencesSelectedSource { get; }

        public AnalyzedBinding(BlendshapeSyncBindingState binding, BindingIssue issues, bool selectedSource)
        {
            Binding = binding;
            Issues = issues;
            ReferencesSelectedSource = selectedSource;
            Kind = issues != BindingIssue.None ? BindingKind.Broken
                : !string.Equals(binding.SourceName, binding.TargetName, StringComparison.Ordinal)
                    ? BindingKind.CustomMapping
                    : selectedSource ? BindingKind.ExactSameName : BindingKind.OtherSource;
        }
    }
}
