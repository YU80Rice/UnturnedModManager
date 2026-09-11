# Ticket B: Legacy NavigationFacade 兼容桥与无底层 UI 泄漏请求重构 (Legacy NavigationFacade Adapter)

**What to build:** 将既有旧导航服务 `AppNavigationService.Current` 重构为面向当前活动 `INavigationShellHost` 的门面适配器（Facade Adapter）。彻底消除原先通过 `Page source` 将底层 WPF 页面控件实例暴露给导航层的设计，统一改由结构化强类型请求转发到宿主 Shell。确保调用兼容门面与直接调用宿主 Shell 行为完全一致，消除双重历史栈隐患，维持旧页面平滑调用。

**Input contracts:**
- `AppNavigationService.Current` 仅作为兼容门面，解析并调用当前宿主的 `INavigationShellHost`；
- 移除携带 `Page source` 的方法签名，改用不可变结构化请求对象；
- 单一历史栈事实源，门面自身不维护独立的页面栈。

**Blocked by:** Ticket A: NavigationShell 契约与逻辑宿主

**Status:** ready-for-human

## 范围外 (Out of Scope)
- 不修改现有页面的内部 ViewModel 业务数据逻辑；
- 不修改与页面导航无关的独立业务模块。

## 验收标准 (Acceptance Criteria)
- [x] 重构 `AppNavigationService`，内部解析并持有当前活动 `INavigationShellHost` 引用
- [x] 移除对底层 UI `Page` 类型的依赖参数，改用强类型结构化上下文
- [x] 兼容旧有页面（如从某页面跳转到设置或关于）的调用路径，由门面适配器无缝转发至宿主 Shell
- [x] 确保单一历史栈事实源，门面转发与直接宿主调用的状态完全一致
- [x] 编写适配器契约测试，验证门面桥接转发、宿主同步与异常参数防守，测试全部通过

## Answer

2026-09-11 完成 Ticket B 生产级交付并通过全量回归与双轴复审修复：

1. **结构化强类型请求与 UI 零泄漏**：
   - 在 `UnturnedModManager.Navigation` 中定义不可变记录 `CommunityDetailRequest(int ModId, WorkshopSubDomain? SourceSubDomain = null, NavigationContext? SourceContext = null)` 与 `NavigationOrigin(string BackLabel, PrimarySlot SourceSlot = PrimarySlot.Mods, WorkshopSubDomain? SourceSubDomain = null, NavigationContext? SourceContext = null)`，属性命名与 `ReturnContext` 完全对齐并保留别名兼容；
   - 彻底废除 `PageNavigationOrigin(Page Page, ...)`，并在 `AppNavigationService` 中彻底移除 `using System.Windows.Controls;` 及所有 `Page` 依赖参数。
2. **面向 INavigationShellHost 的门面适配器实现与严格单一事实源（P1 审查项修复）**：
   - `_host` 严格采用可空引用 `INavigationShellHost?`，未挂载宿主时**绝不隐式创建第二套备用导航状态机**；
   - 无活动宿主时，兼容门面所有导航操作严格返回受控失败（`NavigationResult.Rejected`），`ActiveSlot` 访问抛出明确的 `InvalidOperationException` 未挂载异常，绝不伪装成真实 Play 状态；
   - 彻底消除宿主切换竞态：所有导航操作与状态读取均在统一 `_syncLock` 同步监视器保护下原子捕获当前活动宿主并执行转发，确保并发切换宿主时请求不会遗漏发送至已解绑的旧宿主。
3. **生产环境过渡期宿主适配器与无断层运行（P1 生产挂载修复 - 方向 2）**：
   - 实现 `Navigation/LegacyNavigationViewHostAdapter.cs`，实现 `INavigationShellHost` 契约，作为过渡期生产宿主适配器；
   - 在 `MainWindow.xaml.cs` 中暴露 `CurrentPage`，并在 `OnLoaded` 中显式挂载 `_navigationHost = new LegacyNavigationViewHostAdapter(this, NavigationView)`，在窗口关闭时严格 `DetachHost` 置空解绑；
   - 保证在 Ticket E 原生 Shell 挂载前，生产环境已迁移页面的所有导航操作 100% 连通可用，杜绝无宿主空转故障。
4. **事件派生生命周期与宿主身份隔离防守（P2 审查项修复）**：
   - 修复 `AppNavigationService.OnHostStateChanged` 事件中继：在 `lock (_syncLock)` 下严格验证 `_host is not null && ReferenceEquals(sender, _host)`；
   - 彻底拦截解绑旧宿主的延迟残留事件与伪造 sender 事件，确保只有当前活动宿主发出的事件被安全中继。
5. **次级来源完整上下文保存与恢复（P1 审查项修复）**：
   - 在 `LegacyNavigationViewHostAdapter` 中完整维护 `_currentSubDomain` 与 `_currentContext`；
   - 次级激活时生成完整单根来源锚点：`ReturnContext(_activeSlot, _currentSubDomain, _currentContext)`；
   - 返回时优先恢复完整单根来源锚点（即便底层 Frame journal 不可用也能精准恢复对应子域、搜索词或模组详情上下文与返回面包屑），彻底消解来源丢失问题。
6. **Data 槽位明确受控语义杜绝假成功（P1 审查项修复）**：
   - 针对过渡期 `LegacyNavigationViewHostAdapter` 暂无数据中心承载页面的事实，导航至 `PrimarySlot.Data` 时显式返回 `NavigationResult.PendingInitialization`；
   - 保持槽位状态不变更、不触发假 `StateChanged`、视口不空转，杜绝空操作伪装 `Success`。
7. **既有页面平滑迁移适配与复用优化**：
   - 同步迁移 `ModListPage`、`CommunityPage`、`CommunityDetailPage`、`TaskCenterPage` 以及 `MainWindow` 的导航调用点至 `CommunityDetailRequest` 与 `NavigationOrigin`；
   - `MainWindow.xaml.cs` 复用 `OpenCommunityDetail(modId)` 便捷重载，消解重复构造。
8. **适配器契约测试与 0 回归门禁**：
   - 编写并扩充 `UnturnedModManager.Tests/LegacyNavigationFacadeAdapterTests.cs`，涵盖：默认无宿主置空与未挂载异常、未挂载时受控拒绝与零历史创建、宿主 Attach/Detach 生命周期与置空语义、双向单一事实源同步、结构化模组详情请求转发、防御性参数校验、来源恢复、便捷导航方法、事件重绑、宿主解绑后延迟事件隔离（`StateChanged_DelayedEventFromDetachedHost_IsNotRelayed`）、并发 Attach/Detach/Navigate 竞态一致性、Data 槽位 `PendingInitialization` 拒绝假成功、次级来源完整上下文保存，以及 Journal 不可用时精准恢复子域与搜索/详情上下文等 6 项新增测试；
   - 271 项测试（Release 模式）100% 绿灯通过；Debug 与 Release 模式全量编译通过，0 警告、0 错误。




