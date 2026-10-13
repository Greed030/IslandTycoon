# Island Tycoon

A cozy 2D isometric resort tycoon for mobile (landscape), made in Unity 6 (6000.0 LTS, URP 2D).

Guests arrive in waves and ask for Rooms, Drinks, Food, Relax, Ocean View and Spa. Move workers between buildings to fix bottlenecks, keep satisfaction high, earn coins and stars, upgrade buildings, unlock new areas and buy a second island. Goal: finish 10 waves on Coral Lagoon to become **Resort Master**.

## Run
1. Open the folder in Unity Hub with **Unity 6000.0.x LTS**.
2. Open `Assets/IslandTycoon/IslandTycoon.unity` (or use the menu **Island Tycoon → Play**).
3. Press Play. The whole game builds itself at runtime, no scene wiring needed.

## Controls
- Tap a building: workers, upgrade, boost, unlock
- Drag: move the camera; mouse wheel / pinch: zoom
- Map button: islands; gear: settings

## Project layout
- `Assets/IslandTycoon/Scripts/Core` – pure game logic (`GameModel`), config, save, self test
- `Assets/IslandTycoon/Scripts/View` – world rendering, camera, audio (synthesised), controller
- `Assets/IslandTycoon/Scripts/UI` – runtime-built uGUI
- `Assets/IslandTycoon/Editor` – sprite import settings and the **Island Tycoon** menu
- `Assets/IslandTycoon/Resources/IslandTycoon/GameConfig.asset` – all balance numbers (edit in Inspector)

Menu **Island Tycoon → Run Self Test** checks every state transition and prints a balance report.

## Credits
- Art generated with ChatGPT image generation
- Font: Fredoka One (SIL Open Font License 1.1)
- Code: written with Claude
