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

**Status:** ready-for-agent

## 范围外 (Out of Scope)
- 不修改生产 `MainWindow.xaml` 或实际 UI 布局；
- 不接入真实单机存档或角色数据；
- 不实现底层插件工坊三级页面自治路由。

## 验收标准 (Acceptance Criteria)
- [ ] 定义强类型 `PrimarySlot`、`SecondaryNavigationTarget`、`PageNavigationRequest`、`NavigationResult` 与 `INavigationShellHost` 接口
- [ ] 实现 `NavigationShellHost` 纯逻辑宿主，管理当前活跃主槽位与次级状态
- [ ] 激活次级入口时，主槽位取消高亮，记录 `RootReturnContext`；调用 `NavigateBack()` 时准确恢复先前的槽位与子路由
- [ ] 实现不可变 `NavigationCacheKey`，并在 `NavigateTo` 中执行槽位与上下文合法性组合校验，拒绝非法组合
- [ ] 编写全面的契约单元测试，覆盖 5 槽位切换、次级入栈出栈、返回恢复与非法组合拦截，测试全部通过
