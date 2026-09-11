# Ticket C: PluginWorkshopPage 模组工坊纯导航聚合外壳与上下文恢复 (PluginWorkshopPage Aggregation Host)

**What to build:** 构建 `PluginWorkshopPage` 作为 Slot 3（模组工坊）的纯导航聚合宿主。在单一容器内挂载现有的两个独立子页面（`ModListPage` 本地插件 与 `CommunityPage` 社区发现），通过 `Visibility`（`Visible` vs `Collapsed`）切换保持单活跃视图，隔离焦点命中与辅助功能树。为社区模组详情提供自治适配器，并在子域切换或返回时精准恢复搜索词、筛选选项与滚动位置。

**Input contracts:**
- `PluginWorkshopPage` 仅负责导航聚合、子域选择切换、标题计数与上下文恢复；
- `ModListPage` 与 `CommunityPage` 及其现有 ViewModel 保持各自独立托管状态与数据源，严禁引入全局聚合数据服务；
- 社区插件详情请求由 Slot 3 自治适配器消化与渲染，不暴露给顶级宿主 Shell。

**Blocked by:** Ticket A: NavigationShell 契约与逻辑宿主

**Status:** ready-for-human

## 范围外 (Out of Scope)
- 严禁引入未经批准的“全局聚合数据服务”；
- 不重构 `ModListPage` 或 `CommunityPage` 内部已有的网络请求与模组管理业务逻辑。

## 验收标准 (Acceptance Criteria)
- [x] 实现 `PluginWorkshopPage`，具备本地插件与社区发现双子域切换导航栏
- [x] 单一容器内挂载 `ModListPage` 与 `CommunityPage`，以 `Visibility` 控制活跃态，未激活视图移出焦点树与命中测试
- [x] 两个子域的刷新、错误展示与过滤状态互不干扰、各自隔离
- [x] Slot 3 自治消化社区插件详情展示与返回，精准恢复原列表筛选与滚动上下文
- [x] 编写单元测试验证子域切换状态隔离、上下文暂存恢复与单活跃视图契约，测试全部通过

## Answer

2026-09-11 完成 Ticket C 生产级交付并通过全量自动化验证与回归防线：

1. **工坊聚合契约与纯逻辑状态机**：
   - 在 `UnturnedModManager.Navigation` 中定义 `IPluginWorkshopNavigation`、`NavigationChangeResult` 与 `WorkshopActiveView`；
   - 实现纯逻辑协调器 `PluginWorkshopCoordinator`，管理 `CurrentSubDomain`、`ActiveView`、`IsDetailActive` 与详情模组 ID；
   - 支持幂等导航（`AlreadyActive`）、非法目标拦截（`InvalidTarget`）、携带详情上下文自治打开与搜索词暂存。
2. **单一容器挂载与单活跃视图物理隔离**：
   - 生产级 `Pages/PluginWorkshopPage.xaml` 与 `.xaml.cs`，在单一容器 `WorkshopContentHost` 内常驻挂载 `ModListPage`（`LocalHost`）、`CommunityPage`（`CommunityHost`）与 `CommunityDetailPage`（`DetailHost`）；
   - 基于 `Visibility.Visible` 与 `Visibility.Collapsed` 互斥控制，未激活子视图彻底移出 WPF 命中测试、Tab 焦点导航树与辅助功能自动化树。
3. **数据与故障隔离（零全局聚合服务）**：
   - `ModListPage` 与 `CommunityPage` 及其 ViewModel 继续各自独立托管网络状态、过滤与错误展示，零侵入无耦合；
   - 暴露只读 `ViewModel` 属性以供聚合外壳安全查询与恢复，严禁引入全局聚合数据服务。
4. **社区详情自治消化与交互上下文精准恢复**：
   - `ModListPage` 与 `CommunityPage` 的模组详情点击事件由 Slot 3 自治适配器直接消化并呈现，不向顶级宿主 Shell 泄漏业务标识；
   - 在进入详情或跨子域切换前，通过 `WorkshopContextSnapshot` 精准捕获垂直滚动条位置、搜索词、分类、排序与配置方案；
   - `CommunityDetailPage` 增加 `CustomBackAction` 支持自治返回，退出详情时无缝恢复源子域与暂存交互上下文。
5. **滚轮隧道路由与透明壁纸兼容**：
   - 接入 `ScrollWheelRouter.RouteToNearestScrollViewer` 与 `Background="Transparent"`，无缝支持毛玻璃壁纸与外层统一滚动。
6. **自动化测试与质量门禁**：
   - 编写 `UnturnedModManager.Tests/PluginWorkshopCoordinatorTests.cs`（23 项纯逻辑契约测试）与 `PluginWorkshopPageTests.cs`（9 项 STA UI 契约测试）；
   - 同步通过 `PageScrollWheelBehaviorTests` 与 `WallpaperHostTests` 静态断言；
   - 全量测试 273 项 100% 绿灯通过；Release 模式全量编译 0 警告、0 错误。

---

### 复核缺陷整改记录 (Review Findings Resolution - 2026-09-11)

针对独立复核审查提出的 2 项 P1 与 1 项 P2 缺陷，完成专项重构与强化验证：

1. **[P1] 消除多 Frame Journal 结构越界，确立单一容器纯净宿主**：
   - 重构 `Pages/PluginWorkshopPage.xaml`，彻底移除 3 个带 `JournalOwnership="OwnsJournal"` 的 `Frame`，统一改用纯净 `ContentControl`（`LocalHost`、`CommunityHost`、`DetailHost`）；
   - 杜绝多层嵌套 WPF 导航历史栈与生命周期副作用，完全符合单一内容宿主与 `Visibility` 互斥控制设计规范。
2. **[P1] 消除事件重复订阅与双重导航冲突，实现单一所有权就地接管**：
   - 在 `ModListPage` 与 `CommunityPage` 中增加 `CustomOpenDetailAction` 委托插槽；
   - 子页面在触发打开详情事件时，优先由聚合宿主注入的 `CustomOpenDetailAction` 就地消费；
   - `PluginWorkshopPage` 彻底移除对 `ViewModel` 事件的外部重复订阅与多余处理函数，杜绝点击一次同时触发 facade 与聚合详情的双重导航。
3. **[P2] 补全交互上下文保全与精准恢复（分类、排序与本地 Profile ID）**：
   - 扩充 `WorkshopContextSnapshot` 与 `PluginWorkshopCoordinator`，支持捕获与还原 `SelectedProfileId`；
   - 在进入详情或切换子域时，不仅保留搜索词与滚动位移，更完整记录并精准还原 `SelectedCategory`、`Sort` 及 `SelectedProfile`；
   - 滚动位移还原采用“即时设定 + `DispatcherPriority.Loaded` 异步二阶段对齐”，保证虚拟化列表与视图切换后滚动位置不丢失。
4. **验证结论**：
   - 运行全量 273 项测试 100% 绿灯；
   - Release 构建 0 错误、0 警告。


