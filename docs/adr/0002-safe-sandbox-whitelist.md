# 2. 社区安装器与模组包的严苛安全白名单与沙箱防线

## 上下文与问题陈述
社区模组与第三方 `.ummpk` 模组包来自不可信网络或外部玩家分享。若允许自由解压，存在压缩包炸弹、路径穿越（`..` 逃逸）、写入 Windows 启动项或覆盖游戏核心可执行文件（如 `Unturned.exe`、`winhttp.dll`）的严重安全隐患。

## 决策
我们在 [`CommunityModInstaller`](file:///D:/Agent-工作目录/DevelopMyUNMultiplayerModAndModloader/启动器/UnturnedModManager/Services/CommunityModInstaller.cs) 与 [`PluginProfileService`](file:///D:/Agent-工作目录/DevelopMyUNMultiplayerModAndModloader/启动器/UnturnedModManager/Services/PluginProfileService.cs) 中实施了**不可妥协的严苛目录白名单与多层防御流水线**。

## 核心防护规则
1. **严格白名单路径**：仅允许将文件解压并写入 `BepInEx/plugins/**` 和 `BepInEx/config/**` 两个目标目录，禁止向游戏根目录、Unity 数据目录或任何其他系统目录写入任何文件。
2. **危险扩展名全面拦截**：严禁压缩包包含 `.exe`、`.bat`、`.cmd`、`.ps1`、`.vbs`、`.reg`、`.msi`、`.scr` 等脚本或可执行文件。
3. **体积与条目防护**：限制解压条目数上限（4096 条）与总解压体积（2GB 上限），防御 Zip 炸弹。
4. **有效载荷规则**：安装包必须包含至少一个位于 `BepInEx/plugins/` 下的合法 `.dll` 插件，无有效载荷时直接拒绝。
