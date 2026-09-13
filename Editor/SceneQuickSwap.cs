#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CupkekGames.EditorTools
{
    /// <summary>
    /// One swappable scene in the project, addressed by GUID so renames and moves survive.
    /// </summary>
    public readonly struct SceneEntry
    {
        public readonly string Guid;
        public readonly string Path;
        public readonly string Name;
        public readonly string Folder;

        public SceneEntry(string guid, string path)
        {
            Guid = guid;
            Path = path;
            Name = System.IO.Path.GetFileNameWithoutExtension(path);

            string directory = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
            directory = directory.Replace('\\', '/');
            Folder = directory.StartsWith("Assets/", StringComparison.Ordinal)
                ? directory.Substring("Assets/".Length)
                : directory;
        }

        public bool IsValid => !string.IsNullOrEmpty(Path);
    }

    /// <summary>
    /// Model and operations behind the main-toolbar scene quick swap: the project scene index,
    /// per-project favorites and recents, and the edit-mode open calls.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneQuickSwap
    {
        private const int k_MaxRecents = 5;
        private const int k_MaxLabelChars = 18;

        // Scenes under these roots are vendor content, never swap targets.
        private static readonly string[] k_ExcludedFolders = { "Assets/ThirdParty/", "Assets/Vault/" };

        private static List<SceneEntry> s_Scenes;
        private static string s_ProjectKey;

        static SceneQuickSwap()
        {
            ClearLegacyBootstrapPrefs();
        }

        // The scene bootstrapper is gone but its prefs would otherwise sit in the editor forever.
        private static void ClearLegacyBootstrapPrefs()
        {
            const string clearedKey = "CkgSceneSwap.LegacyBootstrapPrefsCleared";
            if (EditorPrefs.GetBool(clearedKey, false))
                return;

            EditorPrefs.DeleteKey("LoadBootstrapScene");
            EditorPrefs.DeleteKey("PreviousScene");
            EditorPrefs.SetBool(clearedKey, true);
        }

        public static IReadOnlyList<SceneEntry> AllScenes
        {
            get
            {
                if (s_Scenes == null)
                    RebuildIndex();
                return s_Scenes;
            }
        }

        public static void InvalidateCache()
        {
            s_Scenes = null;
        }

        private static void RebuildIndex()
        {
            s_Scenes = new List<SceneEntry>();

            string[] guids = AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || IsExcluded(path))
                    continue;

                s_Scenes.Add(new SceneEntry(guid, path));
            }

            s_Scenes.Sort(CompareByFolderThenName);
        }

        private static bool IsExcluded(string path)
        {
            foreach (string folder in k_ExcludedFolders)
            {
                if (path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool TryGetByGuid(string guid, out SceneEntry entry)
        {
            IReadOnlyList<SceneEntry> scenes = AllScenes;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].Guid == guid)
                {
                    entry = scenes[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public static List<SceneEntry> Resolve(List<string> guids)
        {
            var result = new List<SceneEntry>();
            foreach (string guid in guids)
            {
                if (TryGetByGuid(guid, out SceneEntry entry))
                    result.Add(entry);
            }

            return result;
        }

        public static List<string> Favorites => ReadGuidList(FavoritesKey);

        public static List<string> Recents => ReadGuidList(RecentsKey);

        public static bool IsFavorite(string guid)
        {
            return Favorites.Contains(guid);
        }

        public static void ToggleFavorite(string guid)
        {
            List<string> favorites = Favorites;
            if (!favorites.Remove(guid))
                favorites.Add(guid);

            favorites.Sort(CompareGuidsByFolderThenName);
            WriteGuidList(FavoritesKey, favorites);
        }

        // Scenes group by where they live: folder first, name only to break ties inside one folder.
        private static int CompareByFolderThenName(SceneEntry left, SceneEntry right)
        {
            int byFolder = string.Compare(left.Folder, right.Folder, StringComparison.OrdinalIgnoreCase);
            return byFolder != 0
                ? byFolder
                : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareGuidsByFolderThenName(string leftGuid, string rightGuid)
        {
            TryGetByGuid(leftGuid, out SceneEntry left);
            TryGetByGuid(rightGuid, out SceneEntry right);
            return CompareByFolderThenName(left, right);
        }

        private static void RecordRecent(string guid)
        {
            List<string> recents = Recents;
            recents.Remove(guid);
            recents.Insert(0, guid);

            while (recents.Count > k_MaxRecents)
                recents.RemoveAt(recents.Count - 1);

            WriteGuidList(RecentsKey, recents);
        }

        /// <summary>
        /// Opens a scene in edit mode. Single opens prompt to save first and abort if the user cancels.
        /// </summary>
        public static bool Open(SceneEntry entry, bool additive)
        {
            if (!entry.IsValid || EditorApplication.isPlayingOrWillChangePlaymode)
                return false;

            if (!additive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            EditorSceneManager.OpenScene(entry.Path, additive ? OpenSceneMode.Additive : OpenSceneMode.Single);
            RecordRecent(entry.Guid);
            return true;
        }

        public static void Ping(SceneEntry entry)
        {
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.Path);
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }

        /// <summary>
        /// Toolbar label: the active scene, plus how many extra scenes are loaded additively.
        /// </summary>
        public static string CurrentSceneLabel()
        {
            Scene active = SceneManager.GetActiveScene();
            string name = string.IsNullOrEmpty(active.name) ? "Untitled" : active.name;

            if (name.Length > k_MaxLabelChars)
                name = name.Substring(0, k_MaxLabelChars - 1) + "…";

            int extra = SceneManager.sceneCount - 1;
            return extra > 0 ? name + " +" + extra : name;
        }

        private static string FavoritesKey => "CkgSceneSwap." + ProjectKey + ".Favorites";

        private static string RecentsKey => "CkgSceneSwap." + ProjectKey + ".Recents";

        // EditorPrefs are machine-global, so key the lists by project to keep separate
        // projects from sharing one favorites list.
        private static string ProjectKey
        {
            get
            {
                if (s_ProjectKey != null)
                    return s_ProjectKey;

                uint hash = 2166136261;
                string path = Application.dataPath;
                for (int i = 0; i < path.Length; i++)
                {
                    hash ^= path[i];
                    hash *= 16777619;
                }

                s_ProjectKey = hash.ToString("x8");
                return s_ProjectKey;
            }
        }

        private static List<string> ReadGuidList(string key)
        {
            var result = new List<string>();

            string raw = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(raw))
                return result;

            foreach (string guid in raw.Split(','))
            {
                if (string.IsNullOrEmpty(guid) || result.Contains(guid))
                    continue;

                // Drop entries whose scene no longer exists instead of showing dead rows.
                if (!TryGetByGuid(guid, out _))
                    continue;

                result.Add(guid);
            }

            return result;
        }

        private static void WriteGuidList(string key, List<string> guids)
        {
            EditorPrefs.SetString(key, string.Join(",", guids.ToArray()));
        }
    }

    /// <summary>
    /// Keeps the scene index fresh without rescanning the project on every dropdown click.
    /// </summary>
    internal class SceneQuickSwapAssetWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (ContainsScene(importedAssets) || ContainsScene(deletedAssets) ||
                ContainsScene(movedAssets) || ContainsScene(movedFromAssetPaths))
            {
                SceneQuickSwap.InvalidateCache();
            }
        }

        private static bool ContainsScene(string[] paths)
        {
            foreach (string path in paths)
            {
                if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
#endif
