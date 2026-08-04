# Risk of Rain 2 Challenge Tracker
A small mod for Risk of Rain 2 to make it easier to track unlocks and challenges, players will be able to select up to 5 challenges at a time to track from the Logbook that they can see within the Tab menu in-game.

## Installation instructions
todo: write this when done

## Next up tasks
1) find the relevant code for Main Menu > Logbook > Challenges
2) hook into click events on cards within this Challenges sub-menu so that we can trigger our own toggleTracking functionality
3) the "tracked" list itself should replace the "pending" list we have today and we should write it to a reasonable place on disk (maybe a JSON file within the BepInEx config system somewhere?)
4) implement the 5 item cap for selecting which challenges you're tracking within the Logbook, add some sort of simple UI like "3/5 challenges tracked" to make it clear


### Helpful resources for getting started
- [Risk of Rain 2 Modding Wiki](https://thunderstore.io/c/riskofrain2/p/tristanmcpherson/R2API/)
- [Basic Mod Setup Video](https://www.youtube.com/watch?v=awEzwX_B014)
- [dnSpyEx Decompiler](https://github.com/dnSpyEx/dnSpy/releases)
- [RoR2 Boilerplate](https://github.com/risk-of-thunder/r2boilerplate)