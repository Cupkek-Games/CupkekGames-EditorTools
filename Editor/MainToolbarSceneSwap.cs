#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CupkekGames.EditorTools
{
    /// <summary>
    /// Main-toolbar dropdown for swapping scenes in edit mode. The label doubles as a readout
    /// of which scene is currently open.
    /// </summary>
    [InitializeOnLoad]
    public static class MainToolbarSceneSwap
    {
        private const string k_ElementId = "Scenes/QuickSwap";

        static MainToolbarSceneSwap()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;

            EditorSceneManager.sceneClosed -= OnSceneClosed;
            EditorSceneManager.sceneClosed += OnSceneClosed;

            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MainToolbarElement(k_ElementId, defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement SceneSwapDropdown()
        {
            var icon = EditorGUIUtility.IconContent("SceneAsset Icon").image as Texture2D;
            var content = new MainToolbarContent(
                SceneQuickSwap.CurrentSceneLabel(),
                icon,
                "Open a scene. Ctrl-click a row to open it additively, Alt-click to ping it.");

            var dropdown = new MainToolbarDropdown(content, rect =>
            {
                UnityEditor.PopupWindow.Show(rect, new SceneQuickSwapPopup());
            });

            // Swapping scenes mid-play is exactly the surprise this tool replaced.
            dropdown.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;

            MainToolbarElementStyler.StyleElement<VisualElement>(k_ElementId, element =>
            {
                element.style.paddingLeft = 6f;
            });

            return dropdown;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => Refresh();

        private static void OnSceneClosed(Scene scene) => Refresh();

        private static void OnActiveSceneChanged(Scene previous, Scene next) => Refresh();

        private static void OnPlayModeStateChanged(PlayModeStateChange change) => Refresh();

        private static void Refresh()
        {
            MainToolbar.Refresh(k_ElementId);
        }
    }
}
#endif
