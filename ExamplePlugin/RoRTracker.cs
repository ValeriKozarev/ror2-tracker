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
        GameObject pendingUnlocksPanel;
        bool panelBuilt = false;
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
            On.RoR2.UI.HUD.DeactivateScoreboard += HUD_DeactivateScoreboard;

            //TODO: maybe we want to create a new logbook page or similar that is just for the pending unlocks so you can view that from the main menu as well?
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

            //try to make sure everything is fully loaded, TODO: is there a better way to do this?
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
            //TODO: we probably do want SOME recompute logic to capture if you completed a challenge? maybe another hook?
            On.RoR2.Run.Update -= Run_Update;
        }

        private void HUD_ActivateScoreboard(On.RoR2.UI.HUD.orig_ActivateScoreboard orig, RoR2.UI.HUD self)
        {
            Log.Info("TOP OF HUD_ACTIVATE_SCOREBOARD");
            orig.Invoke(self);
            if (!panelBuilt)
            {
                Log.Info($"HUD_ACTIVATE_SCOREBOARD HOOK: {pending.Count} pending unlocks for user {profile.name}.");
                BuildPendingUnlocksPanel(self);
                panelBuilt = true;
            }
            pendingUnlocksPanel?.SetActive(true);
        }

        private void HUD_DeactivateScoreboard(On.RoR2.UI.HUD.orig_DeactivateScoreboard orig, RoR2.UI.HUD self)
        {
            orig.Invoke(self);
            pendingUnlocksPanel?.SetActive(false);
        }
        #endregion

        private void BuildPendingUnlocksPanel(RoR2.UI.HUD hud)
        {
            Log.Info("Begin building pending unlocks panel.");
            GameObject panel = new GameObject("PendingUnlocksPanel");
            panel.transform.SetParent(hud.mainContainer.transform, false);

            //create the parent container for all of our UI that appears when you Tab in-game
            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f,0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(400f, 200f);

            //then this lets us stack the text entries per unlock vertically
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;

            pendingUnlocksPanel = panel;
            panel.SetActive(false);

            Log.Info("Begin adding in the unlocks to the UI:");
            //TODO: probably want to adding scrolling or pagination or something
            //TODO: maybe add auto-filtering for what content we care about? eg. item unlocks, skills and skins and then also filter down to the relevant character?
            //TODO: it would be good to add tracking or something so we can just have a few we care about (maybe track up to 5 at a time?)

            for (int i = 0; i < 5; i++)
            {
                UnlockableDef def = pending[i];

                // TODO: it would be nice to make this UI a bit better, maybe more like a card
                // for that I think we would want AchievementDef.GetUnachievedIcon(), the UnlockableDef.CachedName, and the AchievementDef.DescriptionToken
                Log.Info($"Adding entry for {def.cachedName ?? "Unlockable"}");

                // name comes from the UnlockableDef
                GameObject unlockName = new GameObject(def.cachedName ?? "Unlockable");
                unlockName.transform.SetParent(panel.transform, false);
                LayoutElement unlockNameLayout = unlockName.AddComponent<LayoutElement>();
                unlockNameLayout.preferredHeight = 16f;
                TextMeshProUGUI unlockNameText = unlockName.AddComponent<TextMeshProUGUI>();
                unlockNameText.text = Language.GetString(def.nameToken);
                unlockNameText.fontSize = 14f;
                unlockNameText.color = Color.white;
                unlockNameText.enableWordWrapping = false;

                // desc comes from the related AchievementDef
                AchievementDef achievement = AchievementManager.GetAchievementDefFromUnlockable(def.cachedName);
                if (achievement == null)        
                {
                    Log.Info($"WARNING: No achievement found for {def.cachedName ?? "Unlockable"}"); // TODO: not sure if this is the correct lookup or not 
                    continue;
                }
                GameObject unlockDesc = new GameObject(achievement.descriptionToken ?? "How to Unlock");
                unlockDesc.transform.SetParent(panel.transform, false);
                LayoutElement unlockDescLayout = unlockDesc.AddComponent<LayoutElement>();
                unlockDescLayout.preferredHeight = 14f;
                TextMeshProUGUI unlockDescText = unlockDesc.AddComponent<TextMeshProUGUI>();
                unlockDescText.text = Language.GetString(achievement.descriptionToken);
                unlockDescText.fontSize = 12f;
                unlockDescText.color = Color.gray;
                unlockDescText.enableWordWrapping = true;
            }
        }
    }
}







