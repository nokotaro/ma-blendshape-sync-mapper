using System;
using System.Linq;
using Nokotaro.BlendshapeSyncMapper.Analysis;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nokotaro.BlendshapeSyncMapper.ModularAvatar
{
    public enum AddSyncStatus
    {
        Success, AlreadyExists, CustomConflict, BrokenConflict, OtherSourceConflict,
        MultipleComponents, MissingSourceShape, MissingTargetShape, InvalidReference,
        SetupChanged, UnsupportedObject, Failed
    }

    public sealed class AddSyncResult
    {
        public AddSyncStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status == AddSyncStatus.Success;
        internal AddSyncResult(AddSyncStatus status, string message) { Status = status; Message = message; }
    }

    // An ephemeral request retains the scan's input identity, never a mutable MA binding.
    public sealed class ExactSyncRequest
    {
        public BlendshapeSyncAnalysis Snapshot { get; }
        public BlendshapeSyncRendererState Row { get; }
        public string Shape { get; }
        public ExactSyncRequest(BlendshapeSyncAnalysis snapshot, BlendshapeSyncRendererState row, string shape)
        { Snapshot = snapshot; Row = row; Shape = shape; }
    }

    public static class ModularAvatarBlendshapeSyncWriter
    {
        private const string UndoName = "Add MA Blendshape Sync Binding";
        private static AddSyncResult Result(AddSyncStatus status, string message) => new AddSyncResult(status, message);

        public static AddSyncResult CheckSelection(ExactSyncRequest request)
        {
            try
            {
                if (request?.Snapshot?.Source == null || request.Row?.Renderer == null)
                    return Result(AddSyncStatus.SetupChanged, "The setup changed since the last scan. Please rescan.");
                var check = CheckRow(request.Row, request.Shape);
                if (!check.Succeeded) return check;
                return CheckContext(request.Snapshot.Source, request.Row.Renderer);
            }
            catch (Exception exception) { return Result(AddSyncStatus.InvalidReference, exception.Message); }
        }

        private static AddSyncResult CheckContext(SkinnedMeshRenderer source, SkinnedMeshRenderer target)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(source)
                || EditorUtility.IsPersistent(target) || !target.gameObject.scene.IsValid()
                || PrefabStageUtility.GetPrefabStage(target.gameObject) != null
                || PrefabStageUtility.GetPrefabStage(source.gameObject) != null
                || (target.hideFlags & HideFlags.NotEditable) != 0
                || (target.gameObject.hideFlags & HideFlags.NotEditable) != 0)
                return Result(AddSyncStatus.UnsupportedObject, "Use editable Scene objects / Prefab instances in Edit Mode. Prefab assets and Prefab Mode are not supported.");
            if (source.GetComponent<SkinnedMeshRenderer>() != source || target.GetComponent<SkinnedMeshRenderer>() != target)
                return Result(AddSyncStatus.InvalidReference, "MA would resolve a different Renderer on this GameObject.");
            var rootReference = new AvatarObjectReference { referencePath = AvatarObjectReference.AVATAR_ROOT };
            var root = rootReference.Clone().Get(source);
            if (root == null || root != rootReference.Clone().Get(target))
                return Result(AddSyncStatus.InvalidReference, "Source and Target must belong to the same Avatar root.");
            var reference = new AvatarObjectReference(source.gameObject);
            if (reference.Clone().Get(target) != source.gameObject)
                return Result(AddSyncStatus.InvalidReference, "MA could not resolve the Source reference from Target.");
            var sync = target.GetComponent<ModularAvatarBlendshapeSync>();
            if (sync != null && (sync.hideFlags & HideFlags.NotEditable) != 0)
                return Result(AddSyncStatus.UnsupportedObject, "MA Component is not editable.");
            return Result(AddSyncStatus.Success, "Available, not synced. Add Sync adds exactly one binding.");
        }

        // Used for snapshot-based button eligibility and again against freshly read data before mutation.
        public static AddSyncResult CheckRow(BlendshapeSyncRendererState row, string name)
        {
            if (row.Components.Count > 1) return Result(AddSyncStatus.MultipleComponents,
                "Cannot safely determine which MA Blendshape Sync component to modify.");
            if (row.Diagnostics.Count != 0) return Result(AddSyncStatus.InvalidReference, string.Join("\n", row.Diagnostics));
            var compatible = row.CompatibleShapes.FirstOrDefault(s => s.Name == name);
            if (compatible == null) return Result(AddSyncStatus.MissingTargetShape, "Source or Target same-name shape is unavailable.");
            if (compatible.IsSynced) return Result(AddSyncStatus.AlreadyExists, "Exact same-name sync already exists.");
            // Unresolved bindings cannot reliably be assigned to a cell. Block the whole row conservatively.
            if (row.BrokenCount != 0) return Result(AddSyncStatus.BrokenConflict,
                "Broken binding detected on this Renderer. Addition is disabled until it is resolved.");
            foreach (var b in row.Bindings)
            {
                if (b.Kind == BindingKind.CustomMapping && (b.Binding.TargetName == name
                    || (b.ReferencesSelectedSource && b.Binding.SourceName == name)))
                    return Result(AddSyncStatus.CustomConflict, "Existing custom mapping detected. Automatic addition is disabled.");
                if (b.Binding.TargetName == name && b.Kind == BindingKind.OtherSource)
                    return Result(AddSyncStatus.OtherSourceConflict, "Target shape is already driven by another Source Renderer.");
            }
            return Result(AddSyncStatus.Success, "Available, not synced. Add Sync adds exactly one binding.");
        }

        public static AddSyncResult TryAddExactSync(ExactSyncRequest request,
            SkinnedMeshRenderer currentSource, GameObject currentRoot)
        {
            var group = -1;
            try
            {
                if (request?.Snapshot == null || request.Row == null || string.IsNullOrEmpty(request.Shape))
                    return Result(AddSyncStatus.SetupChanged, "No scanned cell selected. Please rescan.");
                var snapshot = request.Snapshot;
                var source = snapshot.Source;
                var target = request.Row.Renderer;
                if (source == null || target == null || currentRoot == null || currentSource != source
                    || currentRoot != snapshot.TargetRoot || !snapshot.Renderers.Contains(request.Row)
                    || source == target || !target.transform.IsChildOf(currentRoot.transform))
                    return Result(AddSyncStatus.SetupChanged, "The setup changed since the last scan. Please rescan.");
                if (source.sharedMesh == null || source.sharedMesh.GetBlendShapeIndex(request.Shape) < 0)
                    return Result(AddSyncStatus.MissingSourceShape, "Source shape no longer exists. Please rescan.");
                if (target.sharedMesh == null || target.sharedMesh.GetBlendShapeIndex(request.Shape) < 0)
                    return Result(AddSyncStatus.MissingTargetShape, "Target shape no longer exists. Please rescan.");
                if (snapshot.IsStale(currentSource, currentRoot))
                    return Result(AddSyncStatus.SetupChanged, "The setup changed since the last scan. Please rescan.");
                var context = CheckContext(source, target);
                if (!context.Succeeded) return context;
                var reference = new AvatarObjectReference();
                reference.Set(source.gameObject);

                var fresh = BlendshapeSyncScanner.Scan(source, target.gameObject).Renderers.First(r => r.Renderer == target);
                var check = CheckRow(fresh, request.Shape);
                if (!check.Succeeded) return check;
                if (!fresh.Components.SequenceEqual(request.Row.Components))
                    return Result(AddSyncStatus.SetupChanged, "MA Components changed since the last scan. Please rescan.");
                var originalCheck = CheckRow(request.Row, request.Shape);
                if (!originalCheck.Succeeded) return originalCheck;
                var sync = target.GetComponent<ModularAvatarBlendshapeSync>();
                if (sync != null && (sync.hideFlags & HideFlags.NotEditable) != 0)
                    return Result(AddSyncStatus.UnsupportedObject, "MA Component is not editable.");

                // Match MA 1.18.7 OnValidate's normalized default, only for the new binding.
                // Do not invoke OnValidate / SerializedObject.Apply: they normalize existing curves too.
                var curve = new AnimationCurve();
                curve.AddKey(0, 0);
                curve.AddKey(100, 100);
                for (var i = 0; i < 2; i++)
                {
                    AnimationUtility.SetKeyBroken(curve, i, true);
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }
                Undo.IncrementCurrentGroup();
                group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
                if (sync == null) sync = Undo.AddComponent<ModularAvatarBlendshapeSync>(target.gameObject);
                if (sync == null) throw new InvalidOperationException("Could not create MA Blendshape Sync.");
                Undo.RecordObject(sync, UndoName);
                sync.Bindings.Add(new BlendshapeBinding {
                    ReferenceMesh = reference, Blendshape = request.Shape, LocalBlendshape = "",
                    RemapCurve = curve, RemapCurveIsValid = true
                });
                if (PrefabUtility.IsPartOfPrefabInstance(sync))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(sync);
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(group);
                Undo.IncrementCurrentGroup();
                return Result(AddSyncStatus.Success, "Added one exact same-name binding.");
            }
            catch (Exception exception)
            {
                var message = $"Add Sync failed: {exception.GetType().Name}: {exception.Message}";
                if (group >= 0)
                {
                    try { Undo.FlushUndoRecordObjects(); Undo.RevertAllDownToGroup(group); }
                    catch (Exception rollback) { message += $"\nRollback failed: {rollback.Message}. Inspect the Target before continuing."; }
                    finally { Undo.IncrementCurrentGroup(); }
                }
                return Result(AddSyncStatus.Failed, message);
            }
        }
    }
}
