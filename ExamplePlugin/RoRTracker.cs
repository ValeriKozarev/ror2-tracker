using BepInEx;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

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

        //The Awake() method is run at the very start when the game is initialized.
        public void Awake()
        {
            //Init our logging class so that we can properly log for debugging
            Log.Init(Logger);

            // load the local XML file for the player's current progress (C:\Steam\userdata\80402371\632360\remote\UserProfiles, mine is called 62675123-06c8-42f2-bfe1-e42e2f824fe5.xml
            // and I copied it into /Resources as well

            // load the local XML file that has a completed, 100% unlock for diffing - or alternatively, can we find how the game is tracking incomplete challenges?

            // looks like the UI is doing some separate process to determine if you completed a challenge. The XML only contains corresponding unlock tags if you actually got the unlock already
        }

        // Start() is run after Awake(), and is used to initialize things that require other plugins to be initialized first.
        public void Start()
        {

        }


        //The Update() method is run on every frame of the game.
        private void Update()
        {
            // TODO: Add code here to run every frame, if needed.
        }
    }
}
