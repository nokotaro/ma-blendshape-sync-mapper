using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Nokotaro.BlendshapeSyncMapper.Analysis;
using Nokotaro.BlendshapeSyncMapper.ModularAvatar;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace Nokotaro.BlendshapeSyncMapper.Tests
{
    public sealed class BlendshapeSyncScannerTests
    {
        private Scene scene;
        private Scene previousScene;
        private GameObject avatar;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private string assetFolder;
        private BlendshapeSyncMapperWindow window;
        private Object previousSelection;

        [SetUp]
        public void SetUp()
        {
            previousScene = SceneManager.GetActiveScene();
            previousSelection = Selection.activeObject;
            // Batch tests run in a dedicated Editor process. Interactive tests keep existing scenes open.
            // Save untitled scenes before running interactively: Unity disallows adding a scene to them.
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            avatar = new GameObject("Avatar");
            avatar.AddComponent<VRCAvatarDescriptor>();
        }

        [TearDown]
        public void TearDown()
        {
            if (window != null) window.Close();
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            foreach (var mesh in meshes) if (mesh != null && !EditorUtility.IsPersistent(mesh)) Object.DestroyImmediate(mesh);
            meshes.Clear();
            if (assetFolder != null) AssetDatabase.DeleteAsset(assetFolder);
            assetFolder = null;
            Selection.activeObject = previousSelection;
        }

        private SkinnedMeshRenderer Renderer(string name, params string[] shapes)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(avatar.transform);
            var renderer = obj.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh { name = name + " Mesh", vertices = new[] { Vector3.zero } };
            foreach (var shape in shapes)
                mesh.AddBlendShapeFrame(shape, 100, new[] { Vector3.up }, null, null);
            meshes.Add(mesh);
            renderer.sharedMesh = mesh;
            return renderer;
        }

        private static ModularAvatarBlendshapeSync Sync(SkinnedMeshRenderer target, params BlendshapeBinding[] bindings)
        {
            var sync = target.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
            sync.Bindings.AddRange(bindings);
            return sync;
        }

        private static BlendshapeBinding Binding(SkinnedMeshRenderer source, string remote, string local = null)
        {
            return new BlendshapeBinding
            {
                ReferenceMesh = source == null ? null : new AvatarObjectReference(source.gameObject),
                Blendshape = remote,
                LocalBlendshape = local,
                RemapCurveIsValid = true,
                RemapCurve = AnimationCurve.Linear(0, 0, 100, 100)
            };
        }

        [Test]
        public void IncludesInactiveNestedAndRootRenderersButExcludesSource()
        {
            var source = Renderer("Body", "A", "B");
            var target = Renderer("Jacket", "A");
            var nested = Renderer("Nested", "B");
            nested.transform.SetParent(target.transform);
            nested.gameObject.SetActive(false);
            var all = BlendshapeSyncScanner.Scan(source, avatar);
            Assert.That(all.Renderers.Select(r => r.Renderer), Is.EquivalentTo(new[] { target, nested }));
            Assert.That(all.CompatibleCount, Is.EqualTo(2));
            Assert.That(all.MissingCount, Is.EqualTo(2));
            Assert.That(all.Renderers.All(r => r.Components.Count == 0), Is.True);
            Assert.That(BlendshapeSyncScanner.Scan(source, target.gameObject).Renderers.Count, Is.EqualTo(2));
            Assert.That(BlendshapeSyncScanner.Scan(source, source.gameObject).Renderers, Is.Empty);
        }

        [Test]
        public void InvalidInputsAndNullTargetMeshAreSafe()
        {
            var source = Renderer("Body", "A");
            Assert.That(BlendshapeSyncScanner.GetInputError(null, avatar), Is.EqualTo("Select a Source Renderer."));
            Assert.That(BlendshapeSyncScanner.GetInputError(source, null), Is.EqualTo("Select a Target Root."));
            var target = Renderer("Empty");
            target.sharedMesh = null;
            Assert.That(BlendshapeSyncScanner.Scan(source, avatar).Renderers.Single().TargetBlendshapes, Is.Empty);
            Sync(target, Binding(source, "A"));
            var row = BlendshapeSyncScanner.Scan(source, avatar).Renderers.Single();
            Assert.That(row.BrokenCount, Is.EqualTo(1));
            Assert.That((row.Bindings[0].Issues & BindingIssue.MissingTargetMesh) != 0, Is.True);
            source.sharedMesh = null;
            Assert.That(BlendshapeSyncScanner.GetInputError(source, avatar), Does.Contain("sharedMesh"));
            Assert.Throws<ArgumentException>(() => BlendshapeSyncScanner.Scan(source, avatar));
        }

        [Test]
        public void CountsDistinctExactShapesAndPreservesCustomOccupancyAndCurves()
        {
            var source = Renderer("Body", "A", "B", "C", "Breast_big");
            var target = Renderer("Jacket", "A", "B", "C", "Bust_Large");
            var custom = Binding(source, "Breast_big", "Bust_Large");
            custom.RemapCurve = AnimationCurve.Linear(0, 100, 100, 0);
            custom.RemapCurve.preWrapMode = WrapMode.Loop;
            custom.RemapCurve.postWrapMode = WrapMode.PingPong;
            var sync = Sync(target, Binding(source, "A"), Binding(source, "A", "A"),
                Binding(source, "B", " \t"), custom, Binding(source, "Breast_big", "C"));
            var original = EditorJsonUtility.ToJson(sync);
            var row = BlendshapeSyncScanner.Scan(source, target.gameObject).Renderers.Single();
            Assert.That(row.SourceBlendshapeCount, Is.EqualTo(4));
            Assert.That(row.TargetBlendshapes.Count, Is.EqualTo(4));
            Assert.That(row.CompatibleCount, Is.EqualTo(3));
            Assert.That(row.SyncedCount, Is.EqualTo(2));
            Assert.That(row.MissingCount, Is.EqualTo(1));
            Assert.That(row.CustomCount, Is.EqualTo(2));
            var c = row.CompatibleShapes.Single(s => s.Name == "C");
            Assert.That(c.IsSynced, Is.False);
            Assert.That(c.HasExistingTargetBinding, Is.True);
            Assert.That(c.ExistingTargetBindings[0].Kind, Is.EqualTo(BindingKind.CustomMapping));
            var captured = row.Bindings[3].Binding;
            Assert.That(captured.RemapCurveIsValid && captured.HasRemapCurve, Is.True);
            Assert.That(captured.RemapKeys[0].value, Is.EqualTo(100));
            Assert.That(captured.PreWrapMode, Is.EqualTo(WrapMode.Loop));
            Assert.That(captured.PostWrapMode, Is.EqualTo(WrapMode.PingPong));
            Assert.That(EditorJsonUtility.ToJson(sync), Is.EqualTo(original));
            custom.RemapCurve.keys = new[] { new Keyframe(0, 7) };
            Assert.That(captured.RemapKeys.Count, Is.EqualTo(2), "Curve keys must be detached from MA.");
            Assert.That(captured.RemapKeys[0].value, Is.EqualTo(100));
        }

        [Test]
        public void DifferentSourceDoesNotCountAsExactSync()
        {
            var source = Renderer("Body", "A", "B");
            var other = Renderer("OtherBody", "A", "B");
            var target = Renderer("Jacket", "A", "B");
            Sync(target, Binding(other, "A"), Binding(other, "A", "B"));
            var row = BlendshapeSyncScanner.Scan(source, target.gameObject).Renderers.Single();
            Assert.That(row.SyncedCount, Is.Zero);
            Assert.That(row.MissingCount, Is.EqualTo(2));
            Assert.That(row.Bindings[0].Kind, Is.EqualTo(BindingKind.OtherSource));
            Assert.That(row.Bindings[1].Kind, Is.EqualTo(BindingKind.CustomMapping));
            Assert.That(row.Bindings.All(b => !b.ReferencesSelectedSource), Is.True);
        }

        [Test]
        public void BrokenReasonsIncludeUnresolvedObjectsMeshesAndShapeNames()
        {
            var source = Renderer("Body", "A");
            var target = Renderer("Jacket", "A");
            var noMesh = Renderer("NoMesh", "A");
            noMesh.sharedMesh = null;
            var noRenderer = new GameObject("NoRenderer");
            noRenderer.transform.SetParent(avatar.transform);
            var missingPath = Binding(source, "A");
            missingPath.ReferenceMesh = new AvatarObjectReference { referencePath = "DoesNotExist" };
            var wrongObject = Binding(source, "A");
            wrongObject.ReferenceMesh = new AvatarObjectReference(noRenderer);
            Sync(target, Binding(null, "A"), missingPath, wrongObject, Binding(noMesh, "A"),
                Binding(source, "Gone"), Binding(source, "A", "Gone"));
            var row = BlendshapeSyncScanner.Scan(source, target.gameObject).Renderers.Single();
            Assert.That(row.BrokenCount, Is.EqualTo(6));
            Assert.That(row.SyncedCount, Is.Zero);
            var expected = new[] { BindingIssue.MissingReference, BindingIssue.UnresolvedReference,
                BindingIssue.MissingReferenceRenderer, BindingIssue.MissingReferenceMesh,
                BindingIssue.MissingSourceBlendshape, BindingIssue.MissingTargetBlendshape };
            for (var i = 0; i < expected.Length; i++)
                Assert.That((row.Bindings[i].Issues & expected[i]) != 0, Is.True, expected[i].ToString());
        }

        [Test]
        public void PublicReferenceResolutionUsesAvatarRootDirectObjectAndFreshClone()
        {
            var source = Renderer("Body", "A");
            var target = Renderer("Jacket", "A");
            var binding = Binding(source, "A");
            binding.ReferenceMesh.referencePath = "StalePath";
            var sync = Sync(target, binding);
            Assert.That(BlendshapeSyncScanner.Scan(source, target.gameObject).SyncedCount, Is.EqualTo(1),
                "A direct target within the avatar wins over an obsolete nonempty path.");
            var pathOnly = new AvatarObjectReference { referencePath = "Body" };
            binding.ReferenceMesh = pathOnly;
            sync.Bindings[0] = binding;
            var cache = typeof(AvatarObjectReference).GetField("_cacheValid", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(cache.GetValue(pathOnly), Is.False);
            Assert.That(BlendshapeSyncScanner.Scan(source, target.gameObject).SyncedCount, Is.EqualTo(1));
            Assert.That(cache.GetValue(pathOnly), Is.False, "Scan must not populate the original MA cache.");
            source.name = "Renamed";
            Assert.That(BlendshapeSyncScanner.Scan(source, target.gameObject).BrokenCount, Is.EqualTo(1));
            binding.ReferenceMesh = new AvatarObjectReference(source.gameObject) { referencePath = "" };
            sync.Bindings[0] = binding;
            Assert.That(BlendshapeSyncScanner.Scan(source, target.gameObject).BrokenCount, Is.EqualTo(1),
                "MA Get(Component) rejects an empty path even when a direct object exists.");
            target.transform.SetParent(null);
            binding.ReferenceMesh = new AvatarObjectReference { referencePath = "Renamed" };
            sync.Bindings[0] = binding;
            Assert.That(BlendshapeSyncScanner.Scan(source, target.gameObject).BrokenCount, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateNamesRetainUnityFrameAndIndexSemantics()
        {
            var source = Renderer("Body", "A");
            source.sharedMesh.AddBlendShapeFrame("A", 200, new[] { Vector3.up * 2 }, null, null);
            var target = Renderer("Jacket", "A", "a");
            var row = BlendshapeSyncScanner.Scan(source, target.gameObject).Renderers.Single();
            Assert.That(source.sharedMesh.blendShapeCount, Is.EqualTo(1));
            Assert.That(source.sharedMesh.GetBlendShapeFrameCount(0), Is.EqualTo(2));
            Assert.That(row.CompatibleCount, Is.EqualTo(1));
            Assert.That(row.CompatibleShapes[0].SourceIndex, Is.EqualTo(source.sharedMesh.GetBlendShapeIndex("A")));
            Assert.That(row.CompatibleShapes[0].TargetIndex, Is.EqualTo(target.sharedMesh.GetBlendShapeIndex("A")));
            Assert.That(row.TargetBlendshapes.Select(s => s.Name), Is.EqualTo(new[] { "A", "a" }));
        }

        [Test]
        public void NoCompatibleShapesIsNormalAndDuplicateObjectNamesAreIdentifiable()
        {
            var source = Renderer("Body", "A");
            var first = Renderer("Jacket", "B");
            var second = Renderer("Jacket", "C");
            var analysis = BlendshapeSyncScanner.Scan(source, avatar);
            Assert.That(analysis.CompatibleCount, Is.Zero);
            Assert.That(analysis.BrokenCount, Is.Zero);
            Assert.That(analysis.Renderers[0].HierarchyPath, Is.Not.EqualTo(analysis.Renderers[1].HierarchyPath));
            Assert.That(analysis.Renderers[0].RendererInstanceId, Is.Not.EqualTo(analysis.Renderers[1].RendererInstanceId));
            Assert.That(analysis.IsStale(source, avatar), Is.False);
            Object.DestroyImmediate(first.gameObject);
            Assert.That(analysis.IsStale(source, avatar), Is.True);
            Assert.That(BlendshapeSyncScanner.Scan(source, second.gameObject).IsStale(source, second.gameObject), Is.False);
        }

        [Test]
        public void SnapshotDetectsInputMeshAndComponentChangesButAllowsOriginallyBrokenReferences()
        {
            var source = Renderer("Body", "A");
            var target = Renderer("Jacket", "A");
            var sync = Sync(target, Binding(null, "A"));
            var snapshot = BlendshapeSyncScanner.Scan(source, avatar);
            Assert.That(snapshot.IsStale(source, avatar), Is.False);
            Assert.That(snapshot.IsStale(target, avatar), Is.True);
            Assert.That(snapshot.IsStale(source, target.gameObject), Is.True);
            Object.DestroyImmediate(sync);
            Assert.That(snapshot.IsStale(source, avatar), Is.True);
            snapshot = BlendshapeSyncScanner.Scan(source, avatar);
            target.sharedMesh = null;
            Assert.That(snapshot.IsStale(source, avatar), Is.True);
            snapshot = BlendshapeSyncScanner.Scan(source, avatar);
            Object.DestroyImmediate(source.sharedMesh);
            Assert.That(snapshot.IsStale(source, avatar), Is.True);
        }

        [TestCase(false, "A", "A", BindingIssue.None, BindingKind.OtherSource)]
        [TestCase(true, "A", "A", BindingIssue.None, BindingKind.ExactSameName)]
        [TestCase(true, "A", "B", BindingIssue.None, BindingKind.CustomMapping)]
        [TestCase(true, "A", "B", BindingIssue.MissingTargetBlendshape, BindingKind.Broken)]
        public void ClassificationCanBeTestedWithoutSceneObjects(bool selected, string sourceName, string targetName,
            BindingIssue issues, BindingKind expected)
        {
            var data = new BlendshapeSyncBindingState(null, 0, 0, null, null, sourceName, targetName,
                false, null, BindingIssue.None, null);
            Assert.That(new AnalyzedBinding(data, issues, selected).Kind, Is.EqualTo(expected));
        }

        private void EnsureAssetFolder()
        {
            if (assetFolder != null) return;
            var name = "__BlendshapeSyncMapperTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", name);
            assetFolder = "Assets/" + name;
        }

        [Test]
        public void ScansThirtyRenderersWithOneHundredFiftyShapes()
        {
            var names = Enumerable.Range(0, 150).Select(i => "Shape" + i).ToArray();
            var source = Renderer("Body", names);
            for (var i = 0; i < 30; i++)
            {
                var target = Renderer("Target" + i, names);
                Sync(target, names.Take(100).Select(name => Binding(source, name)).ToArray());
            }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var snapshot = BlendshapeSyncScanner.Scan(source, avatar);
            watch.Stop();
            Assert.That(snapshot.Renderers.Count, Is.EqualTo(30));
            Assert.That(snapshot.CompatibleCount, Is.EqualTo(4500));
            Assert.That(snapshot.SyncedCount, Is.EqualTo(3000));
            Assert.That(snapshot.MissingCount, Is.EqualTo(1500));
            TestContext.WriteLine($"30 renderers x 150 shapes / 3000 bindings: {watch.Elapsed.TotalMilliseconds:F2} ms");
        }

        private void SaveMeshes()
        {
            EnsureAssetFolder();
            for (var i = 0; i < meshes.Count; i++)
                if (meshes[i] != null && !EditorUtility.IsPersistent(meshes[i]))
                    AssetDatabase.CreateAsset(meshes[i], $"{assetFolder}/mesh-{i}.asset");
        }

        [UnityTest]
        public IEnumerator WindowRescanAndRepaintDoNotChangeScenePrefabOrBindings()
        {
            var source = Renderer("Body", "A");
            var target = Renderer("Jacket", "A");
            var sync = Sync(target, Binding(source, "A"));
            SaveMeshes();
            var prefabPath = assetFolder + "/Avatar.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(avatar, prefabPath, InteractionMode.AutomatedAction);
            // Let MA settle before measuring the tool's operations.
            yield return null;
            yield return null;
            var scenePath = assetFolder + "/ReadOnly.unity";
            Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
            var before = EditorJsonUtility.ToJson(sync);
            var prefabBytes = File.ReadAllBytes(prefabPath);
            var sceneBytes = File.ReadAllBytes(scenePath);
            var componentDirty = EditorUtility.IsDirty(sync);
            var rendererDirty = EditorUtility.IsDirty(target);
            var meshDirty = EditorUtility.IsDirty(target.sharedMesh);
            var overrides = PrefabUtility.GetPropertyModifications(avatar);
            Assert.That(scene.isDirty, Is.False);

            Assert.That(EditorApplication.ExecuteMenuItem("Tools/MA Blendshape Sync Mapper"), Is.True);
            window = EditorWindow.GetWindow<BlendshapeSyncMapperWindow>();
            var windowState = new SerializedObject(window);
            windowState.FindProperty("sourceRenderer").objectReferenceValue = source;
            windowState.FindProperty("targetRoot").objectReferenceValue = target.gameObject;
            windowState.ApplyModifiedPropertiesWithoutUndo();
            window.Rescan();
            window.Repaint();
            yield return null;
            Assert.That(window.Analysis, Is.Not.Null);
            Assert.That(window.Analysis.SyncedCount, Is.EqualTo(1));
            Assert.That(scene.isDirty, Is.False);
            Assert.That(EditorUtility.IsDirty(sync), Is.EqualTo(componentDirty));
            Assert.That(EditorUtility.IsDirty(target), Is.EqualTo(rendererDirty));
            Assert.That(EditorUtility.IsDirty(target.sharedMesh), Is.EqualTo(meshDirty));
            Assert.That(EditorJsonUtility.ToJson(sync), Is.EqualTo(before));
            CollectionAssert.AreEqual(prefabBytes, File.ReadAllBytes(prefabPath));
            CollectionAssert.AreEqual(sceneBytes, File.ReadAllBytes(scenePath));
            Assert.That(PrefabUtility.GetPropertyModifications(avatar)?.Length ?? 0, Is.EqualTo(overrides?.Length ?? 0));
        }

        [Test]
        public void ReaderHandlesMultipleSerializedComponentsWithoutMergingThem()
        {
            var source = Renderer("Body", "A");
            var target = Renderer("Jacket", "A");
            Sync(target, Binding(source, "A"));
            SaveMeshes();
            var path = assetFolder + "/Multiple.prefab";
            PrefabUtility.SaveAsPrefabAsset(avatar, path);
            var yaml = File.ReadAllText(path);
            var scriptGuid = AssetDatabase.AssetPathToGUID(
                "Packages/nadena.dev.modular-avatar/Runtime/ModularAvatarBlendshapeSync.cs");
            var blocks = Regex.Split(yaml, "(?=^--- !u!)", RegexOptions.Multiline);
            var componentBlock = blocks.Single(block => block.StartsWith("--- !u!114 ") && block.Contains(scriptGuid));
            var id = Regex.Match(componentBlock, @"^--- !u!114 &(\d+)").Groups[1].Value;
            const string secondId = "987654321987654321";
            Assert.That(yaml.Contains(secondId), Is.False);
            yaml = yaml.Replace("  - component: {fileID: " + id + "}",
                "  - component: {fileID: " + id + "}\n  - component: {fileID: " + secondId + "}");
            yaml += componentBlock.Replace("--- !u!114 &" + id, "--- !u!114 &" + secondId);
            File.WriteAllText(path, yaml, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var prefabSource = prefab.transform.Find("Body").GetComponent<SkinnedMeshRenderer>();
            var prefabTarget = prefab.transform.Find("Jacket").GetComponent<SkinnedMeshRenderer>();
            var before = File.ReadAllBytes(path);
            var row = BlendshapeSyncScanner.Scan(prefabSource, prefabTarget.gameObject).Renderers.Single();
            Assert.That(row.Components.Count, Is.EqualTo(2));
            Assert.That(row.Bindings.Count, Is.EqualTo(2));
            Assert.That(row.Bindings.Select(b => b.Binding.ComponentIndex), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(row.SyncedCount, Is.EqualTo(1), "Two exact bindings must not count a shape twice.");
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }
    }
}
