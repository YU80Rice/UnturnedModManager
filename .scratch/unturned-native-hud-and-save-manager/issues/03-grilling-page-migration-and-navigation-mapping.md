# Ticket 03: 兼容式 NavigationShell 契约与五槽位导航映射 (NavigationShell Contract & 5-Slot Mapping)

Status: ready-for-agent
Type: grilling
Blocked by: 

## 问题

针对 PM 裁定的“五槽位主导航、任务中心降级为全局状态入口、兼容式 NavigationShell 包裹现有 Page + Frame，不直接重写页面，也不把本地插件和社区插件合并成一个业务实现”：

1. **五槽位主导航目标映射规约**：
   - `[▶] 游戏启动 (Play)`：映射 `HomePage`，承载启动按钮、Profile 切换、BepInEx/DXVK 状态、诊断提示；
   - `[👥] 角色与数据 (Data)`：映射独立的数据中心占位主页，展示数据总览卡片（当前槽位、最近世界、快照概况与下阶段数据地图预告），严禁直接在此阶段实现任何写入；
   - `[🧩] 模组工坊 (Mods)`：作为导航层的聚合入口，内部由 `LocalModsAdapter` 与 `CommunityModsAdapter` 分别挂接现有本地与社区页面，保持底层业务实现互不混淆；
   - `[⚙] 启动器设置 (Settings)`：映射 `SettingsPage`（壁纸、主题、路径、文件关联、启动偏好）；
   - `[◀] 退出游戏 (Exit)`：左下角固定退出按钮（带确认交互）。
2. **次级与浮动入口设计**：
   - 任务中心：降级为顶部或右上角的浮动状态药丸/通知抽屉，有活动任务时动态呼吸闪烁；
   - 账户面板与关于信息：收拢为头像菜单或底部次级入口，不挤占主导航槽位。
3. **架构 Seam 边界规约**：
   - 确立 `NavigationShell` 的接口边界——只负责导航状态、选中态、返回行为和视觉布局；不拥有启动、下载、诊断业务逻辑。现有 Page 保持继承结构与业务行为不变，仅做最小导航注册适配。
