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
            //subscribe our HUD_Awake hook to the HUD.ActivateScoreboard and HUD.DeactivateScoreboard methods
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
                UnlockableDef unlockable = UnlockableCatalog.indexToDefTable[(int)unlockableIndex]; // note: sounds like BepInEx addresses these warnings automatically
                Log.Info($"Building entry for def at index {unlockableIndex}: {unlockable.cachedName}");

                if (!profile.HasUnlockable(unlockable))
                {
                    this.pending.Add(unlockable);
                }
            }

            if (pending.Count > 0)
            {
                Log.Info($"Identified {pending.Count} pending unlocks for user {profile.name}.");
            }

            //unsubscribe so we don't try to recompute this list every frame
            //TODO: we probably do want SOME recompute logic to capture if you completed a challenge? maybe another hook?
            On.RoR2.Run.Update -= Run_Update;
        }

        private void HUD_ActivateScoreboard(On.RoR2.UI.HUD.orig_ActivateScoreboard orig, RoR2.UI.HUD self)
        {
            orig.Invoke(self);

            //make sure the panel is built before we try to show it, and only build it once
            if (!panelBuilt)
            {
                BuildPendingUnlocksPanel(self);
                panelBuilt = true;
            }

            pendingUnlocksPanel?.SetActive(true);
        }

        private void HUD_DeactivateScoreboard(On.RoR2.UI.HUD.orig_DeactivateScoreboard orig, RoR2.UI.HUD self)
        {
            orig.Invoke(self);

            //same as above, just for hiding the panel when the scoreboard is closed
            pendingUnlocksPanel?.SetActive(false);
        }
        #endregion

        /// <summary>
        /// Helper method that builds out the UI panel for the pending unlocks, displayed when you Tab during a game
        /// </summary>
        /// <param name="hud"></param>
        private void BuildPendingUnlocksPanel(RoR2.UI.HUD hud)
        {
            //we create and attach the panel to the mainContainer so it can be centered and have enough space
            pendingUnlocksPanel = new GameObject("PendingUnlocksPanel");
            pendingUnlocksPanel.transform.SetParent(hud.mainContainer.transform, false);

            //this defines the spacing and sizing of the panel itself, and anchors it relative to the parent (which is mainContainer)
            RectTransform rt = pendingUnlocksPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.4f);
            rt.anchorMax = new Vector2(0.5f, 0.4f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(560f, 340f);

            //this says that the content within the panel (the children pending unlock cards) will be stacked vertically and spaced evenly
            VerticalLayoutGroup outerLayout = pendingUnlocksPanel.AddComponent<VerticalLayoutGroup>();
            outerLayout.childForceExpandHeight = false;
            outerLayout.childForceExpandWidth = true;
            outerLayout.childControlWidth = true;
            outerLayout.childControlHeight = true;
            outerLayout.spacing = 9f;

            //start hidden, it will be shown when we open the Tab menu during a game
            pendingUnlocksPanel.SetActive(false);

            //TODO: currently just taking the first 5 unlocks, adding manual tracking here from the user would be better
            for (int i = 0; i < 5; i++)
            {
                //An UnlockableDef and an AchievementDef work together to give us the icon, title, and description for the unlock
                UnlockableDef def = pending[i];

                AchievementDef achievement = AchievementManager.GetAchievementDefFromUnlockable(def.cachedName);
                if (achievement == null)
                {
                    Log.Info($"WARNING: No achievement found for {def.cachedName ?? "Unlockable"}"); //TODO: I am not totally sure how all the unlocks/achievements are keyed together but this mostly works
                    continue;
                }

                //build out the card, trying to make this look the way that it does elsewhere in the game's UI (can't access the actual prefab from Unity) :P
                GameObject card = new GameObject("Card_" + (def.cachedName ?? "Unlockable_" + i));
                card.transform.SetParent(pendingUnlocksPanel.transform, false);

                LayoutElement cardLayout = card.AddComponent<LayoutElement>();
                cardLayout.preferredHeight = 73f;

                Image cardBg = card.AddComponent<Image>();
                cardBg.color = new Color(0f, 0f, 0f, 0.6f); // we're just rendering a dark, semi-transparent rectangle to serve as the bgfor the card

                HorizontalLayoutGroup cardInnerLayout = card.AddComponent<HorizontalLayoutGroup>();
                cardInnerLayout.childForceExpandWidth = false;
                cardInnerLayout.childForceExpandHeight = true;
                cardInnerLayout.childControlWidth = true;
                cardInnerLayout.childControlHeight = true;
                cardInnerLayout.padding = new RectOffset(9, 9, 9, 9);
                cardInnerLayout.spacing = 11f;

                //icon is just using the "unachieved icon" which is just the ? symbol
                GameObject iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(card.transform, false);
                LayoutElement iconLayout = iconGO.AddComponent<LayoutElement>();
                iconLayout.preferredWidth = 55f;
                iconLayout.preferredHeight = 55f;
                iconLayout.flexibleWidth = 0f; // this prevents stretching the icon
                Image iconImage = iconGO.AddComponent<Image>();
                iconImage.sprite = achievement.GetUnachievedIcon();
                iconImage.preserveAspect = true;

                //text is the name of the unlockable and the description of how to unlock it from the achievement, stacked vertically
                GameObject textColumn = new GameObject("TextColumn");
                textColumn.transform.SetParent(card.transform, false);
                LayoutElement textColumnLayout = textColumn.AddComponent<LayoutElement>();
                textColumnLayout.flexibleWidth = 1f; // and then this makes it so the text stretches to fill the rest of the card
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







