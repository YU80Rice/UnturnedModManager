# Ticket D: HomeSnapshot Coordinator 响应式投影管道与多态离线降级 (HomeSnapshot Coordinator & Source Adapters)

**What to build:** 实现只读不可变 `HomeSnapshot` 树与 `IHomeSnapshotCoordinator` 协调器。建立双代际（`RefreshGeneration` vs `PublishedRevision`）流转状态机、分批局部发布与最大超时防饿死机制。对接启动状态、环境、Profile、社区推荐与公告适配器，实现五态离线降级模型（`LiveOnline`、`CachedValid`、`CachedStale`、`Empty`、`Failed`）与独立的 `IsInitialized` 初始语义。为吉祥物提供动态安全 Insets 布局规约。

**Input contracts:**
- `HomeSnapshot` 纯只读不可变 Record 树，集合统一采用 `ImmutableArray<T>`（禁止向 UI 派发 default 数组）；
- `FeedFailureCategory` 强类型枚举（`NetworkUnavailable`, `ServerUnavailable`, `Timeout`, `DataCorrupted`）；
- `GetCacheAge(nowUtc)` 纯函数负值防御截断为 `TimeSpan.Zero`；
- Coordinator 属于当前宿主窗口生命周期作用域（非全局静态单例），支持取消与 Dispose；
- 吉祥物 Insets 动态自适应：展开时参与命中测试，关闭时设为 `Visibility.Collapsed` 并安全清零边距。

**Blocked by:** Ticket A: NavigationShell 契约与逻辑宿主

**Status:** ready-for-human

## 范围外 (Out of Scope)
- 不侵入修改真实网络 API 端点；
- 不修改生产启动主逻辑或真实 DXVK 执行流程。

## 验收标准 (Acceptance Criteria)
- [x] 定义完整的不可变 `HomeSnapshot` 树及相关只读 Record 与不可变集合
- [x] 实现 `HomeSnapshotCoordinator`，包含双代际任务校验与单调递增发布机制
- [x] 实现分批局部发布与最大延迟防饿死强制截断
- [x] 实现五态离线降级模型与独立的 `IsInitialized` 语义，无网络时安全返回本地缓存
- [x] 编写纯逻辑内存化单元测试，覆盖代际防覆盖、超时截断、降级枚举状态匹配与时间差防御，测试全部通过

## 交付产物与验证记录 (Delivery Summary)
- 产出 `Domain/Home/HomeSnapshotModels.cs`：落地不可变 Record 树、`FeedDataState` 五态模型、`FeedFailureCategory` 枚举、`GetCacheAge` 负时钟回拨防御截断、非 default 不可变集合保障。
- 产出 `Domain/Home/MascotLayoutPolicy.cs`：实现动态安全 Insets 规约、命中测试树联动与 960x600、800x500 极端小视口防御降级。
- 产出 `Domain/Home/IHomeSnapshotCoordinator.cs` 与 `Domain/Home/IHomeSourceAdapters.cs`：定义 Scoped 协调器与各源适配器输入输出契约。
- 产出 `Domain/Home/HomeSnapshotCoordinator.cs`：双代际（`RefreshGeneration` vs `PublishedRevision`）流转、两阶段分批局部发布、防抖合并与 Max Delay Deadline 防饿死强制截断。
- 产出 `Services/Home/ProductionHomeAdapters.cs`：对接现有 `GamePathService`、`GameLaunchService`、`PluginProfileService`、`LocalModService`、`BepInExService`、`DxvkService`、`DiagnosticService`、`CommunityApiClient`、`CommunityCacheService`、`LauncherUpdateService`，并在 `AppServices` 中接入工厂。
- 产出 `UnturnedModManager.Tests/HomeSnapshotCoordinatorTests.cs`：纯逻辑内存化单元测试 100% 绿灯通过；Release 配置构建 0 错误、0 警告。

---

### 复核缺陷整改记录 (Review Findings Resolution - 2026-09-11)

针对独立复核审查提出的 1 项 P1 阻断项完成专项修复：

1. **[P1] 严格传播调用者取消异常，消除静默 RanToCompletion**：
   - 在 `HomeSnapshotCoordinator.ExecuteRefreshCycleAsync` 入口处与 Phase 1 / Phase 2 执行检查点，显式加入 `callerToken.ThrowIfCancellationRequested()`；
   - 异常处理分支重构为：
     ```csharp
     catch (OperationCanceledException)
     {
         // Ticket 04 冻结契约：调用者主动传入的取消，必须严格继续传播
         if (callerToken.IsCancellationRequested)
         {
             throw;
         }

         // 仅吞掉明确属于内部代际 supersede 或 Coordinator 释放的取消
         if (_disposed || _lifetimeCts.IsCancellationRequested || Volatile.Read(ref _refreshGeneration) != generation)
         {
             return;
         }

         throw;
     }
     ```
   - 确保 `RefreshAsync(callerToken)` 被调用方取消时返回真实的 `Canceled` 任务状态，使调用方能精准区分“刷新完成”与“主动取消”；同时非内部代际原因的 OCE 不会被静默吞掉。
2. **新增取消与代际覆盖自动化测试**：
   - `RefreshAsync_WhenCallerTokenPreCanceled_ThrowsOperationCanceledException`：验证前置取消令牌立即抛出 OCE 且任务处于取消态；
   - `RefreshAsync_WhenCallerTokenCanceledDuringExecution_PropagatesOperationCanceledException`：验证执行中取消令牌如实向外传播 OCE；
   - `RefreshAsync_WhenSuperseded_InternalSupersedeDoesNotThrowToCaller`：验证内部代际覆盖（supersede）正常安全返回，不干扰调用者。
3. **测试数字口径对齐**：
   - 独立审查阶段基线测试总数为 258 项；
   - 包含 Ticket B (+6)、Ticket C (+2)、Ticket D (+11) 复核回归测试后，当前全量自动化测试实测为 **277 项**（Release 模式全绿通过，0 失败，0 跳过）。

---

### 二次复核缺陷整改记录 (Second Review Findings Resolution - 2026-09-11)

针对独立复核审查提出的 2 项 P1 阻断项完成专项整改与契约强化：

1. **[P1-1] Mascot 底部安全 Inset 提升为完整有效高度加安全边距**：
   - 重构 `Domain/Home/MascotLayoutPolicy.cs`，彻底移除无布局占用凭据的 `effectiveHeight * 0.40` 经验系数；
   - 计算公式升级为：
     ```csharp
     double effectiveHeight = measuredHeight * scaleMultiplier;
     double safeBottomInset = Math.Round((effectiveHeight + safetyPadding) * scaleRatio, 1);
     ```
   - 确保小助理可见时，底部安全区完整涵盖挂件真实测量高度加上安全边距与 DPI 缩放比，彻底杜绝内容被挂件遮挡。
   - 同步修正 `HomeSnapshotCoordinatorTests` 中对基线视口 (216.0)、高 DPI (324.0)、紧凑视口 (166.0) 以及测量驱动 (216.0 / 320.0 / 245.0) 的断言。

2. **[P1-2] `FeedSectionSnapshot<T>` 强制构造期冻结不变量**：
   - 重构 `Domain/Home/HomeSnapshotModels.cs` 中的 `FeedSectionSnapshot<T>` 构造器，杜绝软归一化导致的非法快照创建，在构造期显式检验并拒绝非法组合：
     - `Failed`：必须指定 `FailureCategory`，且 `Items` 必须为空集合（传入非空即抛出 `ArgumentException`）；
     - `LiveOnline`、`CachedValid`、`CachedStale`：`Items` 必须非空（至少包含 1 项）且必须提供 `LastSynchronized` 时间戳，严禁携带 `FailureCategory`；
     - `Empty`：`Items` 必须为空集合，严禁携带 `FailureCategory`；
     - `IsInitialized == false`：严格仅限 `State == FeedDataState.Empty`，`Items` 必须为空，不得包含时间戳或失败类别。
   - 新增 `ModelInvariants_IllegalCombinations_ExplicitlyRejected` 单元测试，全面验证 11 种非法状态组合在构造期均被立即拦截抛出 `ArgumentException`；
   - 新增 `ModelInvariants_FactoryMethods_ProduceValidInstances` 验证全套静态工厂方法产出合规快照。


