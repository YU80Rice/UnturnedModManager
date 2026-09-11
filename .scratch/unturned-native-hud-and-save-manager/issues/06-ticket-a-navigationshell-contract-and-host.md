# Ticket A: NavigationShell 契约定义与逻辑宿主状态机 (NavigationShell Contract & State Machine Host)

**What to build:** 构建当前活动窗口宿主范围内的单一导航状态机与逻辑宿主（`INavigationShellHost` 与 `NavigationShellHost`），确立 5 槽位（Play, Data, Mods, Settings, Exit）调度、次级入口（`SecondaryNavigationTarget`：TaskCenter, About）调度、单根来源锚点（`RootReturnContext`）记忆与返回恢复机制。实现强类型不可变 `NavigationCacheKey(PrimarySlot, WorkshopSubDomain?, NavigationContext?)` 及其目标槽位与上下文组合合法性校验不变量。提供纯逻辑单元测试，断言槽位切换、次级覆盖、历史栈单根回退与非法上下文拒绝。

**Input contracts:**
- `PageNavigationRequest(PrimarySlot TargetSlot, WorkshopSubDomain? SubDomain = null, NavigationContext? Context = null)`
- `INavigationShellHost`:
  - `PrimarySlot ActiveSlot { get; }`
  - `SecondaryNavigationTarget? ActiveSecondary { get; }`
  - `NavigationResult NavigateTo(PageNavigationRequest request)`
  - `bool CanNavigateBack { get; }`
  - `NavigationResult NavigateBack()`
- `NavigationCacheKey(PrimarySlot Slot, WorkshopSubDomain? SubDomain, NavigationContext? Context)`
- **组合合法性不变量**: 非法组合（例如 Play 搭配 CommunitySearchContext）直接被拒绝并返回失败 NavigationResult，不进入缓存。

**Blocked by:** None (can start immediately)

**Status:** resolved

## 范围外 (Out of Scope)
- 不修改生产 `MainWindow.xaml` 或实际 UI 布局；
- 不接入真实单机存档或角色数据；
- 不实现底层插件工坊三级页面自治路由。

## 验收标准 (Acceptance Criteria)
- [x] 定义强类型 `PrimarySlot`、`SecondaryNavigationTarget`、`PageNavigationRequest`、`NavigationResult` 与 `INavigationShellHost` 接口
- [x] 实现 `NavigationShellHost` 纯逻辑宿主，管理当前活跃主槽位与次级状态
- [x] 激活次级入口时，主槽位取消高亮，记录 `RootReturnContext`；调用 `NavigateBack()` 时准确恢复先前的槽位与子路由
- [x] 实现不可变 `NavigationCacheKey`，并在 `NavigateTo` 中执行槽位与上下文合法性组合校验，拒绝非法组合
- [x] 编写全面的契约单元测试，覆盖 5 槽位切换、次级覆盖与单根来源恢复、返回恢复与非法组合拦截，测试全部通过

## Answer

2026-09-11 完成 Ticket A 生产级交付并通过全量回归与双轴审查：

1. **导航领域模型与请求契约**：在 `UnturnedModManager.Navigation` 落地强类型不可变体系，定义 `PrimarySlot`、`SecondaryNavigationTarget`、`WorkshopSubDomain`、`NavigationResult`、`NavigationContext`（含 `PluginDetailContext` 与 `CommunitySearchContext`）、`PageNavigationRequest` 与 `ReturnContext`；
2. **最小宿主契约与纯逻辑状态机**：接口 `INavigationShellHost` 维持严格最小契约，不泄漏内部缓存与三级页面自治路由；`NavigationShellHost` 提供 5 槽位调度、次级覆盖、单根来源锚点记忆、平级横向切换不压栈、不可用锚点安全降级回退至 `PrimarySlot.Play`；
3. **不可变缓存键与组合不变量**：`NavigationCacheKey.IsValid` 严格校验组合合法性；强制上下文（`CommunitySearchContext` / `PluginDetailContext`）显式声明子域，杜绝隐式猜测；非法组合一律拒绝并不计入缓存；
4. **构造安全与封装防线**：构造函数对 `initialSlot`、`initialSecondary` 与 `initialAnchor` 进行全面合法性校验与安全规范化保底，杜绝非法状态入库；`VisitedKeys` 采用 `ImmutableHashSet` 快照输出，彻底杜绝外部篡改；
5. **测试与质量验证**：27 项新增契约单元测试 100% 绿灯，全套 189 项测试持续全绿，Release 构建 0 警告、0 错误。
