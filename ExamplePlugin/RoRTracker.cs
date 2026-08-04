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

            pendingUnlocksPanel = new GameObject("PendingUnlocksPanel");
            pendingUnlocksPanel.transform.SetParent(hud.mainContainer.transform, false);

            RectTransform rt = pendingUnlocksPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.4f);
            rt.anchorMax = new Vector2(0.5f, 0.4f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(560f, 340f);

            VerticalLayoutGroup outerLayout = pendingUnlocksPanel.AddComponent<VerticalLayoutGroup>();
            outerLayout.childForceExpandHeight = false;
            outerLayout.childForceExpandWidth = true;
            outerLayout.childControlWidth = true;
            outerLayout.childControlHeight = true;
            outerLayout.spacing = 9f; // a bit more room between cards than between text lines

            pendingUnlocksPanel.SetActive(false);

            Log.Info("Begin adding in the unlocks to the UI:");

            for (int i = 0; i < 5; i++)
            {
                UnlockableDef def = pending[i];
                Log.Info($"Adding entry for {def.cachedName ?? "Unlockable"}");

                AchievementDef achievement = AchievementManager.GetAchievementDefFromUnlockable(def.cachedName);
                if (achievement == null)
                {
                    Log.Info($"WARNING: No achievement found for {def.cachedName ?? "Unlockable"}");
                    continue;
                }

                // --- Card container ---
                GameObject card = new GameObject("Card_" + (def.cachedName ?? "Unlockable"));
                card.transform.SetParent(pendingUnlocksPanel.transform, false);

                LayoutElement cardLayout = card.AddComponent<LayoutElement>();
                cardLayout.preferredHeight = 73f; // room for icon + two lines of text

                Image cardBg = card.AddComponent<Image>();
                cardBg.color = new Color(0f, 0f, 0f, 0.6f); // dark semi-transparent backing, common RoR2 UI pattern

                HorizontalLayoutGroup cardInnerLayout = card.AddComponent<HorizontalLayoutGroup>();
                cardInnerLayout.childForceExpandWidth = false;
                cardInnerLayout.childForceExpandHeight = true;
                cardInnerLayout.childControlWidth = true;
                cardInnerLayout.childControlHeight = true;
                cardInnerLayout.padding = new RectOffset(9, 9, 9, 9);
                cardInnerLayout.spacing = 11f;

                // --- Icon ---
                GameObject iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(card.transform, false);

                LayoutElement iconLayout = iconGO.AddComponent<LayoutElement>();
                iconLayout.preferredWidth = 55f;
                iconLayout.preferredHeight = 55f;
                iconLayout.flexibleWidth = 0f; // fixed size, don't stretch

                Image iconImage = iconGO.AddComponent<Image>();
                iconImage.sprite = achievement.GetUnachievedIcon();
                iconImage.preserveAspect = true;

                // --- Text column (title + description stacked) ---
                GameObject textColumn = new GameObject("TextColumn");
                textColumn.transform.SetParent(card.transform, false);

                LayoutElement textColumnLayout = textColumn.AddComponent<LayoutElement>();
                textColumnLayout.flexibleWidth = 1f; // takes remaining width after the fixed-size icon

                VerticalLayoutGroup textColumnLayoutGroup = textColumn.AddComponent<VerticalLayoutGroup>();
                textColumnLayoutGroup.childForceExpandWidth = true;
                textColumnLayoutGroup.childForceExpandHeight = false;
                textColumnLayoutGroup.childControlWidth = true;
                textColumnLayoutGroup.childControlHeight = true;
                textColumnLayoutGroup.spacing = 2f;

                GameObject titleGO = new GameObject("Title");
                titleGO.transform.SetParent(textColumn.transform, false);
                LayoutElement titleLayout = titleGO.AddComponent<LayoutElement>();
                titleLayout.preferredHeight = 23f;
                TextMeshProUGUI titleText = titleGO.AddComponent<TextMeshProUGUI>();
                titleText.text = Language.GetString(def.nameToken);
                titleText.fontSize = 16f;
                titleText.color = Color.white;
                titleText.enableWordWrapping = false;

                GameObject descGO = new GameObject("Description");
                descGO.transform.SetParent(textColumn.transform, false);
                LayoutElement descLayout = descGO.AddComponent<LayoutElement>();
                descLayout.preferredHeight = 37f;
                TextMeshProUGUI descText = descGO.AddComponent<TextMeshProUGUI>();
                descText.text = Language.GetString(achievement.descriptionToken);
                descText.fontSize = 14f;
                descText.color = Color.gray;
                descText.enableWordWrapping = true;
            }
        }
    }
}







