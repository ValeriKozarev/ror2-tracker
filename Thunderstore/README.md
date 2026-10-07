# RoRTracker

Pick up to 5 challenges in the Logbook to track during your runs. Open the scoreboard with Tab during a game to see which challenges you're currently tracking, you can also change them from the in-game Logbook so you don't need to leave your run.

## Features

- **Track from the Logbook.** In the Logbook's Challenge tab, click any challenge you haven't completed to track it. Click it again to untrack it. A counter above the description shows how many of your 5 slots are in use.
- **See them mid-run.** Press Tab during a run to see your tracked challenges next to the scoreboard.
- **Change your picks any time.** Tracking also works from the Logbook in the pause menu, and the Tab panel updates right away.
- **Finished challenges clear themselves.** When you complete a tracked challenge, it drops off the list and frees up a slot.
- **Saved between sessions.** Your tracked challenges are stored in `BepInEx/config/Valerbear.RoRTracker.cfg`.

## Screenshots

Tracked challenges highlighted in the Logbook, with the counter:

![Logbook Challenge tab with tracked challenges highlighted](https://raw.githubusercontent.com/ValeriKozarev/ror2-tracker/main/screenshots/Capture.PNG)

Tracked challenges in the Tab panel during a run:

![Tracked challenges shown on the Tab screen during a run](https://raw.githubusercontent.com/ValeriKozarev/ror2-tracker/main/screenshots/Capture2.PNG)

## Installation

**With a mod manager (recommended):** install RoRTracker through r2modman or the Thunderstore Mod Manager. They install the required dependencies for you.

**Manually:**
1. Install [BepInExPack](https://thunderstore.io/c/riskofrain2/p/bbepis/BepInExPack/) and [HookGenPatcher](https://thunderstore.io/c/riskofrain2/p/RiskofThunder/HookGenPatcher/).
2. Download RoRTracker and copy `RoRTrackerPlugin.dll` into `Risk of Rain 2/BepInEx/plugins/RoRTracker/`.
3. Launch the game.

## Roadmap
Next up feature I'd like to add are the ability to track anything from the Logbook such as unlocking new items and survivors (not just challenges) and updating the in-game UI with live counters for the challengs that require you to do X in Y amount of time or in a single run.