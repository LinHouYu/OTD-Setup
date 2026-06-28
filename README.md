# OTD-Setup 一键部署工具

本项目旨在为 FPS 与数位板玩家提供最极简的 OpenTabletDriver (OTD) 驱动安装与自启配置方案。程序内置完整运行环境，开箱即用。

## ⚠️ 许可协议
**严禁商用**。本项目及其附带脚本仅供个人学习、交流及娱乐使用。未经原作者明确书面授权，任何个人或组织不得将本项目的任何部分用于商业盈利目的（包括但不限于淘宝/闲鱼代售、捆绑收费软件等）。

## 📥 下载地址
使用以下链接即可直接下载最新版本的安装程序。

**最新版直连下载：**
> `https://github.com/LinHouYu/OTD-Setup/releases/latest/download/OTD-Setup.exe`

**加速下载节点：**
若由于网络环境导致上述链接下载缓慢，请在上述下载链接前，直接拼接以下任意一个加速节点的前缀：
1. `https://ghproxy.net/`
2. `https://github.moeyy.xyz/`
3. `https://hub.gitmirror.com/`

*(使用示例：`https://ghproxy.net/https://github.com/LinHouYu/OTD-Setup/releases/latest/download/OTD-Setup.exe`)*

## 🚀 使用说明
本程序已实现“自包含”，无需配置任何复杂的 .NET 环境。

1. **直接运行**：下载完成后，双击打开 `OTD-Setup.exe`。
2. **授予权限**：部署过程中若弹出 UAC (用户账户控制) 提示，请无脑点击“同意/是”。
3. **重启系统**：程序闪过黑框配置结束后，请务必**重启电脑**。重启后驱动服务与托盘自启策略将完全生效。

## ⚙️ 灵敏度推荐与调整
程序内已预设了针对 FPS 玩家的最佳参数。本人作为高灵敏度玩家，默认主要参数设定为 **12**。

**如何调整：**
打开托盘中的软件主界面，找到灵敏度/区域映射设置选项卡。
* **高敏玩家**：保持默认 **12** 即可。
* **低敏玩家**：若感觉滑动过快或不可控，建议将 X/Y 轴灵敏度均修改为 **9**。

**保存生效：**
修改任意参数后，请务必依次点击界面上的 **`Save` (保存)**，然后再点击 **`Apply` (应用)** 使其生效。

## 💬 社区与反馈
欢迎加入“邪教”数位板交流群。若程序出现 Bug 或配置异常，请优先在 GitHub Issues 提交报告。

* **问题报告**：[提交 Issue](https://github.com/LinHouYu/OTD-Setup/issues)
* **技术研讨 (QQ群)**：`325318218`
* **日常分享 (Telegram)**：[t.me/zhishifenzi8266](https://t.me/zhishifenzi8266)

## ☕ 支持与打赏
如果这个小工具为你节省了折腾的时间，欢迎请作者喝杯咖啡：

<img width="1037" height="1037" alt="1175CC4181FC60F1F8098A4AC99ADF58" src="https://github.com/user-attachments/assets/71293586-a5d7-4904-babd-c421316f51aa" />


## 🙏 鸣谢 (Credits)
本项目的核心驱动与过滤算法离不开以下开源开发者的伟大贡献，感谢他们提供的底层软件与驱动支持：
* **[OpenTabletDriver](https://github.com/OpenTabletDriver/OpenTabletDriver)**
* **[X9VoiD/vmulti-bin](https://github.com/X9VoiD/vmulti-bin)**
* **[TabletDriverFilters](https://github.com/OpenTabletDriver/TabletDriverFilters)**
