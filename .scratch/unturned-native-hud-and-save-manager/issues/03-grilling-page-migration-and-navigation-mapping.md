# Ticket 03: 兼容式 NavigationShell 契约与五槽位导航映射 (NavigationShell Contract & 5-Slot Mapping)

Status: resolved
Type: grilling
Blocked by: 

## 问题

针对 PM 裁定的“五槽位主导航、任务中心降级为全局状态入口、兼容式 NavigationShell 包裹现有 Page + Frame，不直接重写页面，也不把本地插件和社区插件合并成一个业务实现”：

1. **五槽位主导航目标映射规约**：
   - `[▶] 游戏启动 (Play)`：映射 `HomePage`，承载启动按钮、Profile 切换、BepInEx/DXVK 状态、诊断提示；
   - `[👥] 角色与数据 (Data)`：映射独立的数据中心占位主页，展示数据总览卡片（当前槽位、最近世界、快照概况与下阶段数据地图预告），严禁直接在此阶段实现任何写入；
   - `[🧩] 模组工坊 (Mods)`：作为导航层的聚合入口，内部由 `LocalModsAdapter` 与 `CommunityModsAdapter` 分别挂接现有本地与社区页面，保持底层业务实现互不混淆；
   - `[⚙] 启动器设置 (Settings)`：映射 `SettingsPage`（壁纸、主题、路径、文件关联、启动偏好）；
   - `[🚪] 退出程序 (Exit)`：执行优雅退出流程。
2. **任务中心与关于降级为次级状态与快捷入口**：
   - 取消它们作为左侧主槽位的一级地位，收敛为统一的全局浮动入口/药丸徽标；
   - 点击时以主视口呈现，左侧主槽位保持不失焦或明确指示当前处于次级覆盖状态；
   - 提供明确的“返回”语义恢复先前的页面。
3. **兼容式包裹 NavigationShell 的边界契约与无损迁入**：
   - 确立 `INavigationShell` 最小暴露接口，既满足统一的路由分发，又严禁侵入现有 7 大页面的内部状态；
   - 制定跨槽位切换时的页面状态保持（如搜索输入、滚动位置、已加载数据）规范，杜绝页面重建抖动与行为回归；
   - 明确测试策略：设计契约测试，验证 5 大槽位导航状态机与历史记录堆栈。

---

## Round 1 架构裁定决策记录 (2026-09-11 PM 终审生效)

> **核心原则**：Ticket 03 仅冻结架构接口、导航语义与 Seam 契约，**严禁修改生产代码、严禁直接接入真实存档数据、严禁提前实现具体动画与缓存方案**。

### 1. 模组工坊聚合架构 (Q1)
- **裁定结论**：接受方案 A（聚合外壳）。
- **职责边界**：
  - `PluginWorkshopPage` 仅作为纯导航聚合外壳，负责当前子域选择、标题、计数和返回语义；
  - `ModListPage` 与 `CommunityPage` 保持为两个独立 module，ViewModel、过滤器、网络状态与详情导航互不合并；
  - 采用单一宿主内容区域，挂载两个子视图实例，通过 `Visibility` 切换保持单活跃视图；未激活视图从可访问性与输入树中隐藏。

### 2. 次级导航与来源恢复机制 (Q2)
- **裁定结论**：接受方案 A（状态机集中派生）。
- **交互与派生**：
  - 任务中心与关于使用主视口全量呈现，左侧五槽位取消高亮，由 `NavigationShell` 统一派生；
  - 引入单根来源锚点（`RootReturnContext`），记录进入前的主槽位与子路由；
  - 返回时一键恢复原主槽位，点击左侧主槽位直接重定向并清空次级上下文；
  - `TaskCenter ↔ About` 之间平级切换不连续压栈。

### 3. NavigationShell 最小公开接口与强类型意图 (Q3)
- **裁定结论**：接受方案 A（分级转交模型，拒绝弱类型字典与动态对象）。
- **层级转交**：
  - Shell 仅负责激活顶级 `PrimarySlot`，并将 `NavigationIntent`（子路由与强类型 `NavigationContext`）移交给对应槽位 Adapter；
  - 槽位内部（如插件详情）由各页面自治管理，Shell 严禁注册三级页面、严禁理解业务标识；
  - 跨槽位切换保持视图状态（搜索词、筛选项、滚动位置）与进行中任务。

### 4. 角色与存档只读结构预览定位 (Q4)
- **裁定结论**：接受方案 A（只读结构预览，遵循 Deletion Test）。
- **边界底线**：
  - Slot 2 正常可进，展示 4 大子域结构框架；
  - 严禁实现备份、恢复、导出或写入，严禁模拟假数据；
  - 仅展示 4 大只读子域形状与功能说明；
- **标准文案规约**：
  > 角色与存档中心  
  > 当前为只读结构预览。真实本地存档浏览将在后续数据引擎阶段接入；本页面暂不读取或修改存档文件。

### 5. 左下角环境与社区中枢 Facet 架构 (Q5)
- **裁定结论**：接受方案 A 的单入口 Popup，但拆分为 3 个独立 Facet/Adapter，删除未经证明的承诺。
- **Facet 拓扑**：
  - `Account facet` → 绑定 `CommunityAuthService`
  - `Environment facet` → 现有运行环境探测与状态投影
  - `Appearance facet` → 绑定 `ThemeService`（外观模式切换入口，不承诺未实现的连续值调节）
- **承诺收敛**：删除未经审计的业务承诺；
- **交互语义**：冻结 `Esc` 关闭、外部点击由遮罩消费、关闭后焦点回到触发器、与模态窗口无冲突四大交互规约。

---

## Round 2 架构裁定决策记录 (2026-09-11 PM 终审生效)

> **刚性边界准则**：本轮依然只冻结 Seam 接口与交互约束，**不修改生产代码、不新增业务 module、不接入真实存档数据、不提前实现具体动画或缓存方案**。

### 1. 插件工坊单活跃视图与故障隔离 (Q1)
- **裁定结论**：接受方案 A。
- **并发与可见性规约**：
  - 两个视图实例（本地插件 vs 社区发现）常驻同一容器，采用 `Visibility.Visible` 与 `Visibility.Collapsed` 切换；
  - 同一时刻仅有一个子视图处于活跃交互态；`Collapsed` 视图严禁参与 Tab 导航、自动化可访问性树和焦点命中；
  - 两个子域的刷新、错误和重试状态完全隔离；
  - 插件详情页返回必须精准恢复原子域及其上下文。

### 2. 次级页面高亮派生与结构化来源恢复 (Q2)
- **裁定结论**：接受方案 A。
- **状态机派生语义**：
  - 高亮状态必须由 `NavigationShell` 统一的导航状态机集中派生（`PrimaryTarget = none`，`SecondaryTarget = TaskCenter | About`），严禁页面自行反向修改 UI；
  - 面包屑仅作为视觉文案，实际返回必须由结构化的 `ReturnContext` 承载；
  - 点击左侧任意主槽位可直接离开次级页面，不必强制先执行返回。

### 3. 分级导航 Seam 契约与责任边界 (Q3)
- **裁定结论**：接受方案 A（分级意图转交模型）。
- **职责划分**：
  - `NavigationShell` 仅负责校验并激活主槽位，再将 `NavigationIntent`（子路由与上下文）原样转交给对应 slot adapter；
  - `PluginWorkshopPage` 或其 adapter 负责切换对应子域并解析详情上下文；
  - **严禁清单**：Shell 严禁注册三级详情页面、严禁理解业务标识、严禁使用全局万能静态路由、严禁让外部事件直接穿透修改内部控件。

### 4. In-Tree Overlay 弹窗与渲染拓扑约束 (Q4)
- **裁定结论**：接受方案 A（Shell 根 Grid 内 In-Tree Overlay）。
- **交互与遮罩契约**：
  - 透明遮罩仅在打开时参与命中测试，关闭时必须从输入树中彻底注销；
  - 外部点击关闭，并由遮罩消费该次点击，不穿透到底层主视口；
  - `Esc` 关闭并把焦点还给 `NavHubTriggerBtn`；Popup 内部焦点循环，禁止键盘逃逸；
  - 打开模态登录等外层流程时，Overlay 必须暂时让出焦点管理。

---

## Answer: 最终冻结架构契约与 Seam 规约 (NavigationShell Specification)

经过三轮 PM 严格审查与 Grilling 盘问，Ticket 03 的所有架构 Seam、导航契约与接口定义全部终审冻结。**本阶段坚守边界，不修改生产代码、不接入真实存档数据、不新增业务模块**。

### 1. 核心导航接口与强类型意图模型

```csharp
namespace UnturnedModManager.Navigation;

/// <summary>
/// 五大核心主槽位枚举 (严格映射 Unturned 原版 HUD)
/// </summary>
public enum PrimarySlot
{
    Play,       // Slot 1: 游戏启动
    Data,       // Slot 2: 角色与存档 (只读结构预览占位)
    Mods,       // Slot 3: 插件工坊 (聚合入口)
    Settings,   // Slot 4: 启动器设置
    Exit        // Slot 5: 退出程序
}

/// <summary>
/// 顶栏次级导航目标
/// </summary>
public enum SecondaryNavigationTarget
{
    TaskCenter, // 任务中心
    About       // 关于 UMM
}

/// <summary>
/// 模组工坊双子域枚举
/// </summary>
public enum WorkshopSubDomain
{
    LocalInstalled,     // 本地插件管理
    CommunityDiscovery  // 社区在线发现
}

/// <summary>
/// 角色与存档只读数据域枚举
/// </summary>
public enum DataSubDomain
{
    Characters,
    Worlds,
    ServerConfig,
    Backups
}

/// <summary>
/// 强类型导航上下文抽象基类 (彻底杜绝弱类型字典与字符串路由)
/// </summary>
public abstract record NavigationContext;

/// <summary>
/// 插件详情上下文
/// </summary>
public sealed record PluginDetailContext(string ModId) : NavigationContext;

/// <summary>
/// 社区搜索上下文
/// </summary>
public sealed record CommunitySearchContext(string Query) : NavigationContext;

/// <summary>
/// 强类型导航意图 (只读不可变)
/// </summary>
public sealed record NavigationIntent(
    PrimarySlot PrimarySlot,
    WorkshopSubDomain? SubDomain = null,
    NavigationContext? Context = null
);

/// <summary>
/// 单根来源返回上下文
/// </summary>
public sealed record ReturnContext(
    PrimarySlot SourceSlot,
    WorkshopSubDomain? SourceSubDomain = null,
    NavigationContext? SourceContext = null
);

/// <summary>
/// 次级导航会话状态 (单一次级目标 + 单根来源快照)
/// </summary>
public sealed record SecondaryNavigationState(
    SecondaryNavigationTarget ActiveTarget,
    ReturnContext? RootReturnContext
);

/// <summary>
/// 导航执行结果指示
/// </summary>
public enum NavigationResult
{
    Success,
    Rejected,
    InvalidTarget,
    PendingInitialization
}

/// <summary>
/// 状态变更事件参数
/// </summary>
public sealed class NavigationStateChangedEventArgs(
    PrimarySlot? primarySlot,
    SecondaryNavigationTarget? secondaryTarget,
    ReturnContext? returnContext) : EventArgs
{
    public PrimarySlot? PrimarySlot { get; } = primarySlot;
    public SecondaryNavigationTarget? SecondaryTarget { get; } = secondaryTarget;
    public ReturnContext? ReturnContext { get; } = returnContext;
}

/// <summary>
/// NavigationShell 最小公开 Seam 契约
/// </summary>
public interface INavigationShell
{
    PrimarySlot CurrentSlot { get; }
    SecondaryNavigationTarget? ActiveSecondary { get; }
    ReturnContext? CurrentReturnContext { get; }

    NavigationResult Navigate(NavigationIntent intent);
    NavigationResult NavigateToSecondary(SecondaryNavigationTarget secondaryTarget);
    NavigationResult ReturnToSource();

    event EventHandler<NavigationStateChangedEventArgs>? StateChanged;
}
```

### 2. 插件工坊子域聚合 Seam (`IPluginWorkshopNavigation`)

```csharp
public enum NavigationChangeResult
{
    Changed,
    AlreadyActive,
    InvalidTarget,
    Rejected,
    Unavailable
}

public interface IPluginWorkshopNavigation
{
    WorkshopSubDomain CurrentSubDomain { get; }
    NavigationChangeResult NavigateTo(WorkshopSubDomain target);
}
```

- **单活跃视图约束**：同一宿主常驻两个子页面实例，通过 `Visibility` 切换。同一时刻仅一个子视图可交互与接收焦点；`Collapsed` 视图严禁参与 Tab 导航、自动化可访问性树和命中测试；
- **幂等性与失败不变**：重复导航至当前激活子域必须是幂等的（返回 `AlreadyActive`）；子域切换失败时，当前已激活子域保持不变；
- **职责隔离**：聚合页只管理当前子域选择、内容挂载、返回上下文与故障隔离，不拥有搜索、下载、安装、删除等具体业务逻辑；
- **状态与故障隔离**：本地与社区各自的 ViewModel、搜索词、过滤条件、网络状态与重试逻辑 100% 独立，互不阻塞，互不覆盖。

### 3. 次级导航与来源恢复规约

- **单根来源锚定快照**：
  - 用户从主槽位首次进入次级体系时建立 `RootReturnContext` 快照；
  - 若从应用初始状态直接打开任务中心（无来源页面），`RootReturnContext` 允许为 `null`；
  - 再次点击当前已激活的次级页面项保持幂等，不重建来源锚点；
  - `TaskCenter ↔ About` 之间横向切换不压栈、不改写 `RootReturnContext`；
  - 来源页面已失效或不可用时，返回操作应安全回退至默认主槽位（`PrimarySlot.Play`），严禁抛出异常或陷入空白页；
  - 点击左侧任意主槽位直接重定向并彻底清空次级状态；
  - 面包屑仅作为只读层次提示，不承担实际路由职责。

### 4. 分层状态保持规约

| 状态类别 | 跨槽位切换处理契约 |
|---|---|
| **视图状态** | 搜索词、筛选项、子 Tab 激活项、滚动条位置无条件保留 |
| **已加载数据** | 在生命周期允许时复用，不因主槽位切换强制清空 |
| **进行中任务** | 由各 ViewModel 自主管理，不因视图切换取消 |
| **临时草稿** | 遵循页面自身声明的生命周期，Shell 不擅自承诺 |
| **过期网络数据** | 允许展示已有缓存，支持后台静默刷新 |
| **失败上下文** | 保持错误卡片与重试入口，不静默清空 |

### 5. 角色与存档占位规约

- Slot 2 作为只读结构预览入口，正常响应切换，呈现 4 大子域视觉框架；
- **可观察状态契约**：
  - `CurrentSubDomain`: 当前选中的数据子域（Characters / Worlds / ServerConfig / Backups）；
  - `IsReadOnly = true`: 明确标记只读；
  - `IsDataEngineConnected = false`: 页面明确标记底层数据引擎尚未接入；
  - `Notice`: 规范说明文本；
- **严格红线**：
  - 页面不提供保存、写入、删除、恢复、导出命令；
  - 不读取真实存档文件；
  - 不使用模拟假数据伪装成玩家真实数据；
  - 四个子域只表达未来结构，不声称已经具备数据能力；
  - 暂不创建 `IDataCenterPlaceholderSeam`，遵循 Deletion Test，真实接口延迟到步骤 5～6 独立立项。
- **标准文案**：
  > 角色与存档中心  
  > 当前为只读结构预览。真实本地存档浏览将在后续数据引擎阶段接入；本页面暂不读取或修改存档文件。

### 6. 左下角中枢 Facet 拓扑规约

- 单入口 `NavHubTriggerBtn` + In-Tree Overlay 浮层（处于 Shell 根 Grid 内，保持壁纸与模糊同一视觉树）；
- **三大独立 Facet/Adapter 依赖注入**：
  - `IAccountFacet`：登录状态投影 + 打开登录/管理流程意图（绑定 `CommunityAuthService`）；
  - `IEnvironmentFacet`：运行环境状态探测与投影；
  - `IAppearanceFacet`：外观模式投影 + 切换主题意图（绑定 `ThemeService`，仅模式切换，不承诺连续值滑动条与免密）；
  - 三者由独立服务注入，严禁使用静态 `App.Services` locator；Facet 只暴露状态投影与受控意图，不暴露 WPF 控件或窗口对象；
- **交互与遮罩命中测试契约**：
  - **外部点击关闭且遮罩消费**：点击 Popup 外部区域时关闭 Popup；**该次点击默认由遮罩消费，不继续触发底层主视口业务命令**（防止误点启动、删除或导航）；用户再次点击主视口时执行正常业务操作；
  - **键盘焦点闭环**：Popup 打开期间，键盘焦点限制在 Popup 内，禁止 Tab 逃逸；
  - **Esc 与焦点安全归还**：按 `Esc` 键关闭 Popup；关闭后焦点精确安全归还给触发器 `NavHubTriggerBtn`；
  - **模态流程交接**：打开模态登录等外层流程时，Popup 先关闭或让出焦点控制权；
  - **遮罩命中生命周期**：透明遮罩仅在 Popup 打开时参与命中测试，关闭后必须从输入树中彻底注销。
