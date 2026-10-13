using UnityEngine;

namespace IslandTycoon
{
    /// <summary>JSON save in PlayerPrefs. Includes the in-progress wave (NFR-07).</summary>
    public static class SaveSystem
    {
        const string Key = "island_tycoon_save_v2";

        public static void Save(GameState s)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(s));
            PlayerPrefs.Save();
        }

        public static GameState Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return null;
            try { return JsonUtility.FromJson<GameState>(PlayerPrefs.GetString(Key)); }
            catch (System.Exception e) { Debug.LogWarning("[IslandTycoon] Save corrupted, starting fresh: " + e.Message); return null; }
        }

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
