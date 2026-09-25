using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private ExactSyncRequest Request(SkinnedMeshRenderer source, SkinnedMeshRenderer target, string shape = "A")
        {
            var scan = BlendshapeSyncScanner.Scan(source, target.gameObject);
            return new ExactSyncRequest(scan, scan.Renderers.Single(r => r.Renderer == target), shape);
        }

        private static AddSyncResult Add(ExactSyncRequest request) => ModularAvatarBlendshapeSyncWriter.TryAddExactSync(
            request, request.Snapshot.Source, request.Snapshot.TargetRoot);

        [TestCase(false)]
        [TestCase(true)]
        public void WriterAddsOneStandardBindingAndIsIdempotent(bool existing)
        {
            var source = Renderer("Body", "A", "B");
            var target = Renderer("Jacket", "A", "B");
            if (existing) Sync(target, Binding(source, "B"));
            var request = Request(source, target);
            var result = Add(request);
            Assert.That(result.Status, Is.EqualTo(AddSyncStatus.Success), result.Message);
            var sync = target.GetComponent<ModularAvatarBlendshapeSync>();
            Assert.That(target.GetComponents<ModularAvatarBlendshapeSync>().Length, Is.EqualTo(1));
            var binding = sync.Bindings.Last();
            Assert.That(binding.ReferenceMesh.Clone().Get(sync), Is.SameAs(source.gameObject));
            Assert.That(binding.Blendshape, Is.EqualTo("A"));
            Assert.That(binding.LocalBlendshape, Is.Empty);
            Assert.That(binding.RemapCurveIsValid, Is.True);
            Assert.That(binding.RemapCurve.keys.Select(k => k.time), Is.EqualTo(new[] { 0f, 100f }));
            Assert.That(binding.RemapCurve.keys.Select(k => k.value), Is.EqualTo(new[] { 0f, 100f }));
            Assert.That(AnimationUtility.GetKeyLeftTangentMode(binding.RemapCurve, 0), Is.EqualTo(AnimationUtility.TangentMode.Linear));
            Assert.That(Add(request).Status, Is.EqualTo(AddSyncStatus.AlreadyExists));
            Assert.That(sync.Bindings.Count, Is.EqualTo(existing ? 2 : 1));
            using (var serialized = new SerializedObject(sync))
            {
                var item = serialized.FindProperty("Bindings").GetArrayElementAtIndex(sync.Bindings.Count - 1);
                Assert.That(item.FindPropertyRelative("Blendshape").stringValue, Is.EqualTo("A"));
                Assert.That(item.FindPropertyRelative("ReferenceMesh.targetObject").objectReferenceValue, Is.SameAs(source.gameObject));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WriterUndoRedoRestoresComponentAndBinding(bool existing)
        {
            var source = Renderer("Body", "A", "B");
            var target = Renderer("Jacket", "A", "B");
            if (existing) Sync(target, Binding(source, "B"));
            Undo.IncrementCurrentGroup();
            Assert.That(Add(Request(source, target)).Succeeded, Is.True);
            Undo.PerformUndo();
            var sync = target.GetComponent<ModularAvatarBlendshapeSync>();
            if (existing) Assert.That(sync.Bindings.Select(b => b.Blendshape), Is.EqualTo(new[] { "B" }));
            else Assert.That(sync, Is.Null);
            Undo.PerformRedo();
            sync = target.GetComponent<ModularAvatarBlendshapeSync>();
            Assert.That(sync.Bindings.Last().Blendshape, Is.EqualTo("A"));
            Assert.That(sync.Bindings.Last().ReferenceMesh.Clone().Get(sync), Is.SameAs(source.gameObject));
            Assert.That(sync.Bindings.Count, Is.EqualTo(existing ? 2 : 1));
        }

        [TestCase("custom", AddSyncStatus.CustomConflict)]
        [TestCase("outgoing", AddSyncStatus.CustomConflict)]
        [TestCase("broken", AddSyncStatus.BrokenConflict)]
        [TestCase("unresolved", AddSyncStatus.BrokenConflict)]
        [TestCase("other", AddSyncStatus.OtherSourceConflict)]
        public void WriterRejectsConflictsIntroducedAfterScanWithoutMutation(string conflict, AddSyncStatus expected)
        {
            var source = Renderer("Body", "A", "B");
            var target = Renderer("Jacket", "A", "B");
            var sync = Sync(target);
            var request = Request(source, target);
            switch (conflict)
            {
                case "custom": sync.Bindings.Add(Binding(source, "B", "A")); break;
                case "outgoing": sync.Bindings.Add(Binding(source, "A", "B")); break;
                case "broken": sync.Bindings.Add(Binding(source, "Gone", "A")); break;
                case "unresolved": sync.Bindings.Add(Binding(null, "B")); break;
                case "other": sync.Bindings.Add(Binding(Renderer("Other", "A"), "A")); break;
            }
            var before = EditorJsonUtility.ToJson(sync);
            SaveMeshes();
            EditorSceneManager.SaveScene(scene, assetFolder + "/Rejected.unity");
            var group = Undo.GetCurrentGroup();
            Assert.That(Add(request).Status, Is.EqualTo(expected));
            Assert.That(EditorJsonUtility.ToJson(sync), Is.EqualTo(before));
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(group));
            Assert.That(scene.isDirty, Is.False);
            Assert.That(ModularAvatarBlendshapeSyncWriter.CheckSelection(Request(source, target)).Status, Is.EqualTo(expected));
        }

        [Test]
        public void WriterDefaultsMatchMaNormalizedInspectorDefaults()
        {
            var source = Renderer("Body", "A", "B");
            var target = Renderer("Jacket", "A", "B");
            var sync = Sync(target, new BlendshapeBinding {
                ReferenceMesh = new AvatarObjectReference(source.gameObject), Blendshape = "B"
            });
            // Fixture only: reproduce MA's normalization of the Inspector-created raw candidate.
            sync.SendMessage("OnValidate");
            var standard = sync.Bindings[0];
            Assert.That(Add(Request(source, target)).Succeeded, Is.True);
            var added = sync.Bindings[1];
            CollectionAssert.AreEqual(standard.RemapCurve.keys, added.RemapCurve.keys);
            Assert.That(added.RemapCurveIsValid, Is.EqualTo(standard.RemapCurveIsValid));
            Assert.That(added.RemapCurve.preWrapMode, Is.EqualTo(standard.RemapCurve.preWrapMode));
            Assert.That(added.RemapCurve.postWrapMode, Is.EqualTo(standard.RemapCurve.postWrapMode));
            Assert.That(string.IsNullOrEmpty(standard.LocalBlendshape), Is.True);
        }

        [Test]
        public void WriterRejectsNewComponentAfterScanUntilRescan()
        {
            var source = Renderer("Body", "A");
            var target = Renderer("Jacket", "A");
            var request = Request(source, target);
            var sync = Sync(target);
            Assert.That(Add(request).Status, Is.EqualTo(AddSyncStatus.SetupChanged));
            Assert.That(sync.Bindings, Is.Empty);
            Assert.That(Add(Request(source, target)).Succeeded, Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void WriterRejectsShapeLossAfterScan(bool sourceLoss)
        {
            var source = Renderer("Body", "A");
            var target = Renderer("Jacket", "A");
            var request = Request(source, target);
            (sourceLoss ? source : target).sharedMesh.ClearBlendShapes();
            Assert.That(Add(request).Status, Is.EqualTo(sourceLoss ? AddSyncStatus.MissingSourceShape : AddSyncStatus.MissingTargetShape));
            Assert.That(target.GetComponent<ModularAvatarBlendshapeSync>(), Is.Null);
        }

        [Test]
        public void WriterRejectsChangedInputMeshHierarchyAndUnresolvableReferences()
        {
            var source = Renderer("Body", "A");
            var target = Renderer("Jacket", "A");
            var other = Renderer("Other", "A");
            var request = Request(source, target);
            Assert.That(ModularAvatarBlendshapeSyncWriter.TryAddExactSync(request, other, target.gameObject).Status,
                Is.EqualTo(AddSyncStatus.SetupChanged));
            source.sharedMesh = other.sharedMesh;
            Assert.That(Add(request).Status, Is.EqualTo(AddSyncStatus.SetupChanged));
            request = Request(source, target);
            target.transform.SetParent(null);
            Assert.That(Add(request).Status, Is.EqualTo(AddSyncStatus.InvalidReference));
            Assert.That(target.GetComponent<ModularAvatarBlendshapeSync>(), Is.Null);
            Object.DestroyImmediate(target.gameObject);
            Assert.That(Add(request).Succeeded, Is.False);
        }

        [Test]
        public void WriterPreservesEveryExistingBindingAndRemapWithoutNormalizing()
        {
            var source = Renderer("Body", "A", "B");
            var target = Renderer("Jacket", "A", "B");
            var prior = Binding(source, "B");
            prior.RemapCurve = new AnimationCurve(new Keyframe(10, 23, 2, 3), new Keyframe(80, 74, 5, 6));
            prior.RemapCurve.preWrapMode = WrapMode.Loop;
            prior.RemapCurve.postWrapMode = WrapMode.PingPong;
            var sync = Sync(target, prior);
            var oldCurve = sync.Bindings[0].RemapCurve;
            var keys = oldCurve.keys;
            Assert.That(Add(Request(source, target)).Succeeded, Is.True);
            Assert.That(sync.Bindings[0].RemapCurve, Is.SameAs(oldCurve));
            CollectionAssert.AreEqual(keys, sync.Bindings[0].RemapCurve.keys);
            Assert.That(oldCurve.preWrapMode, Is.EqualTo(WrapMode.Loop));
            Assert.That(oldCurve.postWrapMode, Is.EqualTo(WrapMode.PingPong));
            Assert.That(sync.Bindings[0].ReferenceMesh, Is.SameAs(prior.ReferenceMesh));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WriterPrefabOverridesSurviveSaveReloadAndUndoRedo(bool existing)
        {
            var source = Renderer("Body", "A");
            var target = Renderer("Jacket", "A");
            if (existing) Sync(target);
            SaveMeshes();
            var prefabPath = assetFolder + "/Write.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(avatar, prefabPath);
            var assetSource = prefab.transform.Find("Body").GetComponent<SkinnedMeshRenderer>();
            var assetTarget = prefab.transform.Find("Jacket").GetComponent<SkinnedMeshRenderer>();
            Assert.That(Add(Request(assetSource, assetTarget)).Status, Is.EqualTo(AddSyncStatus.UnsupportedObject));
            Object.DestroyImmediate(avatar);
            avatar = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            source = avatar.transform.Find("Body").GetComponent<SkinnedMeshRenderer>();
            target = avatar.transform.Find("Jacket").GetComponent<SkinnedMeshRenderer>();
            var scenePath = assetFolder + "/Write.unity";
            Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
            var bytes = File.ReadAllBytes(prefabPath);
            Assert.That(scene.isDirty, Is.False);
            var request = Request(source, target);
            Assert.That(scene.isDirty, Is.False);
            Assert.That(Add(request).Succeeded, Is.True);
            Assert.That(scene.isDirty, Is.True);
            var sync = target.GetComponent<ModularAvatarBlendshapeSync>();
            if (existing) Assert.That(PrefabUtility.GetPropertyModifications(avatar).Any(p => p.propertyPath.StartsWith("Bindings")), Is.True);
            else Assert.That(PrefabUtility.IsAddedComponentOverride(sync), Is.True);
            Undo.PerformUndo();
            sync = target.GetComponent<ModularAvatarBlendshapeSync>();
            if (existing) Assert.That(sync.Bindings, Is.Empty);
            else Assert.That(sync, Is.Null);
            Undo.PerformRedo();
            Assert.That(target.GetComponent<ModularAvatarBlendshapeSync>().Bindings.Count, Is.EqualTo(1));
            Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(prefabPath));
            EditorSceneManager.CloseScene(scene, true);
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            avatar = scene.GetRootGameObjects().Single();
            source = avatar.transform.Find("Body").GetComponent<SkinnedMeshRenderer>();
            target = avatar.transform.Find("Jacket").GetComponent<SkinnedMeshRenderer>();
            sync = target.GetComponent<ModularAvatarBlendshapeSync>();
            Assert.That(sync.Bindings.Single().ReferenceMesh.Clone().Get(sync), Is.SameAs(source.gameObject));
            Assert.That(BlendshapeSyncScanner.Scan(source, target.gameObject).SyncedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WriterWindowSelectionIsReadOnlyAndWriteUndoRedoRescan()
        {
            var source = Renderer("Body", "A");
            var target = Renderer("Jacket", "A");
            Sync(target);
            SaveMeshes();
            EditorSceneManager.SaveScene(scene, assetFolder + "/WindowWrite.unity");
            window = EditorWindow.GetWindow<BlendshapeSyncMapperWindow>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(BlendshapeSyncMapperWindow).GetField("sourceRenderer", flags).SetValue(window, source);
            typeof(BlendshapeSyncMapperWindow).GetField("targetRoot", flags).SetValue(window, target.gameObject);
            window.Rescan();
            window.Matrix.SelectCell(0, 0);
            window.Repaint();
            yield return null;
            Assert.That(scene.isDirty, Is.False);
            Assert.That(window.Analysis.SyncedCount, Is.Zero);
            Assert.That(window.AddSelectedSync().Succeeded, Is.True);
            Assert.That(window.Analysis.SyncedCount, Is.EqualTo(1));
            Assert.That(window.Matrix.SelectedColumn, Is.Zero);
            Undo.PerformUndo();
            yield return null;
            Assert.That(window.Analysis.SyncedCount, Is.Zero);
            Undo.PerformRedo();
            yield return null;
            Assert.That(window.Analysis.SyncedCount, Is.EqualTo(1));
            Assert.That(window.Matrix.CellContent(0, 0).text, Is.EqualTo("●"));
        }
    }
}
