# Ticket D: HomeSnapshot Coordinator 响应式投影管道与多态离线降级 (HomeSnapshot Coordinator & Source Adapters)

**What to build:** 实现只读不可变 `HomeSnapshot` 树与 `IHomeSnapshotCoordinator` 协调器。建立双代际（`RefreshGeneration` vs `PublishedRevision`）流转状态机、分批局部发布与最大超时防饿死机制。对接启动状态、环境、Profile、社区推荐与公告适配器，实现五态离线降级模型（`LiveOnline`、`CachedValid`、`CachedStale`、`Empty`、`Failed`）与独立的 `IsInitialized` 初始语义。为吉祥物提供动态安全 Insets 布局规约。

**Input contracts:**
- `HomeSnapshot` 纯只读不可变 Record 树，集合统一采用 `ImmutableArray<T>`（禁止向 UI 派发 default 数组）；
- `FeedFailureCategory` 强类型枚举（`NetworkUnavailable`, `ServerUnavailable`, `Timeout`, `DataCorrupted`）；
- `GetCacheAge(nowUtc)` 纯函数负值防御截断为 `TimeSpan.Zero`；
- Coordinator 属于当前宿主窗口生命周期作用域（非全局静态单例），支持取消与 Dispose；
- 吉祥物 Insets 动态自适应：展开时参与命中测试，关闭时设为 `Visibility.Collapsed` 并安全清零边距。

**Blocked by:** Ticket A: NavigationShell 契约与逻辑宿主

**Status:** ready-for-agent

## 范围外 (Out of Scope)
- 不侵入修改真实网络 API 端点；
- 不修改生产启动主逻辑或真实 DXVK 执行流程。

## 验收标准 (Acceptance Criteria)
- [ ] 定义完整的不可变 `HomeSnapshot` 树及相关只读 Record 与不可变集合
- [ ] 实现 `HomeSnapshotCoordinator`，包含双代际任务校验与单调递增发布机制
- [ ] 实现分批局部发布与最大延迟防饿死强制截断
- [ ] 实现五态离线降级模型与独立的 `IsInitialized` 语义，无网络时安全返回本地缓存
- [ ] 编写纯逻辑内存化单元测试，覆盖代际防覆盖、超时截断、降级枚举状态匹配与时间差防御，测试全部通过
