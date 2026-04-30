using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace CupkekGames.EditorTools
{
    public class MainToolbarButtons
    {
        private const string k_ShouldLoadBootstrap = "LoadBootstrapScene";
        private const string k_BootstrapToggleId = "Bootstrap/Toggle";

        [MainToolbarElement("Project/Open Project Settings", defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement ProjectSettingsButton()
        {
            var icon = EditorGUIUtility.IconContent("SettingsIcon").image as Texture2D;
            var content = new MainToolbarContent(icon);
            return new MainToolbarButton(content, () => { SettingsService.OpenProjectSettings(); });
        }

        [MainToolbarElement("Timescale/Reset", defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement ResetTimeScaleButton()
        {
            var icon = EditorGUIUtility.IconContent("Refresh").image as Texture2D;
            var content = new MainToolbarContent(icon, "Reset");
            var button = new MainToolbarButton(content, () =>
            {
                Time.timeScale = 1f;
                MainToolbar.Refresh("Timescale/Slider");
            });

            MainToolbarElementStyler.StyleElement<UnityEditor.Toolbars.EditorToolbarButton>("Timescale/Reset", element =>
            {
                element.style.paddingLeft = 0f;
                element.style.paddingRight = 0f;
                element.style.marginLeft = 0f;
                element.style.marginRight = 0f;
                element.style.minWidth = 20f;
                element.style.maxWidth = 20f;

                var image = element.Q<Image>();
                if (image != null)
                {
                    image.style.width = 12f;
                    image.style.height = 12f;
                }
            });

            return button;
        }

        [MainToolbarElement(k_BootstrapToggleId, defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement BootstrapToggleButton()
        {
            bool isEnabled = EditorPrefs.GetBool(k_ShouldLoadBootstrap, false);
            string label = isEnabled ? "▶ Bootstrap" : "Bootstrap";

            var icon = EditorGUIUtility.IconContent("SceneAsset Icon").image as Texture2D;
            var content = new MainToolbarContent(icon, label);
            var button = new MainToolbarButton(content, () =>
            {
                bool current = EditorPrefs.GetBool(k_ShouldLoadBootstrap, false);
                EditorPrefs.SetBool(k_ShouldLoadBootstrap, !current);
                MainToolbar.Refresh(k_BootstrapToggleId);
            });

            MainToolbarElementStyler.StyleElement<UnityEditor.Toolbars.EditorToolbarButton>(k_BootstrapToggleId, element =>
            {
                if (isEnabled)
                {
                    element.style.backgroundColor = new Color(0.2f, 0.6f, 0.2f, 0.4f);
                    element.style.borderLeftColor = new Color(0.3f, 0.8f, 0.3f, 0.6f);
                    element.style.borderRightColor = new Color(0.3f, 0.8f, 0.3f, 0.6f);
                    element.style.borderTopColor = new Color(0.3f, 0.8f, 0.3f, 0.6f);
                    element.style.borderBottomColor = new Color(0.3f, 0.8f, 0.3f, 0.6f);
                    element.style.borderLeftWidth = 1f;
                    element.style.borderRightWidth = 1f;
                    element.style.borderTopWidth = 1f;
                    element.style.borderBottomWidth = 1f;
                    element.style.borderTopLeftRadius = 3f;
                    element.style.borderTopRightRadius = 3f;
                    element.style.borderBottomLeftRadius = 3f;
                    element.style.borderBottomRightRadius = 3f;
                }
                else
                {
                    element.style.opacity = 0.5f;
                }
            });

            return button;
        }
    }
}

