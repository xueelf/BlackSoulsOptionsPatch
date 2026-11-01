# BlackSoulsOptionsPatch

游戏 [BLACK SOULS](https://www.dlsite.com/maniax/work/=/product_id/RJ251629.html) 的非官方优化补丁，在程序内添加「设置」界面。可修改窗口分辨率、全屏显示，以及提供独立的 BGM、BGS、ME、SE 音量控制。

使用其他语言阅读：[English](./README.md) | 中文 | [日本語](./README.ja.md)

## 功能

- 设置界面：在标题画面和暂停菜单里添加「设置」入口。
- 窗口分辨率：支持 640×480、1280×960、1920×1440、2560×1920 四种分辨率。
- 无边框全屏：全屏会将游戏原本的 4:3 画面强行拉伸到屏幕比例。
- 音量控制：独立的 BGM、BGS、ME、SE 音量控制。
- 配置持久化：设置会保存在游戏目录的 `Options.ini` 文件中。

## 下载

从 [GitHub Releases](https://github.com/xueelf/BlackSoulsOptionsPatch/releases/latest) 下载对应语言的 exe：

- 中文：`BLACK-SOULS-CN-options.exe`
- 日文：`BLACK-SOULS-JP-options.exe`
- 英文：`BLACK-SOULS-EN-options.exe`

## 使用

1. 退出游戏。
2. 将对应语言的 exe 放到游戏目录，与 `Game.exe`、`Game.rgss3a` 同级。
3. 双击 exe，按数字选择：

```text
1) 安装/更新
2) 卸载
3) 退出
```

安装时会将游戏原版 `Game.rgss3a` 备份为 `Game.rgss3a.bsopts.bak`。卸载会恢复原版，并删除备份和 `Options.ini`。

安装完成后启动游戏，在标题画面或暂停菜单里选择「设置」。如果 exe 不在游戏目录，或缺少 `Game.exe`、`Game.rgss3a`，程序会提示后按任意键退出。

## 声明

补丁会修改游戏脚本，exe 图标使用了游戏本体资源，版权归相关方所有。本项目仅供学习用途，请勿肆意宣传滥用。请支持正版游戏。
