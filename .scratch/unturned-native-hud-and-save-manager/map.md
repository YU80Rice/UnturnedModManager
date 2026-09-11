# Map: Unturned 原生拟真 Shell 重塑与无损页面迁移 (Wayfinder Map)

## 目的地

构建具有 Unturned 原生视觉语言（左侧 5 槽位大药丸导航栏、暗调半透明 HUD 面板、沉浸全屏背景）的可替换深层 UI Shell（`NavigationShell`）与设计代币体系，建立事件驱动合并刷新、代际校验防覆盖与多态离线降级的 `HomeSnapshot` 只读投影管道；以兼容式包裹架构完成现有 7 大业务页面的平稳迁入，确保原有启动、插件、设置、社区与诊断业务行为 100% 不回归。

## 笔记

- **核心领域**：Unturned 原生主菜单 HUD 空间布局、WPF-UI 兼容式包裹架构、NavigationShell 导航 Seam、HomeSnapshot 响应式投影管道、WCAG AA 高对比度守卫。
- **查阅技能**：`/domain-modeling`、`/codebase-design`、`/prototype`、`/grilling`、`/tdd`、`/implement`。
- **固定偏好与 PM 权威准则**：
  - **范围严格锁定**：仅涵盖实施步骤 1～4（UI Shell、BackdropHost、HomeSnapshot、页面平稳迁入）；
  - **存档解耦单独立项**：角色/世界只读与备份（步骤 5～6）另开独立地图；受控编辑（步骤 7）严格单独立项；
  - **神似而非像素复制**：借鉴 Unturned 主菜单空间结构与暗调质感，不做逐像素死板复制，保留 UMM 品牌与可访问性；
  - **兼容式包裹 Seam**：采用兼容式 `NavigationShell` 包裹现有 `Page + Frame`，不直接重写页面继承与底层业务，保持 100% 行为不回归；
  - **只读投影隔离**：`HomeSnapshot` 严格作为首页只读投影接口，禁止膨胀为全局 God module，复杂性留在各个 Source Adapter 内部；
  - **多态离线降级**：离线状态需明确区分“当前网络、本地缓存、无数据、加载失败、数据过期”，禁止无提示空白占位；
  - **质量底线**：严格保持 Release 0 警告、0 错误，全套自动化测试 100% 绿灯通过。

## 已有决策

- [01: SDK 角色与世界存档数据调用链调研](issues/01-research-u3-sdk-save-data-architecture.md) — 深入 U3-SDK 查明生存数据物理存储、Block/River 协议、Config.json 体系与 5 大安全防线，作为下一阶段数据中心地图的权威事实输入留档。
- [02: Unturned 原生风格 UI Shell 空间结构与设计代币原型](issues/02-prototype-unturned-native-ui-shell-tokens.md) — (已人工视觉核准·resolved) 2026-09-11 用户与 PM 完成最终视觉验收关单。在 `.scratch/.../prototype/` 建立独立原型沙盒，验证 5 槽位药丸导航、Page+Frame 契约、滚轮隧道路由、独立小助理展台、左下角中枢微卡片与 WCAG AAA (15.6:1) 对比度合规，产出 review.html 与 162 项全绿测试。明确为原型证据，真实生产 UI 尚未迁移。

## 当前活跃 Frontier

- [03: 兼容式 NavigationShell 契约与五槽位导航映射](issues/03-grilling-page-migration-and-navigation-mapping.md) — (已解除阻断·当前 Frontier) 围绕 NavigationShell 架构 Seam 展开盘问（Grilling），明确 5 槽位与现有 7 大业务页面的映射契约、浮动入口行为与页面无损包裹接口，确立接口边界与测试方案。

## 尚未明确

- 步骤 5～6：角色与单机世界只读数据模型（`CharacterReadModel`、`WorldReadModel`）与快照备份目录结构（移至下一张专门地图明确）；
- 步骤 7：受控安全编辑器的字段白名单与写入事务规约（严格单独立项）；
- BUE (BetterUnturnedExperience) 联动的游戏内运行时增强边界（后续阶段独立立项）。

## 范围外

- 生产级角色与世界数据只读模型与解析器（排除出本图，划入步骤 5~6 专用地图）；
- 任何角色或世界存档的写入与受控编辑操作（本阶段严禁触碰，必须另行立项）；
- 本地插件与社区插件业务实现的硬合并（仅在导航层做 5 槽位聚合，业务继续保持适配器独立）；
- 逐像素死板复制 Unturned 原版界面（必须保留 UMM 启动器品牌、无障碍与高 DPI 特性）；
- 游戏运行时内存注入或内存挂钩（维持纯净外部工具定位，运行时由独立 BepInEx/BUE 插件承担）。
