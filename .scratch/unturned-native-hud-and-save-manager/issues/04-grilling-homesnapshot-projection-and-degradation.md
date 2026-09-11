# Ticket 04: HomeSnapshot 响应式不可变快照投影与代际防覆盖降级模型 (HomeSnapshot Projection & Degradation Model)

Status: resolved
Type: grilling
Blocked by: 

## 问题

针对 PM 裁定的“`HomeSnapshot` 必须是首页投影模块，不得膨胀成全局 God module；采用事件驱动 + 合并刷新 + 代际校验，且离线状态必须明确区分当前网络、本地缓存、无数据、加载失败、数据过期”：

1. **多数据源适配器解耦与快照协调管道**：
   - 确立各个源适配器（`LaunchStateAdapter`、`EnvironmentStateAdapter`、`ProfileSummaryAdapter`、`CommunityContentAdapter`、`ReleaseAnnouncementAdapter`、`DiagnosticAdapter`）与 `HomeSnapshotCoordinator` 的输入输出契约；
   - 业务复杂度留在各适配器内部，`HomeSnapshot` 仅作为纯不可变只读投影，首页通过单向绑定消费。
2. **生命周期、合并刷新与代际（Revision）防覆盖机制**：
   - 外部事件（BepInEx 切换、Profile 变更、网络拉取完成）触发通知，协调器在短时间窗口内合并执行单次异步快照组装；
   - 每次快照携带唯一递增 `Revision`，迟到的网络回调若小于当前快照 `Revision` 则立刻丢弃，防止脏数据后发覆盖；
   - 页面离开时自动注销/取消后台进行中的刷新，消除无意义 CPU/内存开销。
3. **离线多状态优雅降级与吉祥物适配器**：
   - 精确区分“当前网络、本地缓存、无数据、加载失败、数据过期”，在 HUD 卡片中展示对应状态与新鲜度标记；
   - `MascotPresentationAdapter`：吉祥物作为可独立开启/关闭的挂件，关闭后首页栅格与卡片排版必须天然完整闭合，严禁产生视觉空洞。

---

## Round 1 架构裁定记录 (2026-09-11 PM 终审生效)

- **Q1 纯不可变投影**：`HomeSnapshot` 必须是纯只读投影，不得包含 Command、ViewModel、底层 DTO 或服务引用；`DiagnosticSectionSnapshot` 移除“导出诊断包意图”等命令，改为只读可用性状态 `CanExportDiagnostics`；
- **Q2 事件驱动微批次合并**：近邻事件合并为单次刷新，最终一定执行；防抖时长（如 30~50ms）、调度器并发锁属于 implementation，不写入公共契约；不提前引入 `IReadOnlyProperty`；
- **Q3 代际防覆盖与取消边界**：新刷新 supersede 旧刷新时取消旧请求；Coordinator 释放时取消全部工作；单纯离开首页不得无条件取消后台有价值刷新；区分 `RefreshGeneration`（判断异步过期）与 `PublishedRevision`（UI 已发布版本）；
- **Q4 五态离线降级**：确立 `LiveOnline`、`CachedValid`、`CachedStale`、`Empty`、`Failed`；同步时间与失败原因使用结构化字段，本地化时间文案与颜色 Token 由 View 层派生；
- **Q5 吉祥物 In-Tree Overlay**：挂件不占主栅格列，关闭自然满宽；遵守安全边距与最小窗口约束，关闭后从命中测试树与自动化树移除；复用已有偏好存储。

---

## Round 2 架构裁定记录 (2026-09-11 PM 终审生效)

- **Q1 局部故障隔离**：Adapter 将可预期的网络、超时、格式错误转化为结构化状态；`OperationCanceledException` 必须继续向上传播；未知编程错误严禁静默吞掉；Coordinator 保证单个分区失败不丢弃其他分区；
- **Q2 双代际与分批局部发布**：`RefreshGeneration` 校验异步过期，`PublishedRevision` 仅在发布新快照时递增；允许快速分区（启动/环境）先行局部发布，慢速分区（社区/公告）稍后更新，慢速分区不得阻塞主页呈现；引入最大防抖超时防饿死保证；
- **Q3 不可变集合与状态一致性**：集合必须是真正不可变容器；`State == Failed` 与 `FailureCategory != null` 严格互斥绑定；移除动态 `DateTimeOffset.UtcNow` 计算属性，保持纯值语义；
- **Q4 观察者模式与 Scoped 宿主**：Coordinator 由独立宿主持有，不强制单例；ViewModel 遵循“先订阅、再读取、按 `PublishedRevision` 单调应用”协议，彻底消除订阅竞态；离开首页只解绑 UI 渲染，不中断后台任务；
- **Q5 动态安全 Insets**：撤销固定 80px 垫片绝对保证，改为挂件可见时自适应应用安全 Insets、隐藏时归零；960×600 最小窗口与高 DPI 纳入验收矩阵，无法满足安全区时挂件自动缩放或隐藏；显隐偏好通过设置适配器解耦。

---

## Round 3 终审裁定与收敛记录 (2026-09-11 PM 终审生效)

1. **初始基线未加载语义**：在 `FeedSectionSnapshot<T>` 增加 `bool IsInitialized` 独立语义，初始基线明确标记 `IsInitialized = false`，不得伪装为 `Empty` 或 `Failed`；
2. **Coordinator 宿主作用域边界**：明确为“每个 MainWindow / NavigationShell Host 一个独立 scope”，生命周期与该窗口 Shell 一致；不同窗口使用不同 scope，不使用全局静态单例；
3. **避免投机性设置接口**：显隐配置通过现有设置/外观适配器依赖注入，在当前无第二个真实实现时不新建投机性公共接口（严格贯彻 Deletion Test）；
4. **负时间差防线**：`GetCacheAge` 遇系统时钟回拨时向下截断至 `TimeSpan.Zero`，作为展示辅助计算；
5. **不可变数组防御**：`ImmutableArray<T>` 构造时严格使用 `.Empty`，禁止向 UI 派发未初始化的 default array。

---

## Answer: 最终冻结架构契约与 Seam 规约 (HomeSnapshot Specification)

经过三轮 PM 严格审查与 Grilling 盘问，Ticket 04 的不可变投影管道、双代际防覆盖机制、五态优雅降级模型与自适应挂件布局契约全部终审冻结。**本阶段坚守边界，不修改生产代码、不接入真实存档数据、不新增未批准业务服务**。

### 1. 核心不可变数据模型体系

```csharp
namespace UnturnedModManager.Domain.Home;

/// <summary>
/// 首页纯只读不可变投影快照 (平台中立、无 Command、无 ViewModel、无底层 DTO)
/// </summary>
public sealed record HomeSnapshot(
    long PublishedRevision,
    DateTimeOffset GeneratedAt,
    LaunchSectionSnapshot Launch,
    EnvironmentSectionSnapshot Environment,
    DiagnosticSectionSnapshot Diagnostics,
    FeedSectionSnapshot<FeaturedPluginItem> Featured,
    FeedSectionSnapshot<PatchNoteItem> Announcements,
    MascotSectionSnapshot Mascot
);

public sealed record LaunchSectionSnapshot(
    string ModeTitle,
    string ModeSubtitle,
    string GamePath,
    bool CanLaunch,
    string ActiveProfileName,
    int ActiveModCount,
    bool IsConfigReady
);

public sealed record EnvironmentSectionSnapshot(
    string LoaderVersion,
    bool IsLoaderEnabled,
    bool IsGraphicsAccelerationActive,
    bool IsFallbackRendering,
    bool IsEnvironmentHealthy
);

public sealed record DiagnosticSectionSnapshot(
    string HealthSummary,
    int UnresolvedIssuesCount,
    bool CanExportDiagnostics
);

public sealed record MascotSectionSnapshot(
    bool IsVisible,
    int QuoteIndex,
    string CurrentQuote
);

public sealed record FeaturedPluginItem(
    string PluginId,
    string DisplayName,
    string ShortDescription,
    string CategoryTag,
    string Version
);

public sealed record PatchNoteItem(
    string VersionTag,
    DateTimeOffset ReleasedAt,
    System.Collections.Immutable.ImmutableArray<string> Highlights
);
```

### 2. 五态 Feed 降级模型与纯值语义契约

```csharp
public enum FeedDataState
{
    LiveOnline,   // 实时在线最新数据
    CachedValid,  // 命中本地有效缓存 (TTL 周期内)
    CachedStale,  // 本地缓存已过期，因网络受阻降级展示
    Empty,        // 远端正常连接但内容为空
    Failed        // 网络失败且无可用本地缓存
}

public enum FeedFailureCategory
{
    NetworkUnavailable, // 无网络连接 / DNS 失败
    ServerUnavailable,  // 远端服务故障 / 5xx / 维护中
    Timeout,            // 请求超时未响应
    DataCorrupted       // 数据格式错误或反序列化失败
}

/// <summary>
/// 内容投递分区快照 (使用真正不可变集合，保持纯值语义)
/// </summary>
public sealed record FeedSectionSnapshot<T>(
    FeedDataState State,
    bool IsInitialized,
    System.Collections.Immutable.ImmutableArray<T> Items,
    DateTimeOffset? LastSynchronized,
    bool CanRetry,
    FeedFailureCategory? FailureCategory = null
)
{
    /// <summary>
    /// 显式计算缓存年龄 (传入权威基准时间，遇时钟回拨自动截断至零，杜绝漂移与负值)
    /// </summary>
    public TimeSpan? GetCacheAge(DateTimeOffset baselineTime)
    {
        if (!LastSynchronized.HasValue) return null;
        var diff = baselineTime - LastSynchronized.Value;
        return diff < TimeSpan.Zero ? TimeSpan.Zero : diff;
    }
}
```

- **数据不变量**：
  - `IsInitialized == false` $\implies$ 处于初始化尚未加载阶段，不得解释为 `Empty`、`Failed`、`Cached` 或 `LiveOnline`；
  - `Items` 必须使用 `ImmutableArray<T>.Empty` 构造，禁止向 UI 派发 default 未初始化值；
  - `State == FeedDataState.Failed` $\iff$ `FailureCategory != null`；
  - `State == FeedDataState.Empty` $\implies$ `Items.IsEmpty && FailureCategory == null`；
  - `State == FeedDataState.CachedValid || State == FeedDataState.CachedStale` $\implies$ `LastSynchronized.HasValue && !Items.IsEmpty && FailureCategory == null`；
  - `State == FeedDataState.LiveOnline` $\implies$ `LastSynchronized.HasValue && !Items.IsEmpty && FailureCategory == null`；
- **表现解耦**：时间显示文本与状态颜色由 View 层基于代币与 Converter 动态派生，快照内不包含平台特定的文案与色值。

### 3. Coordinator 接口、双代际流转与防饿死机制

```csharp
public sealed class HomeSnapshotChangedEventArgs(HomeSnapshot snapshot) : EventArgs
{
    public HomeSnapshot Snapshot { get; } = snapshot;
    public long PublishedRevision => Snapshot.PublishedRevision;
}

public interface IHomeSnapshotCoordinator : IDisposable
{
    HomeSnapshot CurrentSnapshot { get; }
    event EventHandler<HomeSnapshotChangedEventArgs>? SnapshotChanged;
    void RequestRefresh();
}
```

- **双代际流转状态机**：
  - `RefreshGeneration`：内部递增标识异步任务批次，每次触发刷新自增；异步回调若与当前最新 `RefreshGeneration` 不符或关联的 `CancellationToken.IsCancellationRequested` 为真，立即丢弃；
  - `PublishedRevision`：快照发布版本号，仅在组装完成并向 UI 派发新 `HomeSnapshot` 完整帧时自增；
- **分批局部发布**：
  - 维护上一帧有效 Section 基线；初始基线具备完整的未初始化/就绪语义（严禁用 `null`）；
  - 本地快速 Section（启动、环境）完成时先行发布完整自洽帧（`PublishedRevision` 自增）；慢速网络 Section 随后以匹配代际返回时再次合并发布新帧（`PublishedRevision` 再次自增）；
- **防饿死保证**：
  - 合并刷新包含最大延迟阈值（Max Delay Deadline），连续密集事件达到上限时强制截断并执行单次快照发布，杜绝无限推迟；
  - 新刷新 supersede 旧刷新，取消旧任务并丢弃其结果。

### 4. Scoped 宿主生命周期与单调 Revision 消费协议

- **宿主作用域边界**：
  - 每个 `MainWindow` / `NavigationShell Host` 拥有一个独立的 Coordinator scope，生命周期与该窗口的 Shell 一致；不同窗口使用不同 scope，不使用全局静态单例；
  - Coordinator 的 `Dispose()` 在所属窗口关闭时触发，取消全部异步任务；
- **ViewModel 消费防竞态协议**：
  ```text
  1. 订阅事件：_coordinator.SnapshotChanged += OnSnapshotChanged;
  2. 读取快照：var initialSnapshot = _coordinator.CurrentSnapshot;
  3. 单调应用：if (initialSnapshot.PublishedRevision > _appliedRevision)
               {
                   _appliedRevision = initialSnapshot.PublishedRevision;
                   Apply(initialSnapshot);
               }
  4. 事件处理：收到 SnapshotChanged 时，若 e.PublishedRevision <= _appliedRevision 则忽略（幂等过滤）；
  5. 离开首页：仅注销 SnapshotChanged 订阅，停止 UI 重绘，后台有价值的拉取继续完成并落入 CurrentSnapshot；
  6. 切回首页：重新执行 1~3 步，零闪烁瞬时呈现最新已就绪快照。
  ```

### 5. 吉祥物动态 Insets 与可验证布局验收契约

- **拓扑挂载**：挂件位于根 Grid 顶层（In-Tree Overlay），采用 `HorizontalAlignment="Right"`, `VerticalAlignment="Bottom"`；
- **动态安全 Insets**：
  - 主视口 `ScrollViewer` 底部边距根据挂件实际测量高度动态调整安全 Inset：挂件可见时应用安全余量，挂件隐藏时安全余量精确归零；
  - 挂件打开时参与命中测试与无障碍树；挂件关闭时设置为 `Visibility.Collapsed`，彻底从命中测试树与自动化树移除；
- **偏好解耦**：显隐配置通过现有设置/外观适配器依赖注入；在当前无第二个真实实现时不新建投机性公共接口（严格贯彻 Deletion Test）；
- **布局验收防御契约**：
  - 将 960×600 最小窗口、125%/150% 高 DPI 缩放、长文本本地化、内容滚动到底部纳入自动化与人工布局验收矩阵；
  - 若在极端小视口下无法满足安全区要求，挂件实行防御性降级（缩小比例或自适应隐藏），坚决保证英雄启动卡片、状态横幅与主要交互控件 100% 零遮挡。
