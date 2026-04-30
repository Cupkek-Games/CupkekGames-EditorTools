using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CupkekGames.EditorTools
{
    public sealed class ScriptableObjectCreatorWindow : EditorWindow
    {
        private const string RecentPrefsKey = "CupkekGames.SOCreator.Recent";
        private const int MaxRecentItems = 10;
        private const string UssFileName = "ScriptableObjectCreatorWindow.uss";

        private string _targetFolder;
        private List<SOTypeEntry> _allEntries;
        private List<SOTypeEntry> _filteredEntries;
        private List<GroupEntry> _groups;

        private ToolbarSearchField _searchField;
        private ListView _recentListView;
        private ListView _mainListView;
        private Label _selectedLabel;
        private TextField _fileNameField;
        private Button _createButton;
        private Foldout _recentFoldout;
        private VisualElement _bottomBar;

        private SOTypeEntry _selectedEntry;
        private List<string> _recentTypeNames;

        // ─── Data structures ───

        private struct SOTypeEntry
        {
            public Type Type;
            public string DisplayName;
            public string GroupName;
        }

        private struct GroupEntry
        {
            public string Name;
            public bool Expanded;
            public List<SOTypeEntry> Entries;
        }

        // ─── Menu item ───

        [MenuItem("Assets/Create/Create Scriptable Object", false, -1000)]
        private static void OpenFromContextMenu()
        {
            string folder = GetSelectedFolder();
            ScriptableObjectCreatorWindow window = CreateInstance<ScriptableObjectCreatorWindow>();
            window._targetFolder = folder;
            window.titleContent = new GUIContent("Create Scriptable Object");
            window.minSize = new Vector2(360, 480);
            window.ShowUtility();
        }

        private static string GetSelectedFolder()
        {
            if (Selection.activeObject == null)
                return "Assets";

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path))
                return "Assets";

            if (Directory.Exists(path))
                return path;

            string dir = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(dir) ? "Assets" : dir;
        }

        // ─── GUI setup ───

        public void CreateGUI()
        {
            LoadStyleSheet();
            GatherSOTypes();
            LoadRecents();

            VisualElement root = rootVisualElement;
            root.AddToClassList("so-creator-root");

            // Search bar
            _searchField = new ToolbarSearchField();
            _searchField.AddToClassList("so-creator-search");
            _searchField.RegisterValueChangedCallback(OnSearchChanged);
            root.Add(_searchField);

            // Scroll area for recents + main list
            ScrollView scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.AddToClassList("so-creator-scroll");
            root.Add(scrollView);

            // Recently created foldout
            _recentFoldout = new Foldout { text = "Recently Created", value = true };
            _recentFoldout.AddToClassList("so-creator-foldout");
            scrollView.Add(_recentFoldout);

            _recentListView = new ListView
            {
                fixedItemHeight = 22,
                selectionType = SelectionType.Single,
                makeItem = () =>
                {
                    Label label = new Label();
                    label.AddToClassList("so-creator-item");
                    return label;
                },
                bindItem = (element, index) =>
                {
                    Label label = (Label)element;
                    if (index < _recentTypeNames.Count)
                    {
                        string fullName = _recentTypeNames[index];
                        SOTypeEntry? entry = FindEntryByFullName(fullName);
                        label.text = entry.HasValue ? entry.Value.DisplayName : ExtractSimpleName(fullName);
                    }
                }
            };
            _recentListView.selectionChanged += OnRecentSelectionChanged;
            _recentFoldout.Add(_recentListView);
            RefreshRecentList();

            // Group foldouts with flat ListViews
            BuildGroupedList(scrollView);

            // Bottom bar
            _bottomBar = new VisualElement();
            _bottomBar.AddToClassList("so-creator-bottom");
            root.Add(_bottomBar);

            _selectedLabel = new Label("No type selected");
            _selectedLabel.AddToClassList("so-creator-selected-label");
            _bottomBar.Add(_selectedLabel);

            VisualElement nameRow = new VisualElement();
            nameRow.AddToClassList("so-creator-name-row");
            _bottomBar.Add(nameRow);

            Label nameLabel = new Label("File name");
            nameLabel.AddToClassList("so-creator-name-label");
            nameRow.Add(nameLabel);

            _fileNameField = new TextField();
            _fileNameField.AddToClassList("so-creator-name-field");
            _fileNameField.RegisterCallback<KeyDownEvent>(OnFileNameKeyDown);
            nameRow.Add(_fileNameField);

            _createButton = new Button(OnCreateClicked) { text = "Create Asset" };
            _createButton.AddToClassList("so-creator-create-btn");
            _createButton.SetEnabled(false);
            _bottomBar.Add(_createButton);

            // Focus search on open
            root.schedule.Execute(() => _searchField.Focus()).ExecuteLater(50);

            // Escape to close
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                    Close();
            });
        }

        // ─── SO type discovery ───

        private void GatherSOTypes()
        {
            _allEntries = new List<SOTypeEntry>();

            TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<ScriptableObject>();
            foreach (Type type in types)
            {
                if (type.IsAbstract || type.IsGenericType)
                    continue;

                if (typeof(UnityEditor.Editor).IsAssignableFrom(type)
                    || typeof(EditorWindow).IsAssignableFrom(type))
                    continue;

                string assemblyName = type.Assembly.GetName().Name;

                if (IsUnityInternalAssembly(assemblyName))
                    continue;

                _allEntries.Add(new SOTypeEntry
                {
                    Type = type,
                    DisplayName = type.Name,
                    GroupName = GetGroupName(assemblyName)
                });
            }

            _allEntries.Sort((a, b) =>
            {
                int groupCmp = string.Compare(a.GroupName, b.GroupName, StringComparison.Ordinal);
                return groupCmp != 0 ? groupCmp : string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
            });

            _filteredEntries = new List<SOTypeEntry>(_allEntries);
            RebuildGroups();
        }

        private static bool IsUnityInternalAssembly(string name)
        {
            return name.StartsWith("Unity.", StringComparison.Ordinal)
                || name.StartsWith("UnityEngine.", StringComparison.Ordinal)
                || name.StartsWith("UnityEditor.", StringComparison.Ordinal)
                || name == "UnityEngine"
                || name == "UnityEditor";
        }

        private static string GetGroupName(string assemblyName)
        {
            if (assemblyName.StartsWith("CupkekGames.", StringComparison.Ordinal))
            {
                string module = assemblyName.Substring("CupkekGames.".Length);
                // Strip ".Editor", ".Runtime" suffixes
                if (module.EndsWith(".Editor", StringComparison.Ordinal))
                    module = module.Substring(0, module.Length - ".Editor".Length);
                if (module.EndsWith(".Runtime", StringComparison.Ordinal))
                    module = module.Substring(0, module.Length - ".Runtime".Length);
                return module;
            }

            if (assemblyName.StartsWith("Assembly-CSharp", StringComparison.Ordinal))
                return "Game";

            return assemblyName;
        }

        private void RebuildGroups()
        {
            _groups = new List<GroupEntry>();
            string currentGroup = null;
            List<SOTypeEntry> currentEntries = null;

            foreach (SOTypeEntry entry in _filteredEntries)
            {
                if (entry.GroupName != currentGroup)
                {
                    if (currentEntries != null)
                        _groups.Add(new GroupEntry { Name = currentGroup, Expanded = true, Entries = currentEntries });

                    currentGroup = entry.GroupName;
                    currentEntries = new List<SOTypeEntry>();
                }
                currentEntries.Add(entry);
            }

            if (currentEntries != null && currentGroup != null)
                _groups.Add(new GroupEntry { Name = currentGroup, Expanded = true, Entries = currentEntries });
        }

        // ─── Grouped list UI ───

        private ScrollView _groupScrollParent;
        private readonly Dictionary<string, Foldout> _groupFoldouts = new Dictionary<string, Foldout>();

        private void BuildGroupedList(ScrollView scrollParent)
        {
            _groupScrollParent = scrollParent;
            RebuildGroupedListUI();
        }

        private void RebuildGroupedListUI()
        {
            // Remove old group foldouts (keep recent foldout)
            List<VisualElement> toRemove = new List<VisualElement>();
            foreach (VisualElement child in _groupScrollParent.Children())
            {
                if (child != _recentFoldout)
                    toRemove.Add(child);
            }
            foreach (VisualElement el in toRemove)
                _groupScrollParent.Remove(el);

            _groupFoldouts.Clear();

            foreach (GroupEntry group in _groups)
            {
                Foldout foldout = new Foldout { text = $"{group.Name}  ({group.Entries.Count})", value = true };
                foldout.AddToClassList("so-creator-group-foldout");
                _groupScrollParent.Add(foldout);
                _groupFoldouts[group.Name] = foldout;

                foreach (SOTypeEntry entry in group.Entries)
                {
                    SOTypeEntry captured = entry;
                    Button itemButton = new Button(() => SelectEntry(captured))
                    {
                        text = captured.DisplayName
                    };
                    itemButton.AddToClassList("so-creator-type-item");
                    foldout.Add(itemButton);
                }
            }
        }

        // ─── Search ───

        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            string query = evt.newValue?.Trim() ?? "";
            if (string.IsNullOrEmpty(query))
            {
                _filteredEntries = new List<SOTypeEntry>(_allEntries);
            }
            else
            {
                List<FuzzySearch.Result<SOTypeEntry>> results =
                    FuzzySearch.Search(query, _allEntries, e => e.DisplayName);

                _filteredEntries = new List<SOTypeEntry>(results.Count);
                for (int i = 0; i < results.Count; i++)
                    _filteredEntries.Add(results[i].Item);
            }

            RebuildGroups();
            RebuildGroupedListUI();
        }

        // ─── Selection ───

        private void SelectEntry(SOTypeEntry entry)
        {
            _selectedEntry = entry;
            _selectedLabel.text = $"Selected: {entry.DisplayName}";
            _fileNameField.value = $"New {entry.DisplayName}";
            _createButton.SetEnabled(true);
            _fileNameField.Focus();
        }

        private void OnRecentSelectionChanged(IEnumerable<object> selection)
        {
            int index = _recentListView.selectedIndex;
            if (index < 0 || index >= _recentTypeNames.Count)
                return;

            SOTypeEntry? entry = FindEntryByFullName(_recentTypeNames[index]);
            if (entry.HasValue)
                SelectEntry(entry.Value);
        }

        // ─── Create asset ───

        private void OnCreateClicked()
        {
            if (_selectedEntry.Type == null)
                return;

            string fileName = SanitizeFileName(_fileNameField.value?.Trim());
            if (string.IsNullOrEmpty(fileName))
            {
                EditorUtility.DisplayDialog("Invalid Name", "Please enter a valid file name.", "OK");
                return;
            }

            // Validate target folder still exists
            if (!Directory.Exists(_targetFolder))
                _targetFolder = "Assets";

            string assetPath = GetUniqueAssetPath(_targetFolder, fileName);

            try
            {
                ScriptableObject instance = CreateInstance(_selectedEntry.Type);
                AssetDatabase.CreateAsset(instance, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.FocusProjectWindow();
                Selection.activeObject = instance;
                EditorGUIUtility.PingObject(instance);

                AddToRecent(_selectedEntry.Type.AssemblyQualifiedName);
                Close();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Creation Failed",
                    $"Failed to create {_selectedEntry.DisplayName}:\n{ex.Message}", "OK");
            }
        }

        private void OnFileNameKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                evt.StopPropagation();
                if (_createButton.enabledSelf)
                    OnCreateClicked();
            }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
                name = name.Replace(c, '_');

            return name;
        }

        private static string GetUniqueAssetPath(string folder, string fileName)
        {
            // Normalize separators for AssetDatabase
            folder = folder.Replace('\\', '/');
            string path = $"{folder}/{fileName}.asset";

            if (!File.Exists(path))
                return path;

            int counter = 1;
            while (File.Exists($"{folder}/{fileName} {counter}.asset"))
                counter++;

            return $"{folder}/{fileName} {counter}.asset";
        }

        // ─── Recent types (EditorPrefs) ───

        private void LoadRecents()
        {
            string json = EditorPrefs.GetString(RecentPrefsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    RecentData data = JsonUtility.FromJson<RecentData>(json);
                    _recentTypeNames = data?.items ?? new List<string>();
                }
                catch
                {
                    _recentTypeNames = new List<string>();
                }
            }
            else
            {
                _recentTypeNames = new List<string>();
            }
        }

        private void SaveRecents()
        {
            RecentData data = new RecentData { items = _recentTypeNames };
            EditorPrefs.SetString(RecentPrefsKey, JsonUtility.ToJson(data));
        }

        private void AddToRecent(string assemblyQualifiedName)
        {
            _recentTypeNames.Remove(assemblyQualifiedName);
            _recentTypeNames.Insert(0, assemblyQualifiedName);
            if (_recentTypeNames.Count > MaxRecentItems)
                _recentTypeNames.RemoveRange(MaxRecentItems, _recentTypeNames.Count - MaxRecentItems);
            SaveRecents();
        }

        private void RefreshRecentList()
        {
            // Filter out types that no longer exist
            _recentTypeNames.RemoveAll(name => Type.GetType(name) == null);

            bool hasRecents = _recentTypeNames.Count > 0;
            _recentFoldout.style.display = hasRecents ? DisplayStyle.Flex : DisplayStyle.None;

            if (hasRecents)
            {
                _recentListView.itemsSource = _recentTypeNames;
                _recentListView.style.height = Math.Min(_recentTypeNames.Count, MaxRecentItems) * 22;
                _recentListView.Rebuild();
            }
        }

        private SOTypeEntry? FindEntryByFullName(string assemblyQualifiedName)
        {
            Type type = Type.GetType(assemblyQualifiedName);
            if (type == null) return null;

            foreach (SOTypeEntry entry in _allEntries)
            {
                if (entry.Type == type)
                    return entry;
            }
            return null;
        }

        private static string ExtractSimpleName(string assemblyQualifiedName)
        {
            Type type = Type.GetType(assemblyQualifiedName);
            return type != null ? type.Name : "Unknown";
        }

        // ─── Stylesheet ───

        private void LoadStyleSheet()
        {
            // Find USS relative to this script
            string[] guids = AssetDatabase.FindAssets("ScriptableObjectCreatorWindow t:StyleSheet");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(UssFileName, StringComparison.OrdinalIgnoreCase))
                {
                    StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                    if (sheet != null)
                        rootVisualElement.styleSheets.Add(sheet);
                    break;
                }
            }
        }

        // ─── Serializable wrapper for JsonUtility ───

        [Serializable]
        private class RecentData
        {
            public List<string> items = new List<string>();
        }
    }
}
