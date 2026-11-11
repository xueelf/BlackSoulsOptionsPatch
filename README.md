# BlackSoulsQoLPatch

An unofficial QoL patch for [BLACK SOULS](https://www.dlsite.com/maniax/work/=/product_id/RJ251629.html) and [BLACK SOULS II](https://www.dlsite.com/maniax/work/=/product_id/RJ293183.html), which adds an in-game Options screen, a battle log, and more. You can change the window resolution and fullscreen mode, and adjust the volume of BGM, BGS, ME, and SE independently.

Read this in other languages: English | [中文](./README.zh.md) | [日本語](./README.ja.md)

## Features

- Single exe: automatically detects BLACK SOULS or BLACK SOULS II and the game language.
- Options screen: adds menu entries to the title screen and pause menu. The screen is modeled after the default Options screen in RPG Maker MZ.
- Window resolution: supports 640×480, 1280×960, 1920×1440, and 2560×1920.
- Borderless fullscreen: fullscreen stretches the game's original 4:3 image to the screen aspect ratio.
- Volume controls: independent BGM, BGS, ME, and SE volume controls.
- Dash behavior: off by default. When enabled, the character dashes by default. Hold Shift to walk.
- Battle log: select "Log" from the battle commands to review the current battle's history.
- DPI scaling: enables Windows high-DPI compatibility for `Game.exe` to prevent stretching under display scaling.
- Persistent settings: settings are saved in the `Options.ini` file in the game directory.

## Download

Download the exe from [GitHub Releases](https://github.com/xueelf/BlackSoulsOptionsPatch/releases/latest):

- `BlackSoulsQoLPatch.exe`

## Usage

1. Exit the game.
2. Place the exe in the game directory of BLACK SOULS or BLACK SOULS II, alongside `Game.exe` and `Game.rgss3a`.
3. Double-click the exe and select a number:

```text
1) Install/Update
2) Uninstall
3) Exit
```

The installer follows your system language and falls back to English. The in-game text follows the detected game language.

During installation the original `Game.rgss3a` is backed up as `Game.rgss3a.bsopts.bak`. Uninstalling restores the original, deletes the backup and `Options.ini`, and removes the high-DPI compatibility setting.

If the exe is not in the game directory, or `Game.exe` or `Game.rgss3a` is missing, the program will show an error message.

## Notice

The patch modifies game scripts, the exe icon uses assets from the game, and all rights belong to their respective owners. This project is for learning purposes only, so please do not promote or abuse it. Please support the official release.
