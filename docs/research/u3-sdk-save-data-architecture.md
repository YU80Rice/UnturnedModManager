# Unturned 游戏底层数据架构与调用链调研报告 (U3-SDK 权威源码事实)

本报告深入调查位于本地路径 `D:\Agent-工作目录\U3-SDK` 的 Unturned 官方游戏 C# 源码，查明角色存档（`PlayerSavedata`）、世界存档与难度配置（`ServerSavedata` / `Config.json` / `ModeConfigData`）以及核心大世界物理数据（`Barricades.dat` / `Structures.dat` / `Vehicles.dat`）的真实底层数据架构、序列化格式与调用链路。

---

## 1. 角色数据调用链 (PlayerSavedata 架构)

### 1.1 核心类与源文件索引
- **存档调用入口**：`SDG.Unturned.PlayerSavedata`（位于 `Assets/Runtime/Assembly-CSharp/Unturned/Files/PlayerSavedata.cs:7-124`）
- **服务器文件存储层**：`SDG.Unturned.ServerSavedata`（位于 `Assets/Runtime/Assembly-CSharp/Unturned/Files/ServerSavedata.cs:7-150`）
- **底层 IO 与 Cloud 代理**：`SDG.Unturned.ReadWrite`（位于 `Assets/Runtime/Assembly-CSharp/Unturned/Files/ReadWrite.cs:21-480`）
- **根目录定义**：`SDG.Unturned.UnturnedPaths`（位于 `Assets/Runtime/Assembly-CSharp/Unturned/Files/UnturnedPaths.cs:10-34`）
- **角色槽位与外观管理器**：`SDG.Unturned.Characters`（位于 `Assets/Runtime/Assembly-CSharp/Unturned/Menu/Characters.cs:14-1203`）
- **生命与状态管理**：`SDG.Unturned.PlayerLife`（位于 `Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerLife.cs:28-2558`）
- **服装与装备管理**：`SDG.Unturned.PlayerClothing`（位于 `Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerClothing.cs:27-2040`）
- **背包与网格物品管理**：`SDG.Unturned.PlayerInventory`（位于 `Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerInventory.cs:28-2011`）
- **技能与声望管理**：`SDG.Unturned.PlayerSkills`（位于 `Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerSkills.cs:25-1035`）
- **任务与标记管理**：`SDG.Unturned.PlayerQuests`（位于 `Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerQuests.cs:23-2905`）
- **玩家出生与基础坐标**：`SDG.Unturned.Player`（位于 `Assets/Runtime/Assembly-CSharp/Unturned/Player/Player.cs:190, 1834-1848`）

---

### 1.2 物理路径生成规则与云存档/单机存档的真实映射关系

#### (1) 单机与服务器路径生成规约
Unturned 在进入游戏前，在 `Provider.cs` 中确定存储目录与标识：
1. **单机模式初始化**（`Provider.cs:2054`）：
   ```csharp
   Dedicator.serverID = "Singleplayer_" + Characters.selected;
   ```
   其中 `Characters.selected` 为选中的角色槽位（0~4，0为免费槽位，1~4为黄金版 Pro 槽位，定义于 `Customization.cs:11-12`）。
2. **目录前缀判定**（`ServerSavedata.cs:9-37`）：
   - 当 `Dedicator.IsDedicatedServer == false`（单机或局域网房主），`ServerSavedata.directory` 返回 `"/Worlds"`。
   - 当 `Dedicator.IsDedicatedServer == true`（独立专用服务器），返回 `"/Servers"`。
3. **玩家数据路径生成**（`PlayerSavedata.cs:19, 43, 55, 67`）：
   ```csharp
   ServerSavedata.writeBlock("/Players/" + playerID.steamID + "_" + playerID.characterID + "/" + Level.info.name + path, block);
   ```
4. **完整物理路径**（拼接 `ReadWrite.PATH` 即 `UnturnedPaths.RootDirectory`）：
   - **单机生存存档实际磁盘路径**：
     `{UnturnedRoot}/Worlds/Singleplayer_{SlotId}/Players/{SteamId}_{CharacterId}/{MapName}/Player/{FileName}.dat`
     *（注：在单机模式下，`SlotId` 与 `CharacterId` 恒等，均取当前 `Characters.selected`，范围 `0~4`）*
   - **服务器生存存档实际磁盘路径**：
     `{UnturnedRoot}/Servers/{ServerId}/Players/{SteamId}_{CharacterId}/{MapName}/Player/{FileName}.dat`

#### (2) Steam 云存档 vs 单机/本地存档的真实机制与读取优先级
许多社区玩家和第三方工具误以为“角色数据在 Steam Cloud 的 Slots 文件夹中”，**SDK 源码推翻了这一猜测**：
1. **玩家生存数据完全不走 Steam Cloud**：
   - 查看 `PlayerSavedata.cs:13-20`：`hasSync` 字段默认为 `false`（只有在控制台执行遗留的 `sync` 命令时才由 `CommandSync.cs:24` 置为 true，此时写入游戏目录的 `/Sync/` 文件夹）。
   - 当 `hasSync == false` 时，所有生命、物品、装备、技能、任务全部调用 `ServerSavedata`，其底层调用 `ReadWrite.writeBlock(..., false, block)`，`useCloud` 参数**显式传为 `false`**！
   - **结论**：`Life.dat`, `Clothing.dat`, `Inventory.dat`, `Skills.dat`, `Quests.dat`, `Player.dat` 纯粹由本地/服务器主机持有，**永远不上传 Steam Cloud**。
2. **真正保存在 Steam Cloud 的数据**：
   - 角色全局外貌卡槽文件 `/Characters.dat`：`Characters.cs:944, 1201` 中调用 `ReadWrite.readBlock("/Characters.dat", true, 0)` 与 `ReadWrite.writeBlock("/Characters.dat", true, block)`（`useCloud = true`）。
   - 通过 `SteamworksCloudService.cs:11-88` 调用 Steam 官方 API `SteamRemoteStorage.FileRead/FileWrite`，其真实磁盘位置在 Steam 客户端专属目录：`<SteamInstallPath>/userdata/<SteamAccountID>/304930/remote/Characters.dat`。
   - 本地游戏目录中的 `/Cloud/` 文件夹（如 `UnturnedPaths.RootDirectory/Cloud/`）仅用于存放编辑器预设（`Selection.tool`, `ConvenientSavedata.json`, `BlockedPlayers.bin` 等），并不是 Steam 云端同步区。
3. **单机角色与单机世界的一一对应隔离**：
   - 切换角色槽位（0~4）会导致 `Characters.selected` 改变，单机世界的根目录 `Worlds/Singleplayer_{SlotId}` 直接发生切换。
   - 这意味着 **Slot 0 的单机世界与 Slot 1 的单机世界是彻底物理隔离的两个世界**。

---

### 1.3 核心角色 `.dat` 序列化协议与数据结构

Unturned 角色 `.dat` 文件**没有通用的魔数（Magic Number）**，均以 **1 字节的 `byte SAVEDATA_VERSION` 开头**。

| 文件名 | 读写入口方法与行号 | 序列化工具 | 最新版本号 | 数据结构与字段顺序 |
| :--- | :--- | :--- | :--- | :--- |
| **`Player.dat`** | `Player.cs:1834-1848`<br>`Provider.cs:1408-1431` | `Block` | `1` | 1. `byte version` (`1`)<br>2. `Vector3 savePosition` (3个 `float`，共 12 字节)<br>3. `byte saveRotation` (水平航向角偏航值) |
| **`Life.dat`** | `PlayerLife.cs:2481-2555` | `Block` | `3` (`SAVEDATA_VERSION_WITH_OXYGEN`) | 1. `byte version` (`3`)<br>2. `byte health` (生命)<br>3. `byte food` (饥饿)<br>4. `byte water` (口渴)<br>5. `byte virus` (感染)<br>6. `byte oxygen` (氧气，v3引入)<br>7. `bool isBleeding` (流血)<br>8. `bool isBroken` (骨折)<br>*注：玩家死亡时文件会被 `deleteFile` 彻底删除* |
| **`Clothing.dat`** | `PlayerClothing.cs:1832-2038` | `Block` | `7` | 1. `byte version` (`7`)<br>2. 依次写入 7 件装备（衬衫, 裤子, 帽子, 背包, 防弹衣, 面罩, 眼镜）：<br>   - 每件写入 `System.Guid` (16 字节，v7由 ushort ID 升级为 Guid)<br>   - 每件写入 `byte quality` (品质 0~100)<br>3. `bool isVisual` (是否显示外观服饰)<br>4. `bool isSkinned` (是否启用皮肤)<br>5. `bool isMythic` (是否启用特效)<br>6. 依次写入 7 件装备的二进制状态数组 `byte[] state`（调用 `writeByteArray`，每个由 1 字节长度 + 内容组成） |
| **`Inventory.dat`** | `PlayerInventory.cs:1865-2010` | `Block` | `5` | 1. `byte version` (`5`)<br>2. 遍历 7 个背包页面（0:Primary主武器, 1:Secondary副武器, 2:Hands手部主槽, 3:Backpack, 4:Vest, 5:Shirt, 6:Pants）：<br>   - `byte width` (页宽)<br>   - `byte height` (页高)<br>   - `byte itemCount` (物品总数)<br>   - 遍历每个物品：<br>     - `byte x`<br>     - `byte y`<br>     - `byte rot` (旋转方向，v5引入)<br>     - `ushort id` (物品 ID)<br>     - `byte amount` (堆叠数量/弹药)<br>     - `byte quality` (品质 0~100)<br>     - `byte[] state` (武器配件/状态字节数组) |
| **`Skills.dat`** | `PlayerSkills.cs:955-1025` | `Block` | `7` | 1. `byte version` (`7`)<br>2. `uint32 experience` (当前经验值)<br>3. `int32 reputation` (声望正负值，v7引入)<br>4. `byte boost` (当前升级加速状态)<br>5. 嵌套遍历 3 大分支（Offense/Defense/Support）的所有技能等级：<br>   - 每个技能写入 1 字节 `byte level` |
| **`Quests.dat`** | `PlayerQuests.cs:2656-2905` | `River` | `11` (`SAVEDATA_VERSION_ADDED_NPC_CUTSCENE_MODE`) | 1. `byte version` (`11`)<br>2. `bool isMarkerPlaced` + `Vector3 markerPosition`<br>3. `uint32 radioFrequency` (电台频率)<br>4. `CSteamID groupID` + `byte groupRank` + `bool inMainGroup`<br>5. `ushort flagCount` + [遍历每个标志：`ushort id` + `int16 value`]<br>6. `int32 questCount` + [遍历每个任务：`Guid assetGuid` (16 字节)]<br>7. `Guid trackedQuestGuid` (正在追踪的任务 Guid)<br>8. `string npcSpawnId` (变长字符串)<br>9. `bool npcCutsceneMode` |

---

## 2. 世界数据调用链 (ServerSavedata & Config.json)

### 2.1 核心类与源文件索引
- **世界文件存储代理**：`ServerSavedata`（`Unturned/Files/ServerSavedata.cs`）
- **关卡文件存储代理**：`LevelSavedata`（`Unturned/Files/LevelSavedata.cs:7-48`）
- **玩法与模式配置模型**：`SDG.Unturned.PlayConfigData` / `ConfigData` / `ModeConfigData`（`Unturned/Settings/PlayConfigData.cs:22-535, 2520-2650`）
- **配置加载与格式迁移**：`SDG.Unturned.Provider.LoadGameplayConfig`（`Unturned/Provider/Provider.cs:2178-2300`）
- **路障系统**：`SDG.Unturned.BarricadeManager`（`Unturned/Managers/BarricadeManager.cs:50-90, 2965-3160`）
- **建筑系统**：`SDG.Unturned.StructureManager`（`Unturned/Managers/StructureManager.cs:26-35, 1145-1270`）
- **载具系统**：`SDG.Unturned.VehicleManager`（`Unturned/Managers/VehicleManager.cs:27-34, 2960-3320`）

---

### 2.2 `Config.json` 完整数据体系与 V2 `UnturnedDat` 演进

#### (1) 配置文件存储位置
- **单机位置**：
  - V1 (遗留 JSON)：`{UnturnedRoot}/Worlds/Singleplayer_{SlotId}/Config.json`
  - V2 (新型 UnturnedDat)：`{UnturnedRoot}/Worlds/Singleplayer_{SlotId}/Config_{ModeName}.txt`（如 `Config_Normal.txt`）
- **服务器位置**：
  - V1 (遗留 JSON)：`{UnturnedRoot}/Servers/{ServerId}/Config.json`
  - V2 (新型 UnturnedDat)：`{UnturnedRoot}/Servers/{ServerId}/Config.txt`（或 `Config_{ModeName}.txt`）

#### (2) `ConfigData` 与 `ModeConfigData` 的字段层次模型
`ConfigData` 顶层包含大厅展示信息、服务器网络配置，以及三种难度模式的独立副本：
```json
{
  "Browser": { "Icon": "", "Thumbnail": "", "Desc_Hint": "", "Links": [] },
  "Server": { "VAC_Secure": true, "Max_Ping_Milliseconds": 750, "Timeout_Game_Seconds": 30.0 },
  "UnityEvents": {},
  "Easy": { /* ModeConfigData */ },
  "Normal": { /* ModeConfigData */ },
  "Hard": { /* ModeConfigData */ }
}
```

单机运行时由 `Provider.modeConfigData` 持有当前难度（`Easy` / `Normal` / `Hard`）对应的 `ModeConfigData`。
`ModeConfigData`（`PlayConfigData.cs:437-479`）内部分为 10 个子配置体系：
1. **`Items` (`ItemsConfigData`)**：`Spawn_Chance`, `Respawn_Time`, `Has_Durability`, 弹药/品质倍率等
2. **`Vehicles` (`VehiclesConfigData`)**：`Respawn_Time`, `Spawn_Chance`, `Max_Instances_*`, 装甲倍率
3. **`Zombies` (`ZombiesConfigData`)**：`Spawn_Chance`, `Loot_Chance`, 10 种特种丧尸刷新概率, 伤害/护甲系数
4. **`Animals` (`AnimalsConfigData`)**：刷新、伤害、护甲倍率
5. **`Barricades` & `Structures`**：腐烂时间 `Decay_Time`, 各武器类型防御系数
6. **`Players` (`PlayersConfigData`)**：基础生命/饥渴/病毒初始值, 代谢频率, 经验倍率, 死亡掉落规则
7. **`Objects` (`ObjectConfigData`)**：资源（树木/矿石）与碎石重置时间
8. **`Events` (`EventsConfigData`)**：天气下雨/下雪周期与时长, 空投频率 `Airdrop_Frequency_Min/Max`
9. **`Gameplay` (`GameplayConfigData`)**：命中标记、准星、弹道计算、地图纸、指南针、退出倒计时等

---

### 2.3 `Barricades.dat`、`Structures.dat`、`Vehicles.dat` 读写与数据头格式

这三类大世界数据物理位置统一由 `LevelSavedata` 定位：
`{UnturnedRoot}/Worlds/Singleplayer_{SlotId}/Level/{MapName}/{FileName}.dat`

1. **`Barricades.dat`**：版本 `19`，基于 `River` 流式，包含时间戳、NetId 计数器与 64x64 网格区域所有路障状态。
2. **`Structures.dat`**：版本 `9`，基于 `River` 流式，包含 64x64 网格区域所有建筑状态。
3. **`Vehicles.dat`**：版本 `17`，基于 `River` 流式，包含全图载具 Guid、状态、后备箱物品页等。

---

## 3. 安全与边界建议 (External Tooling & Anti-Corruption Guardrails)

### 3.1 进程排他与并发锁防护 (Process Concurrency Guard)
- **只读模式**：必须使用 `FileShare.ReadWrite` 打开文件流，严禁请求独占锁，防止造成游戏保存失败或游戏崩溃；
- **编辑模式**：在保存任何 `.dat` 或 `Config` 之前，必须检查 `Unturned.exe`、`Unturned_BE.exe` 与服务器进程。**只要检测到游戏进程处于运行状态，坚决禁用写操作**。

### 3.2 遵循官方原生备份约定与三阶段原子替换 (Atomic Write & Tilde Rollback)
- **原生规范**：Nelson 源码使用 `filePath + '~'` 作为备份。外部工具在此基础上，增加版本化时间戳备份目录（`Backups/YYYY-MM-DDTHH-mm-ssZ/`）。
- **原子替换三阶段**：
  1. **备份阶段**：原文件完整复制进带 manifest 的时间戳快照目录；
  2. **暂存写入**：新二进制完整写入同分区临时文件（`.tmp`），显式 `Flush(true)`；
  3. **原子提交**：调用 `File.Replace` 替换目标文件；异常即刻丢弃临时文件，确保原存档 0 破坏。

### 3.3 二进制流式反序列化的版本熔断 (Strict Version Gate)
- `Block` 与 `River` 为纯顺序字节流，错 1 字节全盘崩溃。
- 读取第 0 字节 `SAVEDATA_VERSION`，若 `version > KNOWN_VERSION`，**立刻中断解析并置为只读**，严禁强行修改。

### 3.4 未知 Mod GUID 穿透保留机制 (Unknown Mod Asset Pass-through)
- 遇到未安装 Mod 的缺失 GUID，**作为 `Unknown Asset` 完整原样保留**，严禁置空或删除。
