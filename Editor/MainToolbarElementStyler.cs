using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CupkekGames.EditorTools
{
    public static class MainToolbarElementStyler
    {
        public static void StyleElement<T>(string elementName, System.Action<T> styleAction) where T : VisualElement
        {
            EditorApplication.delayCall += () =>
            {
                ApplyStyle(elementName, (element) =>
                {
                    T targetElement = null;

                    if (element is T typedElement)
                    {
                        targetElement = typedElement;
                    }
                    else
                    {
                        targetElement = element.Query<T>().First();
                    }

                    if (targetElement != null)
                    {
                        styleAction(targetElement);
                    }
                });
            };
        }

        static void ApplyStyle(string elementName, System.Action<VisualElement> styleCallback)
        {
            var element = FindElementByName(elementName);
            if (element != null)
            {
                styleCallback(element);
            }
        }

        static VisualElement FindElementByName(string name)
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var window in windows)
            {
                var root = window.rootVisualElement;
                if (root == null) continue;

                // Try to find by name using Q<T>
                VisualElement element = root.Q<VisualElement>(name);
                if (element != null) return element;

                // Try to find by tooltip
                element = root.Query<VisualElement>().Where(e => e.tooltip == name).First();
                if (element != null) return element;
            }
            return null;
        }
    }
}

