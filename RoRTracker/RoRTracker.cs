using BepInEx;
using RoR2;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

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
        public const string PluginVersion = "0.1.0";

        GameObject pendingUnlocksPanel;
        TrackedChallenges trackedChallenges;

        // name and color of the highlight we add to each Logbook achievement tile
        const string TrackedHighlightName = "RoRTracker_TrackedHighlight";
        static readonly Color TrackedHighlightColor = new Color(0.85f, 0.65f, 0.13f, 0.35f); // color is amber currently but could be different OR a different indicator entirely

        // flag for wrapping the categories
        bool categoriesWrapped = false;
        readonly Dictionary<GameObject, UnityAction> trackedTileListeners = new Dictionary<GameObject, UnityAction>();

        //The Awake() method is run at the very start of the Unity Lifecycle when the game is initialized.
        public void Awake()
        {
            //Init our logging class so that we can properly log for debugging
            Log.Init(Logger);

            //loads the persisted set of tracked challenges via BepInEx config
            trackedChallenges = new TrackedChallenges(Config);

            //when an achievement is completed, notify our mod's UI as well in case its being tracked
            On.RoR2.UserProfile.AddAchievement += UserProfile_AddAchievement;

            //subscribe our HUD_Awake hook to the HUD.ActivateScoreboard and HUD.DeactivateScoreboard methods
            On.RoR2.UI.HUD.ActivateScoreboard += HUD_ActivateScoreboard;
            On.RoR2.UI.HUD.DeactivateScoreboard += HUD_DeactivateScoreboard;

            //our mod should fire when you view Challenges in the Logbook
            On.RoR2.UI.LogBook.CategoryDef.InitializeChallenge += CatergoriyDef_InitializeChallenge;

            //BuildEntriesPage re-derives disablePointerClick/disableGamepadClick itself, after calling
            //initializeElementGraphics, based on per-slot logic that can undo the re-enable we do there
            //(see the comment on the hook itself). Re-apply it once more after BuildEntriesPage fully finishes.
            On.RoR2.UI.LogBook.LogBookController.BuildEntriesPage += LogBookController_BuildEntriesPage;

            //TODO: maybe we want to create a new logbook page or similar that is just for the pending unlocks so you can view that from the main menu as well?
            // ^^ currently this implementation just lets you select achievements directly for tracking as a proof of concept
        }

        #region Hooks
        private void UserProfile_AddAchievement(On.RoR2.UserProfile.orig_AddAchievement orig, RoR2.UserProfile self, string achievementName, bool isExternal)
        {
            orig.Invoke(self, achievementName, isExternal);
            
            trackedChallenges.TryUntrack(achievementName);
        }

        private void CatergoriyDef_InitializeChallenge(On.RoR2.UI.LogBook.CategoryDef.orig_InitializeChallenge orig, GameObject tileObject, RoR2.UI.LogBook.Entry entry, RoR2.UI.LogBook.EntryStatus status, UserProfile viewerProfile)
        {
            orig.Invoke(tileObject, entry, status, viewerProfile);
            ApplyAchievementTileTracking(tileObject, entry, status);
        }

        private void HUD_ActivateScoreboard(On.RoR2.UI.HUD.orig_ActivateScoreboard orig, RoR2.UI.HUD self)
        {
            orig.Invoke(self);

            // make sure the panel is built before we try to show it, and only build it once per HUD
            if (pendingUnlocksPanel == null)
            {
                BuildPendingUnlocksPanel(self);
            }

            PopulatePendingUnlocksPanel();
            pendingUnlocksPanel?.SetActive(true);
        }

        private void HUD_DeactivateScoreboard(On.RoR2.UI.HUD.orig_DeactivateScoreboard orig, RoR2.UI.HUD self)
        {
            orig.Invoke(self);

            // panel may have been destroyed with the previous run's HUD, this helps us avoid any trouble. 
            if (pendingUnlocksPanel != null)
                pendingUnlocksPanel.SetActive(false);
        }

        /// <summary>
        /// We need to have a second hook because the game determines tile cliackability multiple times and this can be overwritten.
        /// </summary>
        private GameObject LogBookController_BuildEntriesPage(On.RoR2.UI.LogBook.LogBookController.orig_BuildEntriesPage orig, RoR2.UI.LogBook.LogBookController self, object navigationPageInfo)
        {
            GameObject page = orig.Invoke(self, (RoR2.UI.LogBook.LogBookController.NavigationPageInfo)navigationPageInfo);

            foreach (GameObject tileObject in trackedTileListeners.Keys)
            {
                if (tileObject == null)
                    continue;

                RoR2.UI.HGButton button = tileObject.GetComponent<RoR2.UI.HGButton>();
                if (button != null)
                {
                    button.disablePointerClick = false;
                    button.disableGamepadClick = false;
                }
            }

            return page;
        }
        #endregion

        /// <summary>
        /// Applies our clickability and highlight logic to a Logbook achievement tile, if it's an achievement tile and not already completed.
        /// </summary>
        private void ApplyAchievementTileTracking(GameObject tileObject, RoR2.UI.LogBook.Entry entry, RoR2.UI.LogBook.EntryStatus status)
        {
            // if not an achievement tile
            if (!(entry.extraData is AchievementDef achievementDef))
            {
                return;
            }

            // if achievement is already completed
            if (status == RoR2.UI.LogBook.EntryStatus.Available || status == RoR2.UI.LogBook.EntryStatus.New)
                return; // TO-DO maybe this will cause a bug with tracked achievements being impossible to untrack once comlpeted?

            string achievementId = achievementDef.identifier;

            RoR2.UI.HGButton button = tileObject.GetComponent<RoR2.UI.HGButton>();
            // we want to re-enable the button's clickability, since vanilla disables it for completed achievements
            if (button != null)
            {
                button.disablePointerClick = false;
                button.disableGamepadClick = false;

                if (trackedTileListeners.TryGetValue(tileObject, out UnityAction previousListener))
                    button.onClick.RemoveListener(previousListener);

                UnityAction listener = () => OnAchievementTileClicked(tileObject, achievementId);
                trackedTileListeners[tileObject] = listener;
                button.onClick.AddListener(listener);
            }

            ApplyTrackedHighlight(tileObject, achievementId);
        }

        /// <summary>
        /// Toggles tracking for the clicked challenge.
        /// </summary>
        private void OnAchievementTileClicked(GameObject tileObject, string achievementId)
        {
            // toggle functionality, try to untrack if tracked
            if (trackedChallenges.IsTracked(achievementId))
            {
                trackedChallenges.TryUntrack(achievementId);
            }
            // else we'lltry to track, if returns false then we've already hit max
            else if (!trackedChallenges.TryTrack(achievementId))
            {
                Log.Info($"Not tracking '{achievementId}': already tracking {TrackedChallenges.MaxTracked} challenges.");
                return;
            }

            ApplyTrackedHighlight(tileObject, achievementId);
        }

        /// <summary>
        /// Adds a highligh to the achievement tile if it's being tracked, or removes it if it's not.
        /// </summary>
        private void ApplyTrackedHighlight(GameObject tileObject, string achievementId)
        {
            Transform highlightTransform = tileObject.transform.Find(TrackedHighlightName);
            GameObject highlight;
            if (highlightTransform == null)
            {
                highlight = new GameObject(TrackedHighlightName);
                highlight.transform.SetParent(tileObject.transform, false);

                // highlight the selected tile
                RectTransform rt = highlight.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                highlight.transform.SetAsLastSibling(); // TODO this is supposed to make the highlight render on top of the tile but it doesn't work

                Image newImage = highlight.AddComponent<Image>();
                newImage.raycastTarget = false;
            }
            else
            {
                highlight = highlightTransform.gameObject;
            }

            Image highlightImage = highlight.GetComponent<Image>();
            highlightImage.color = trackedChallenges.IsTracked(achievementId) ? TrackedHighlightColor : Color.clear;
        }

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
        }

        private void PopulatePendingUnlocksPanel()
        {
            //first we need to clear the old UI cards from the panel, if they exist
            Transform panelTransform = pendingUnlocksPanel.transform;
            for (int i = panelTransform.childCount - 1; i >= 0; i--)
            {
                GameObject oldChild = panelTransform.GetChild(i).gameObject;
                oldChild.SetActive(false); // layout groups ignore inactive children, so they stop taking up space this frame
                Destroy(oldChild);         // actually removed at the end of the frame
            }

            //load our challenges for populating the panel
            IReadOnlyCollection<string> ids = trackedChallenges.TrackedIds;
            UserProfile profile = LocalUserManager.GetFirstLocalUser()?.userProfile;

            int cardsBuilt = 0;

            foreach (string id in ids)
            {
                AchievementDef achievement = AchievementManager.GetAchievementDef(id);
                if (achievement == null)
                {
                    Log.Info($"WARNING: No achievement found for {id}");
                    continue;
                }
                if (profile != null && profile.HasAchievement(id))
                {
                    Log.Info($"Achievement in tracked list {id} is already completed, skipping");
                    trackedChallenges.TryUntrack(id); // remove it from the tracked list since it's already completed
                    continue;
                }

                //build out the card, trying to make this look the way that it does elsewhere in the game's UI (can't access the actual prefab from Unity) :P
                GameObject card = new GameObject("Card_" + id);
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
                titleText.text = Language.GetString(achievement.nameToken);
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

                cardsBuilt++;
            }

            // this branch is when there are no tracked challenges
            if (cardsBuilt == 0)
            {
                GameObject emptyGO = new GameObject("EmptyMessage");
                emptyGO.transform.SetParent(pendingUnlocksPanel.transform, false);

                LayoutElement emptyLayout = emptyGO.AddComponent<LayoutElement>();
                emptyLayout.preferredHeight = 40f;

                TextMeshProUGUI emptyText = emptyGO.AddComponent<TextMeshProUGUI>();
                emptyText.text = "No challenges tracked. Pick some in the Logbook.";
                emptyText.fontSize = 16f;
                emptyText.color = Color.gray;
                emptyText.alignment = TextAlignmentOptions.Center;
            }
        }
    }
}
