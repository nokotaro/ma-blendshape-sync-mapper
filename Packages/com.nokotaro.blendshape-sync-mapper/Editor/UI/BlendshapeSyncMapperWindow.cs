using UnityEditor;
using UnityEngine;

namespace Nokotaro.BlendshapeSyncMapper
{
    public sealed class BlendshapeSyncMapperWindow : EditorWindow
    {
        private const string WindowTitle = "MA Blendshape Sync Mapper";

        [SerializeField] private SkinnedMeshRenderer sourceRenderer;
        [SerializeField] private GameObject targetRoot;

        [MenuItem("Tools/MA Blendshape Sync Mapper")]
        public static void OpenWindow()
        {
            GetWindow<BlendshapeSyncMapperWindow>(WindowTitle);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(WindowTitle, EditorStyles.boldLabel);
            EditorGUILayout.Space();

            sourceRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
                "Source Renderer", sourceRenderer, typeof(SkinnedMeshRenderer), true);
            targetRoot = (GameObject)EditorGUILayout.ObjectField(
                "Target Root", targetRoot, typeof(GameObject), true);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
            {
                GUILayout.Button(new GUIContent("Rescan", "Scanning is not available in this development version."));
            }
        }
    }
}
