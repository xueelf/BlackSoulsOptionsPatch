# BlackSoulsQoLPatch

游戏 [BLACK SOULS](https://www.dlsite.com/maniax/work/=/product_id/RJ259844.html) 与 [BLACK SOULS II](https://www.dlsite.com/maniax/work/=/product_id/RJ280293.html) 的非官方 QoL 优化补丁，在程序内添加「选项」界面和战斗日志。可修改窗口分辨率、全屏显示，以及提供独立的 BGM、BGS、ME、SE 音量控制。

使用其他语言阅读：[English](./README.md) | 中文 | [日本語](./README.ja.md)

## 特性

- 单文件：自动识别 BLACK SOULS / BLACK SOULS II 和游戏语言。
- 选项界面：在标题画面和暂停菜单里添加菜单入口，界面参考 RPG Maker MZ 的默认选项。
- 窗口分辨率：支持 640×480、1280×960、1920×1440、2560×1920 四种分辨率。
- 无边框全屏：全屏会将游戏原本的 4:3 画面强行拉伸到屏幕比例。
- 音量控制：独立的 BGM、BGS、ME、SE 音量控制。
- 奔跑模式：默认关闭。开启后角色默认奔跑，按住 Shift 走路。
- 战斗日志：在战斗指令中选择「日志」，可查看本场战斗的历史记录。
- DPI 缩放：为 `Game.exe` 启用 Windows 高 DPI 兼容性，避免系统缩放导致画面拉伸。
- 配置持久化：设置会保存在游戏目录的 `Options.ini` 文件中。

## 下载

从 [GitHub Releases](https://github.com/xueelf/BlackSoulsOptionsPatch/releases/latest) 下载 exe：

- `BlackSoulsQoLPatch.exe`

## 使用

1. 退出游戏。
2. 将 exe 放到 BLACK SOULS 或 BLACK SOULS II 的游戏目录，与 `Game.exe`、`Game.rgss3a` 同级。
3. 双击 exe，按数字选择：

```text
1) 安装/更新
2) 卸载
3) 退出
```

安装器界面跟随系统语言，没有对应语言时使用英语。游戏内文案跟随检测到的游戏语言。

安装时会将游戏原版 `Game.rgss3a` 备份为 `Game.rgss3a.bsopts.bak`。卸载会恢复原版，删除备份和 `Options.ini` 文件，并移除高 DPI 兼容性设置。

如果 exe 不处于游戏目录内，或缺少 `Game.exe`、`Game.rgss3a` 文件，程序会提示错误信息。

## 声明

补丁会修改游戏脚本，exe 图标使用了游戏本体资源，版权归相关方所有。本项目仅供学习用途，请勿肆意宣传滥用。请支持正版游戏。
