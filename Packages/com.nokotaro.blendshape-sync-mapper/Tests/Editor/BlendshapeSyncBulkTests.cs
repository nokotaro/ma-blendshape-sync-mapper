using System;
using System.Collections;
using System.IO;
using System.Linq;
using Nokotaro.BlendshapeSyncMapper.Analysis;
using Nokotaro.BlendshapeSyncMapper.ModularAvatar;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Nokotaro.BlendshapeSyncMapper.Tests
{
    public sealed partial class BlendshapeSyncScannerTests
    {
        private BulkAddPreview Preview(SkinnedMeshRenderer source)
            => BulkAddPreview.Create(BlendshapeSyncScanner.Scan(source, avatar));

        private BulkAddResult Bulk(BulkAddPreview preview, SkinnedMeshRenderer source)
            => BlendshapeSyncBulkAdd.Execute(preview, source, avatar);

        [TestCase(false)]
        [TestCase(true)]
        public void BulkSharesComponentsPreservesOrderAndUndoesRedoesTogether(bool existing)
        {
            var source = Renderer("Body", "C", "A", "B", "Prior");
            var first = Renderer("Jacket", "A", "B", "C", "Prior");
            var second = Renderer("Inner", "A", "B", "C");
            if (existing) Sync(first, Binding(source, "Prior"));
            var preview = Preview(source);
            Assert.That(preview.MissingCount, Is.EqualTo(preview.SafeCount + preview.ReviewCount));
            Assert.That(preview.SafeCount, Is.EqualTo(existing ? 6 : 7));
            Assert.That(preview.Candidates.Take(3).Select(c => c.Request.Shape), Is.EqualTo(new[] { "C", "A", "B" }));
            var result = Bulk(preview, source);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(first.GetComponents<ModularAvatarBlendshapeSync>().Length, Is.EqualTo(1));
            Assert.That(second.GetComponents<ModularAvatarBlendshapeSync>().Length, Is.EqualTo(1));
            Assert.That(first.GetComponent<ModularAvatarBlendshapeSync>().Bindings.Select(b => b.Blendshape),
                Is.EqualTo(existing ? new[] { "Prior", "C", "A", "B" } : new[] { "C", "A", "B", "Prior" }));
            Undo.PerformUndo();
            Assert.That(second.GetComponent<ModularAvatarBlendshapeSync>(), Is.Null);
            if (existing) Assert.That(first.GetComponent<ModularAvatarBlendshapeSync>().Bindings.Single().Blendshape, Is.EqualTo("Prior"));
            else Assert.That(first.GetComponent<ModularAvatarBlendshapeSync>(), Is.Null);
            Undo.PerformRedo();
            Assert.That(BlendshapeSyncScanner.Scan(source, avatar).SyncedCount, Is.EqualTo(7));
            Assert.That(Bulk(preview, source).Succeeded, Is.False);
            Assert.That(first.GetComponent<ModularAvatarBlendshapeSync>().Bindings.Count, Is.EqualTo(4));
        }

        [TestCase("custom", AddSyncStatus.CustomConflict)]
        [TestCase("broken", AddSyncStatus.BrokenConflict)]
        [TestCase("other", AddSyncStatus.OtherSourceConflict)]
        public void BulkPreviewSeparatesReviewWithoutChangingScannerMeaning(string kind, AddSyncStatus expected)
        {
            var source = Renderer("Body", "A", "B", "C");
            var safe = Renderer("Safe", "B", "C");
            var review = Renderer("Review", "A");
            Sync(review, kind == "custom" ? Binding(source, "B", "A")
                : kind == "broken" ? Binding(null, "A") : Binding(Renderer("Other"), "A"));
            if (kind == "other")
            {
                var other = avatar.transform.Find("Other").GetComponent<SkinnedMeshRenderer>();
                other.sharedMesh = source.sharedMesh;
                other.gameObject.SetActive(false);
                // Keep the additional source outside the scanned Target Root while retaining the avatar root.
                safe.transform.SetParent(review.transform);
                var scan = BlendshapeSyncScanner.Scan(source, review.gameObject);
                var previewOther = BulkAddPreview.Create(scan);
                Assert.That(previewOther.ReviewCount, Is.EqualTo(1));
                Assert.That(previewOther.Candidates.First(c => !c.Eligibility.Succeeded).Eligibility.Status, Is.EqualTo(expected));
                Assert.That(BlendshapeSyncBulkAdd.Execute(previewOther, source, review.gameObject).AddedCount, Is.EqualTo(2));
                return;
            }
            var preview = Preview(source);
            Assert.That(preview.SafeCount, Is.EqualTo(2));
            Assert.That(preview.ReviewCount, Is.EqualTo(1));
            Assert.That(preview.MissingCount, Is.EqualTo(3));
            Assert.That(preview.Candidates.Last().Eligibility.Status, Is.EqualTo(expected));
            Assert.That(Bulk(preview, source).AddedCount, Is.EqualTo(2));
            Assert.That(Preview(source).ReviewCount, Is.EqualTo(1));
            Assert.That(review.GetComponent<ModularAvatarBlendshapeSync>().Bindings.Count, Is.EqualTo(1));
        }

        [TestCase("exact")]
        [TestCase("shape")]
        [TestCase("component")]
        [TestCase("source")]
        public void BulkPreflightRejectsStalePreviewBeforeAnyWrites(string change)
        {
            var source = Renderer("Body", "A", "B", "C");
            var first = Renderer("First", "A");
            var last = Renderer("Last", "B", "C");
            var sync = Sync(last);
            var preview = Preview(source);
            if (change == "exact") sync.Bindings.Add(Binding(source, "C"));
            if (change == "shape") last.sharedMesh.ClearBlendShapes();
            if (change == "component") Sync(first);
            SaveMeshes();
            EditorSceneManager.SaveScene(scene, assetFolder + "/Preflight.unity");
            var group = Undo.GetCurrentGroup();
            var result = Bulk(preview, change == "source" ? last : source);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.AddedCount, Is.Zero);
            Assert.That(sync.Bindings.Count, Is.EqualTo(change == "exact" ? 1 : 0));
            Assert.That(first.GetComponent<ModularAvatarBlendshapeSync>()?.Bindings.Count ?? 0, Is.Zero);
            Assert.That(scene.isDirty, Is.False);
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(group));
        }

        [Test]
        public void BulkNormalizesDuplicateRendererShapeCandidates()
        {
            var source = Renderer("Body", "A");
            Renderer("Jacket", "A");
            var scan = BlendshapeSyncScanner.Scan(source, avatar);
            var duplicate = new BlendshapeSyncAnalysis(source, avatar, scan.SourceBlendshapes,
                new[] { scan.Renderers[0], scan.Renderers[0] });
            var preview = BulkAddPreview.Create(duplicate);
            Assert.That(preview.SafeCount, Is.EqualTo(1));
            Assert.That(Bulk(preview, source).AddedCount, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BulkFaultInjectionRollsBackEveryWriteAndPreservesPreviousUndo(bool existing)
        {
            var source = Renderer("Body", "A", "B", "Prior");
            var first = Renderer("First", "A", "B", "Prior");
            var second = Renderer("Second", "A", "B");
            if (existing)
            {
                var prior = Binding(source, "Prior");
                prior.RemapCurve = new AnimationCurve(new Keyframe(10, 23, 2, 3), new Keyframe(80, 74, 5, 6));
                prior.RemapCurve.preWrapMode = WrapMode.Loop;
                Sync(first, prior);
            }
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(source.gameObject, "Previous user edit");
            source.gameObject.name = "Renamed Body";
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            var before = existing ? EditorJsonUtility.ToJson(first.GetComponent<ModularAvatarBlendshapeSync>()) : null;
            var result = BlendshapeSyncBulkAdd.Execute(Preview(source), source, avatar,
                count => { if (count == 4) throw new InvalidOperationException("Injected fourth-write failure"); });
            Assert.That(result.Succeeded, Is.False);
            StringAssert.Contains("rolled back", result.Message);
            Assert.That(second.GetComponent<ModularAvatarBlendshapeSync>(), Is.Null);
            if (existing) Assert.That(EditorJsonUtility.ToJson(first.GetComponent<ModularAvatarBlendshapeSync>()), Is.EqualTo(before));
            else Assert.That(first.GetComponent<ModularAvatarBlendshapeSync>(), Is.Null);
            Assert.That(source.gameObject.name, Is.EqualTo("Renamed Body"));
            Undo.PerformUndo();
            Assert.That(source.gameObject.name, Is.EqualTo("Body"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BulkPrefabOverridesSaveReloadAndRollback(bool existing)
        {
            var source = Renderer("Body", "A", "B", "C");
            var target = Renderer("Jacket", "A", "B", "C");
            if (existing) Sync(target);
            SaveMeshes();
            var prefabPath = assetFolder + "/Bulk.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(avatar, prefabPath);
            Object.DestroyImmediate(avatar);
            avatar = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            source = avatar.transform.Find("Body").GetComponent<SkinnedMeshRenderer>();
            target = avatar.transform.Find("Jacket").GetComponent<SkinnedMeshRenderer>();
            var scenePath = assetFolder + "/Bulk.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            var bytes = File.ReadAllBytes(prefabPath);
            var preview = Preview(source);
            Assert.That(scene.isDirty, Is.False);
            var failure = BlendshapeSyncBulkAdd.Execute(preview, source, avatar,
                count => { if (count == 2) throw new Exception("Prefab rollback test"); });
            Assert.That(failure.Succeeded, Is.False);
            if (existing) Assert.That(target.GetComponent<ModularAvatarBlendshapeSync>().Bindings, Is.Empty);
            else Assert.That(target.GetComponent<ModularAvatarBlendshapeSync>(), Is.Null);
            Assert.That(PrefabUtility.GetAddedComponents(avatar), Is.Empty);
            Assert.That(PrefabUtility.GetPropertyModifications(avatar).Any(p => p.propertyPath.StartsWith("Bindings")), Is.False);
            var result = Bulk(Preview(source), source);
            Assert.That(result.Succeeded, Is.True, result.Message);
            if (existing) Assert.That(PrefabUtility.GetPropertyModifications(avatar).Any(p => p.propertyPath.StartsWith("Bindings")), Is.True);
            else Assert.That(PrefabUtility.IsAddedComponentOverride(target.GetComponent<ModularAvatarBlendshapeSync>()), Is.True);
            Undo.PerformUndo();
            Undo.PerformRedo();
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            avatar = scene.GetRootGameObjects().Single();
            source = avatar.transform.Find("Body").GetComponent<SkinnedMeshRenderer>();
            Assert.That(BlendshapeSyncScanner.Scan(source, avatar).SyncedCount, Is.EqualTo(3));
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(prefabPath));
        }

        [Test]
        public void BulkSixtyBindingsTiming()
        {
            var names = Enumerable.Range(0, 10).Select(i => "Shape" + i).ToArray();
            var source = Renderer("Body", names);
            for (var i = 0; i < 6; i++) Renderer("Target" + i, names);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var preview = Preview(source);
            var previewMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            var result = Bulk(preview, source);
            watch.Stop();
            Assert.That(result.AddedCount, Is.EqualTo(60), result.Message);
            TestContext.WriteLine($"Bulk 60: scan+preview {previewMs:F2} ms; preflight+apply {watch.Elapsed.TotalMilliseconds:F2} ms");
        }

        [UnityTest]
        public IEnumerator BulkWindowPreviewIsReadOnlyAndRescansAfterWriteUndoRedo()
        {
            var source = Renderer("Body", "A", "B", "C");
            Renderer("Jacket", "A", "B", "C");
            SaveMeshes();
            EditorSceneManager.SaveScene(scene, assetFolder + "/BulkWindow.unity");
            window = EditorWindow.GetWindow<BlendshapeSyncMapperWindow>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(BlendshapeSyncMapperWindow).GetField("sourceRenderer", flags).SetValue(window, source);
            typeof(BlendshapeSyncMapperWindow).GetField("targetRoot", flags).SetValue(window, avatar);
            window.Rescan();
            window.SetColumnFilter("A", UI.MatrixColumnFilter.Missing);
            typeof(BlendshapeSyncMapperWindow).GetField("reviewingBulk", flags).SetValue(window, true);
            window.Repaint();
            yield return null;
            Assert.That(scene.isDirty, Is.False);
            Assert.That(window.BulkPreview.SafeCount, Is.EqualTo(3), "Preview ignores the Matrix filter.");
            Assert.That(window.AddSafeSyncs().AddedCount, Is.EqualTo(3));
            Assert.That(window.Analysis.SyncedCount, Is.EqualTo(3));
            Assert.That(window.BulkPreview.SafeCount, Is.Zero);
            Undo.PerformUndo();
            yield return null;
            Assert.That(window.Analysis.SyncedCount, Is.Zero);
            Assert.That(window.BulkPreview.SafeCount, Is.EqualTo(3));
            Undo.PerformRedo();
            yield return null;
            Assert.That(window.Analysis.SyncedCount, Is.EqualTo(3));
        }
    }
}
