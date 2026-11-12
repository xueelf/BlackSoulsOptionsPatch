# BlackSoulsQoLPatch

An unofficial QoL patch for [BLACK SOULS](https://www.dlsite.com/maniax/work/=/product_id/RJ251629.html) and [BLACK SOULS II](https://www.dlsite.com/maniax/work/=/product_id/RJ293183.html), which adds an in-game Options screen, a battle log, and more. You can change the window resolution and fullscreen mode, and adjust the volume of BGM, BGS, ME, and SE independently.

Read this in other languages: English | [中文](./README.zh.md) | [日本語](./README.ja.md)

## Features

- Multilingual support: supports Chinese, English, and Japanese.
- Multi-platform support: works with both DLsite and Steam.
- Graphical interface: automatically detects the game, platform, language, and installation status.
- Options screen: adds menu entries to the title screen and pause menu. The screen is modeled after the default Options screen in RPG Maker MZ.
- Window resolution: supports 640×480, 1280×960, 1920×1440, and 2560×1920.
- Borderless fullscreen: fullscreen stretches the game's original 4:3 image to the screen aspect ratio.
- Volume controls: independent BGM, BGS, ME, and SE volume controls.
- Dash behavior: off by default. When enabled, the character dashes by default. Hold Shift to walk.
- Battle log: select "Log" from the battle commands to review the current battle's history.
- DPI scaling: enables Windows high-DPI compatibility for `Game.exe` to prevent stretching under display scaling.
- Persistent settings: settings are saved in the `Options.ini` file in the game directory.

## Download

Download the exe from [GitHub Releases](https://github.com/xueelf/BlackSoulsQoLPatch/releases/latest):

- `BlackSoulsQoLPatch.exe`

At runtime, the program automatically detects the game platform and game language. During installation, it writes the corresponding patch content based on the detection results.

The patch's in-game language and the program interface language are independent. The program interface language follows the operating system language and supports Chinese, English, and Japanese. If the operating system uses another language, the program interface uses English.

## Usage

1. Exit the game.
2. Place the exe in the game directory of BLACK SOULS or BLACK SOULS II, alongside `Game.exe` and `Game.rgss3a`.
3. Double-click the exe and use “Install / Update” or “Uninstall” in the window.

During installation the original `Game.rgss3a` is backed up as `Game.rgss3a.bsopts.bak`. Uninstalling restores the original, deletes the backup and `Options.ini`, and removes the high-DPI compatibility setting.

If the corresponding game is not detected, the program window shows the error reason and disables the relevant action buttons.

## Notice

The patch modifies game scripts, the exe icon uses assets from the game, and all rights belong to their respective owners. This project is for learning purposes only, so please do not promote or abuse it.
