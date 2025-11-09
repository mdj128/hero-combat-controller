# Hero Combat Controller (UPM)

A drop-in third-person combat controller for Unity 2021.3+ that ships with runtime scripts, NPC AI, auto-wiring editor tooling, and reusable HUD/animation assets. This folder is self-contained so you can move it into its own Git repository and distribute it as a Unity Package Manager (UPM) package.

## Contents

- **Runtime** – `HeroCharacterController`, combat agent, NPC AI, floating combat text, world-space health bars, prefabs, and the shared animator controller.
- **Editor** – the Hero Combat Auto Wiring window that configures heroes/NPCs, adds colliders, weapons, HUD bindings, and floating damage text with a single click.
- **Prefabs & Assets** – `Runtime/Prefabs/HeroCombatHUD.prefab` and `Runtime/Animation/HeroCombatAnimator.controller` are included so you can ship default UI/animation data with the package.

## Install (UPM)

1. Move `Packages/com.herocharacter.herocombat` into its own repository (or keep it as an embedded package).
2. Add a `package.json` to the repository root (already included) and push to GitHub.
3. In other projects, open `Packages/manifest.json` and add:
   ```json
   "com.herocharacter.herocombat": "https://github.com/your-org/hero-combat.git#1.0.0"
   ```
4. Unity will import the runtime/editor assemblies automatically. Enable the Input System package if it isn’t already installed.

## Quick Start

1. Drop the **HeroCombatHUD** prefab into your canvas (optional – Hero HUD only).
2. Run **Hero Character → Combat Auto Wiring** from the Unity menu.
3. Select your hero GameObject and click **Configure Hero** – this adds the controller, Input System bindings, floating damage text, and sets up the camera.
4. Select an enemy prefab and click **Configure NPC** – this wires NPC combat, animator driver, melee weapon (auto-finding the right-hand bone by default), floating text, and a world-space health bar.
5. Enter Play Mode and use your existing animations/rigs with the provided controller + HUD.

## Requirements

- Unity **6000.2 LTS** or newer
- **Input System** package (`com.unity.inputsystem`)
- (Recommended) Kevin Iglesias free animation packs:
  - [Human Basic Motions](https://assetstore.unity.com/packages/3d/animations/human-basic-motions-free-154271)
  - [Human Melee Animations](https://assetstore.unity.com/packages/3d/animations/human-melee-animations-free-165785)

The included `HeroCombatAnimator.controller` references clips from those packs. If you skip them, swap in your own animations for every state inside the controller.

For full documentation and samples see the original repository or the comments in the scripts.
