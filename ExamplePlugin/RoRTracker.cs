using BepInEx;
using On.RoR2.UI;
using R2API;
using RoR2;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using static Rewired.Utils.Classes.Utility.ObjectInstanceTracker;
using UnlockableDef = RoR2.UnlockableDef;

namespace RoRTracker
{
    //This attribute specifies that we have a dependency on R2API, as we're using it to add our item to the game.
    //You don't need this if you're not using R2API in your plugin, it's just to tell BepInEx to initialize R2API before this plugin so it's safe to use R2API.
    [BepInDependency(R2API.R2API.PluginGUID)]

    //This attribute is required, and lists metadata for your plugin.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    //This is the main declaration of our plugin class. BepInEx searches for all classes inheriting from BaseUnityPlugin to initialize on startup.
    //BaseUnityPlugin itself inherits from MonoBehaviour, so you can use this as a reference for what you can declare and use in your plugin class: https://docs.unity3d.com/ScriptReference/MonoBehaviour.html
    public class RoRTracker : BaseUnityPlugin
    {
        //The Plugin GUID should be a unique ID for this plugin, which is human readable (as it is used in places like the config).
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "Valerbear";
        public const string PluginName = "RoRTracker";
        public const string PluginVersion = "0.0.1";

        List<UnlockableDef> pending = new List<UnlockableDef>();
        UserProfile profile;

        //The Awake() method is run at the very start of the Unity Lifecycle when the game is initialized.
        public void Awake()
        {
            //Init our logging class so that we can properly log for debugging
            Log.Init(Logger);

            //subscribe our Run_Awake hook to the Run.Awake method from the game, we'll eventually have this load the data we need to display
            On.RoR2.Run.Awake += Run_Awake;
            //subscribe our HUD_Awake hook to the HUD.ActivateScoreboard method
            On.RoR2.UI.HUD.ActivateScoreboard += HUD_ActivateScoreboard;
        }

        #region Hooks
        private void Run_Awake(On.RoR2.Run.orig_Awake orig, Run self)
        {
            orig.Invoke(self);

            //Makes sure the Update Function is hooked at the start!
            On.RoR2.Run.Update += Run_Update;
        }

        private void Run_Update(On.RoR2.Run.orig_Update orig, Run self)
        {
            orig.Invoke(self);

            // try to make sure everything is fully loaded
            if (RoR2.Run.instance.time < 5)
                return;
            
            profile = LocalUserManager.GetFirstLocalUser().userProfile;

            //iterate over the UnlockableCatalog to figure out what the user is still missing
            for (UnlockableIndex unlockableIndex = (UnlockableIndex)0; unlockableIndex < (UnlockableIndex)UnlockableCatalog.indexToDefTable.Length; unlockableIndex++)
            {
                Log.Info($"Building entry for index {unlockableIndex}");
                UnlockableDef unlockable = UnlockableCatalog.indexToDefTable[(int)unlockableIndex]; // BepInEx should address these warnings automatically
                Log.Info($"Building entry for def at index {unlockableIndex}: {unlockable.cachedName}");

                if (!profile.HasUnlockable(unlockable))
                {
                    this.pending.Add(unlockable);
                }
            }

            Log.Info($"Found {pending.Count} pending unlocks for user {profile.name}.");
            if (pending.Count > 0)
            {
                Log.Info($"First pending unlock is {pending[0].cachedName}");
            }

            //unsubscribe so we don't try to recompute this list every frame
            On.RoR2.Run.Update -= Run_Update;
        }

        private void HUD_ActivateScoreboard(On.RoR2.UI.HUD.orig_ActivateScoreboard orig, RoR2.UI.HUD self)
        {
            Log.Info("TOP OF HUD_ACTIVATE_SCOREBOARD");
            orig.Invoke(self);
            Log.Info($"HUD_ACTIVATE_SCOREBOARD HOOK: {pending.Count} pending unlocks for user {profile.name}.");
            this.BuildPendingUnlocksPanel(self);
        }
        #endregion

        private void BuildPendingUnlocksPanel(RoR2.UI.HUD hud)
        {
            Log.Info("Begin building pending unlocks panel.");
            GameObject panel = new GameObject("PendingUnlocksPanel");
            panel.transform.SetParent(hud.scoreboardPanel.transform, false);

            //create the parent RectTransform and set its anchors, pivot, position, and size. our content will go within
            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -20f);
            rt.sizeDelta = new Vector2(0f, 200f);

            //this lets us stack the text entries per unlock vertically
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.spacing = 4f;

            Log.Info("Begin adding in the unlocks to the UI:");

            foreach (UnlockableDef def in pending)
            {
                Log.Info($"Adding entry for {def.cachedName ?? "Unlockable"}");
                GameObject entry = new GameObject(def.cachedName ?? "Unlockable");
                entry.transform.SetParent(panel.transform, false);

                TextMeshProUGUI text = entry.AddComponent<TextMeshProUGUI>();
                text.text = Language.GetString(def.nameToken);
                text.fontSize = 14f;
                text.color = Color.white;
            }
        }
    }
}




