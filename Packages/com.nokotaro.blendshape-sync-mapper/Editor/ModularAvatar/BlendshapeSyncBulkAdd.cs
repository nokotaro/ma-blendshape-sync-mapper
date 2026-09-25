using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Nokotaro.BlendshapeSyncMapper.Analysis;
using UnityEditor;
using UnityEngine;

namespace Nokotaro.BlendshapeSyncMapper.ModularAvatar
{
    public sealed class BulkAddCandidate
    {
        public ExactSyncRequest Request { get; }
        public AddSyncResult Eligibility { get; }
        internal BulkAddCandidate(ExactSyncRequest request, AddSyncResult eligibility)
        { Request = request; Eligibility = eligibility; }
    }

    // Disposable UI data derived from the scan. Never serialized or used as a mapping database.
    public sealed class BulkAddPreview
    {
        public IReadOnlyList<BulkAddCandidate> Candidates { get; }
        public int SafeCount { get; }
        public int ReviewCount => Candidates.Count - SafeCount;
        public int MissingCount => Candidates.Count;

        private BulkAddPreview(List<BulkAddCandidate> candidates, int safeCount)
        { Candidates = new ReadOnlyCollection<BulkAddCandidate>(candidates); SafeCount = safeCount; }

        public static BulkAddPreview Create(BlendshapeSyncAnalysis snapshot)
        {
            var candidates = new List<BulkAddCandidate>();
            var seen = new Dictionary<SkinnedMeshRenderer, HashSet<string>>();
            var safe = 0;
            if (snapshot != null)
                foreach (var row in snapshot.Renderers)
                {
                    if (row.Renderer == null) continue;
                    if (!seen.TryGetValue(row.Renderer, out var names))
                        seen.Add(row.Renderer, names = new HashSet<string>(StringComparer.Ordinal));
                    // Scanner supplies hierarchy order and unique compatible names in source index order.
                    foreach (var shape in row.CompatibleShapes)
                    {
                        if (shape.IsSynced || !names.Add(shape.Name)) continue;
                        var request = new ExactSyncRequest(snapshot, row, shape.Name);
                        var check = ModularAvatarBlendshapeSyncWriter.CheckSelection(request);
                        candidates.Add(new BulkAddCandidate(request, check));
                        if (check.Succeeded) safe++;
                    }
                }
            return new BulkAddPreview(candidates, safe);
        }
    }

    public sealed class BulkAddResult
    {
        public bool Succeeded { get; }
        public int AddedCount { get; }
        public string Message { get; }
        internal BulkAddResult(bool succeeded, int addedCount, string message)
        { Succeeded = succeeded; AddedCount = addedCount; Message = message; }
    }

    public static class BlendshapeSyncBulkAdd
    {
        public static BulkAddResult Execute(BulkAddPreview preview, SkinnedMeshRenderer source, GameObject root)
            => Execute(preview, source, root, null);

        // Per-call seam: tests can fail after N successful writes; production never supplies a callback.
        internal static BulkAddResult Execute(BulkAddPreview preview, SkinnedMeshRenderer source, GameObject root,
            Action<int> afterWrite)
        {
            if (preview == null || preview.SafeCount == 0)
                return new BulkAddResult(false, 0, "No safe missing syncs to add.");
            foreach (var candidate in preview.Candidates)
            {
                if (!candidate.Eligibility.Succeeded) continue;
                var check = ModularAvatarBlendshapeSyncWriter.Preflight(candidate.Request, source, root);
                if (!check.Succeeded)
                    return new BulkAddResult(false, 0, "The setup changed since the preview. No bindings were added. Review the refreshed preview.\n"
                        + candidate.Request.Row.HierarchyPath + " / " + candidate.Request.Shape + ": " + check.Message);
            }

            Action restoreOriginal;
            try { restoreOriginal = ModularAvatarBlendshapeSyncWriter.CaptureRollbackState(
                preview.Candidates.Where(c => c.Eligibility.Succeeded).Select(c => c.Request)); }
            catch (Exception exception) { return new BulkAddResult(false, 0, "Could not prepare rollback. No bindings were added.\n" + exception.Message); }
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            const string undoName = "Add Safe MA Blendshape Sync Bindings";
            Undo.SetCurrentGroupName(undoName);
            var created = new Dictionary<SkinnedMeshRenderer, Component>();
            var added = 0;
            try
            {
                foreach (var candidate in preview.Candidates)
                {
                    if (!candidate.Eligibility.Succeeded) continue;
                    var result = ModularAvatarBlendshapeSyncWriter.TryAddExactSync(candidate.Request, source, root, created);
                    if (!result.Succeeded) throw new InvalidOperationException(result.Message);
                    added++;
                    afterWrite?.Invoke(added);
                }
                Undo.FlushUndoRecordObjects();
                Undo.SetCurrentGroupName(undoName);
                Undo.CollapseUndoOperations(group);
                return new BulkAddResult(true, added, $"Added {added} Blendshape Sync bindings.");
            }
            catch (Exception exception)
            {
                var message = "Bulk Add failed. All changes were rolled back.\n" + exception.Message;
                try { Undo.FlushUndoRecordObjects(); Undo.RevertAllDownToGroup(group); restoreOriginal(); }
                catch (Exception rollback)
                { message = "Bulk Add and rollback failed. Inspect the Scene before continuing.\n" + exception.Message + "\n" + rollback.Message; }
                return new BulkAddResult(false, 0, message);
            }
            finally { Undo.IncrementCurrentGroup(); }
        }
    }
}
