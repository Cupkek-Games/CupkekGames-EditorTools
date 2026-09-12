#if UNITY_EDITOR
using System.Collections.Generic;
using CupkekGames.EditorUI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CupkekGames.EditorTools
{
    /// <summary>
    /// The scene quick-swap dropdown: a search field over favorites, recents and every project scene.
    /// Enter or click opens single, Ctrl held opens additive, Alt pings the asset instead.
    /// </summary>
    public class SceneQuickSwapPopup : PopupWindowContent
    {
        private const float k_Width = 340f;
        private const float k_RowHeight = 22f;
        private const float k_HeaderHeight = 18f;
        private const float k_SearchHeight = 26f;
        private const int k_MaxVisibleRows = 16;

        private static readonly Color k_SelectionColor = new(0.24f, 0.44f, 0.75f, 0.55f);

        private readonly List<SceneEntry> _rows = new();
        private readonly List<VisualElement> _rowElements = new();

        private ToolbarSearchField _search;
        private ScrollView _list;
        private string _query = string.Empty;
        private int _selected;

        public override Vector2 GetWindowSize()
        {
            int favorites = SceneQuickSwap.Favorites.Count;
            int recents = SceneQuickSwap.Recents.Count;
            int all = SceneQuickSwap.AllScenes.Count;

            int sections = (favorites > 0 ? 1 : 0) + (recents > 0 ? 1 : 0) + (all > 0 ? 1 : 0);
            int rows = Mathf.Min(favorites + recents + all, k_MaxVisibleRows);

            float height = k_SearchHeight + sections * k_HeaderHeight + rows * k_RowHeight + 8f;
            return new Vector2(k_Width, height);
        }

        // The popup draws itself with UI Toolkit; IMGUI has nothing to do here.
        public override void OnGUI(Rect rect)
        {
        }

        public override void OnOpen()
        {
            VisualElement root = editorWindow.rootVisualElement;
            root.style.paddingTop = 3f;
            root.style.paddingLeft = 3f;
            root.style.paddingRight = 3f;

            _search = new ToolbarSearchField();
            _search.style.width = Length.Percent(100);
            _search.style.marginBottom = 3f;
            _search.RegisterValueChangedCallback(evt =>
            {
                _query = evt.newValue;
                _selected = 0;
                Rebuild();
            });
            root.Add(_search);

            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.style.flexGrow = 1f;
            root.Add(_list);

            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            Rebuild();

            // The field only takes focus once the popup's panel is live.
            root.schedule.Execute(() =>
            {
                TextField field = _search.Q<TextField>();
                if (field != null)
                    field.Focus();
            }).ExecuteLater(10);
        }

        private void Rebuild()
        {
            _list.Clear();
            _rows.Clear();
            _rowElements.Clear();

            if (string.IsNullOrWhiteSpace(_query))
            {
                AddSection("★  FAVORITES", SceneQuickSwap.Resolve(SceneQuickSwap.Favorites));
                AddSection("RECENT", SceneQuickSwap.Resolve(SceneQuickSwap.Recents));
                AddSection("ALL SCENES", SceneQuickSwap.AllScenes);
            }
            else
            {
                AddSection(null, Filter(_query));
            }

            if (_rows.Count == 0)
            {
                var empty = new Label("No scenes match");
                empty.style.color = EditorColorPalette.TextMuted;
                empty.style.paddingLeft = 6f;
                empty.style.paddingTop = 6f;
                _list.Add(empty);
            }

            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _rows.Count - 1));
            RefreshSelection();
        }

        /// <summary>
        /// Name matches rank above folder matches, so typing "town" finds the scene before the folder.
        /// </summary>
        private List<SceneEntry> Filter(string query)
        {
            IReadOnlyList<SceneEntry> all = SceneQuickSwap.AllScenes;
            var ordered = new List<SceneEntry>();
            var taken = new HashSet<string>();

            foreach (FuzzySearch.Result<SceneEntry> result in FuzzySearch.Search(query, all, entry => entry.Name))
            {
                taken.Add(result.Item.Guid);
                ordered.Add(result.Item);
            }

            foreach (FuzzySearch.Result<SceneEntry> result in FuzzySearch.Search(query, all, entry => entry.Folder))
            {
                if (taken.Add(result.Item.Guid))
                    ordered.Add(result.Item);
            }

            return ordered;
        }

        private void AddSection(string title, IReadOnlyList<SceneEntry> entries)
        {
            if (entries.Count == 0)
                return;

            if (!string.IsNullOrEmpty(title))
            {
                var header = new Label(title);
                header.style.height = k_HeaderHeight;
                header.style.paddingLeft = 4f;
                header.style.fontSize = 9f;
                header.style.unityTextAlign = TextAnchor.LowerLeft;
                header.style.color = EditorColorPalette.TextMuted;
                _list.Add(header);
            }

            for (int i = 0; i < entries.Count; i++)
                AddRow(entries[i]);
        }

        private void AddRow(SceneEntry entry)
        {
            int index = _rows.Count;
            _rows.Add(entry);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = k_RowHeight;
            row.style.paddingRight = 6f;
            row.style.borderTopLeftRadius = 3f;
            row.style.borderTopRightRadius = 3f;
            row.style.borderBottomLeftRadius = 3f;
            row.style.borderBottomRightRadius = 3f;

            bool isFavorite = SceneQuickSwap.IsFavorite(entry.Guid);
            var star = new Label(isFavorite ? "★" : "☆");
            star.style.width = 20f;
            star.style.unityTextAlign = TextAnchor.MiddleCenter;
            star.style.color = isFavorite ? EditorColorPalette.Warning : EditorColorPalette.TextMuted;
            star.style.opacity = isFavorite ? 1f : 0.35f;
            star.tooltip = isFavorite ? "Remove from favorites" : "Add to favorites";
            star.RegisterCallback<PointerDownEvent>(evt =>
            {
                evt.StopPropagation();
                SceneQuickSwap.ToggleFavorite(entry.Guid);
                Rebuild();
            });
            row.Add(star);

            var name = new Label(entry.Name);
            name.style.flexGrow = 1f;
            name.style.overflow = Overflow.Hidden;
            name.style.color = EditorColorPalette.TextPrimary;
            row.Add(name);

            var folder = new Label(entry.Folder);
            folder.style.fontSize = 9f;
            folder.style.color = EditorColorPalette.TextMuted;
            row.Add(folder);

            row.RegisterCallback<PointerEnterEvent>(_ =>
            {
                _selected = index;
                RefreshSelection();
            });

            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                evt.StopPropagation();

                if (evt.altKey)
                {
                    SceneQuickSwap.Ping(entry);
                    return;
                }

                Activate(entry, evt.ctrlKey || evt.commandKey);
            });

            _rowElements.Add(row);
            _list.Add(row);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.DownArrow:
                    Move(1);
                    evt.StopPropagation();
                    break;

                case KeyCode.UpArrow:
                    Move(-1);
                    evt.StopPropagation();
                    break;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_selected >= 0 && _selected < _rows.Count)
                        Activate(_rows[_selected], evt.ctrlKey || evt.commandKey);
                    evt.StopPropagation();
                    break;

                case KeyCode.Escape:
                    editorWindow.Close();
                    evt.StopPropagation();
                    break;
            }
        }

        private void Move(int delta)
        {
            if (_rows.Count == 0)
                return;

            _selected = Mathf.Clamp(_selected + delta, 0, _rows.Count - 1);
            RefreshSelection();
            _list.ScrollTo(_rowElements[_selected]);
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < _rowElements.Count; i++)
            {
                _rowElements[i].style.backgroundColor = i == _selected
                    ? k_SelectionColor
                    : Color.clear;
            }
        }

        // Close before opening: the save prompt and scene load must not run inside a popup event.
        private void Activate(SceneEntry entry, bool additive)
        {
            editorWindow.Close();
            EditorApplication.delayCall += () => SceneQuickSwap.Open(entry, additive);
        }
    }
}
#endif
