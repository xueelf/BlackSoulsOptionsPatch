# BlackSoulsOptionsPatch

An unofficial patch for [BLACK SOULS](https://www.dlsite.com/maniax/work/=/product_id/RJ251629.html) and [BLACK SOULS II](https://www.dlsite.com/maniax/work/=/product_id/RJ293183.html), which adds an in-game Options screen. You can change the window resolution and fullscreen mode, and adjust the volume of BGM, BGS, ME, and SE independently.

Read this in other languages: English | [中文](./README.zh.md) | [日本語](./README.ja.md)

## Features

- Options screen: adds an "Options" entry to the title screen and pause menu.
- Window resolution: supports 640×480, 1280×960, 1920×1440, and 2560×1920.
- Borderless fullscreen: fullscreen stretches the game's original 4:3 image to the screen aspect ratio.
- Volume controls: independent BGM, BGS, ME, and SE volume controls.
- Persistent settings: settings are saved in the `Options.ini` file in the game directory.

## Download

Download the exe for your game from [GitHub Releases](https://github.com/xueelf/BlackSoulsOptionsPatch/releases/latest):

BLACK SOULS:

- Chinese: `BLACK-SOULS-CN-options.exe`
- Japanese: `BLACK-SOULS-JP-options.exe`
- English: `BLACK-SOULS-EN-options.exe`

BLACK SOULS II:

- Chinese: `BLACK-SOULS-II-CN-options.exe`
- Japanese: `BLACK-SOULS-II-JP-options.exe`
- English: `BLACK-SOULS-II-EN-options.exe`

## Usage

1. Exit the game.
2. Place the exe for your game in the game directory, alongside `Game.exe` and `Game.rgss3a`.
3. Double-click the exe and select a number:

```text
1) Install/Update
2) Uninstall
3) Exit
```

During installation the original `Game.rgss3a` is backed up as `Game.rgss3a.bsopts.bak`. Uninstalling restores the original, and deletes the backup and `Options.ini`.

If the exe is not in the game directory, or `Game.exe` or `Game.rgss3a` is missing, the program will show an error message.

## Notice

The patch modifies game scripts, the exe icon uses assets from the game, and all rights belong to their respective owners. This project is for learning purposes only, so please do not promote or abuse it. Please support the official release.
