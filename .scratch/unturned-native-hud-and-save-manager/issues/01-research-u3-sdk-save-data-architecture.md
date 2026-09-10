# Ticket 01: SDK 角色与世界存档数据调用链调研 (Research U3-SDK Save Data Architecture)

Status: resolved
Type: research
Blocked by: 

## 问题

针对 PM 强调的“角色与世界存档结构必须以 `D:\Agent-工作目录\U3-SDK` 源码调用链为准，不能凭文件样例猜格式”，需要深入 `D:\Agent-工作目录\U3-SDK` 源码库进行权威事实调查：

1. **角色存档 (PlayerSavedata)**：
   - 角色生命健康（`Life.dat`）、技能（`Skills.dat`）、背包物品（`Inventory.dat`）、服装装扮（`Clothing.dat`）、任务状态（`Quests.dat`）在 SDK 源码中的物理路径构建规则、具体读取/反序列化方法（如 `Block` / `River` / `UnturnedDat`）与数据字段模型是什么？
   - 角色槽位（Slot 0~4）与 Steam 云存档（`Cloud/Slots/`）和单机存档（`Worlds/Singleplayer_*/Players/`）的映射关系与优先级是什么？
2. **世界存档与配置 (ServerSavedata & Config)**：
   - 地图单机难度配置（`Config.json`）在源码中的完整序列化模型（ModeConfigData 等字段体系）是什么？
   - 地图建筑（`Structures.dat`）、路障（`Barricades.dat`）、载具（`Vehicles.dat`）的文件头与格式规范是什么？
3. **输出要求**：
   - 在 `docs/research/u3-sdk-save-data-architecture.md` 输出调研报告，给出准确的 C# 源码引用文件、行号范围、数据结构及安全读取规约，为后续只读浏览器和受控编辑器提供权威事实支撑。

## Answer

深入 `D:\Agent-工作目录\U3-SDK` 源码库，完成了完整的调用链推演与事实调查，产出完整报告 [docs/research/u3-sdk-save-data-architecture.md](../../docs/research/u3-sdk-save-data-architecture.md)。核心事实结论如下：
1. **生存数据完全不走 Steam Cloud**：所有 `Life.dat`、`Inventory.dat`、`Skills.dat`、`Clothing.dat` 均由本地 `Worlds/Singleplayer_{SlotId}/Players/{SteamId}_{CharacterId}/{MapName}/Player/` 物理持有，Steam Cloud 仅同步全局卡槽外貌 `Characters.dat`。
2. **二进制序列化协议**：全部以 1 字节 `byte SAVEDATA_VERSION` 开头（Player.dat:v1, Life.dat:v3, Clothing.dat:v7 Guid, Inventory.dat:v5 7页网格, Skills.dat:v7 3大分支, Quests.dat:v11 River流式），无魔数，严禁多读或少读 1 字节。
3. **世界配置体系**：`Config.json` 由 `PlayConfigData` 统领，包含 `Items`, `Vehicles`, `Zombies`, `Animals`, `Barricades`, `Structures`, `Players`, `Objects`, `Events`, `Gameplay` 10 大子系统；单机世界同时支持新型 `Config_{ModeName}.txt`。
4. **安全防线规约**：确立“进程排他检测（游戏运行时禁止写入）”、“版本化时间戳快照目录 + 原生 `~` 备份”、“写入临时文件 + `File.Replace` 原子提交流程”、“高版本版本熔断”以及“未知 Mod GUID 原样保留”五大安全铁律。

