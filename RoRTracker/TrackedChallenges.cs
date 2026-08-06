using System.Collections.Generic;
using BepInEx.Configuration;
using SimpleJSON;

namespace RoRTracker
{
    /// <summary>
    /// Persists the set of challenges the player is tracking from their logbook.
    /// </summary>
    internal class TrackedChallenges
    {
        public const int MaxTracked = 5;

        private readonly ConfigEntry<string> storage;
        private readonly HashSet<string> tracked = new HashSet<string>();

        public TrackedChallenges(ConfigFile config)
        {
            storage = config.Bind("Tracking", "TrackedChallenges", "[]",
                "JSON array of achievement identifiers currently being tracked (max " + MaxTracked + ").");
            Load();
        }

        // whether the achievement is currently being tracked
        public bool IsTracked(string achievementId) => tracked.Contains(achievementId);

        // start tracking the achievement
        public bool TryTrack(string achievementId)
        {
            // already tracking it
            if (tracked.Contains(achievementId))
                return true;

            // we're already tracking the max number of achievements
            if (tracked.Count >= MaxTracked)
                return false;

            // otherwise, add it to tracked and save
            tracked.Add(achievementId);
            Save();
            return true;
        }

        // stop tracking the achievement
        public void TryUntrack(string achievementId)
        {
            if (tracked.Remove(achievementId))
                Save();
        }

        // read from the BepInEx config entry
        private void Load()
        {
            tracked.Clear();

            JSONNode parsed = JSON.Parse(storage.Value);
            if (parsed == null || !parsed.IsArray)
                return;

            foreach (JSONNode entry in parsed.AsArray)
            {
                string identifier = entry.Value;
                if (!string.IsNullOrEmpty(identifier))
                    tracked.Add(identifier);
            }
        }

        // write to the BepInEx config entry
        private void Save()
        {
            JSONArray array = new JSONArray();
            foreach (string identifier in tracked)
                array.Add(identifier);

            storage.Value = array.ToString();
        }
    }
}
