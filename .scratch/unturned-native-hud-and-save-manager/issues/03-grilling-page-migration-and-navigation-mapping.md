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
   - `[◀] 退出游戏 (Exit)`：左下角固定退出按钮（带确认交互）。
2. **次级与浮动入口设计**：
   - 任务中心：降级为顶部或右上角的浮动状态药丸/通知抽屉，有活动任务时动态呼吸闪烁；
   - 账户面板与关于信息：收拢为头像菜单或底部次级入口，不挤占主导航槽位。
3. **架构 Seam 边界规约**：
   - 确立 `NavigationShell` 的接口边界——只负责导航状态、选中态、返回行为和视觉布局；不拥有启动、下载、诊断业务逻辑。现有 Page 保持继承结构与业务行为不变，仅做最小导航注册适配。

---

## Round 1 架构裁定决策记录 (2026-09-11 PM 终审生效)

> **刚性边界准则**：本 Ticket 处于 Grilling 阶段，只冻结接口、导航语义与 Seam，**暂不修改生产代码，暂不迁移真实数据功能，暂不引入新的业务服务或依赖**。

### 1. 插件工坊聚合架构 (Q1)
- **裁定结论**：接受方案 A（加限制）。
- **职责边界**：`PluginWorkshopPage` 仅作为 Slot 3 的纯导航聚合 module，非插件业务 module。它只提供子域选择 Tab 与 adapter seam，`ModListPage` 与 `CommunityPage` 保持独立生命周期、ViewModel、过滤器、网络状态与详情导航；
- **状态与隔离契约**：
  - 优先使用明确的内容宿主（单一 ContentControl 或无独立 Journal 的轻量宿主），严禁叠加多个拥有独立 journal 的 `Frame`；
  - 本地插件与社区发现互相切换时，严禁重置对应子域的状态；
  - 从详情页返回时，必须精准返回对应子域，而不是整个模组工坊首页；
  - 社区网络请求失败严禁阻塞本地插件加载，反之亦然。

### 2. 次级导航语义与上下文保留 (Q2)
- **裁定结论**：接受方案 A，暂不重构 Drawer/Dialog，补充次级导航语义契约。
- **语义模型 (`SecondaryNavigationTarget`)**：
  - `AboutPage`：直接通过主内容宿主导航，记录进入前的主槽位，支持显式返回；点击左侧五槽位可直接离开；
  - `TaskCenterPage`：由顶栏入口打开，必须保存进入前的“主槽位 + 子页面”完整上下文；页面内返回时精准还原来源现场；
  - 任务监听生命周期由现有 ViewModel/adapter 自主维系，严禁由 Shell 越俎代庖。

### 3. 页面生命周期与分层状态保持契约 (Q3)
- **裁定结论**：不批准将 `Dictionary<Type, Page>` 裸字典冻结为终态契约；
- **分层保持契约**：
  - **视图状态**：保留搜索词、筛选项、选中 Tab、滚动条位置；
  - **已加载数据**：在生命周期允许时复用，不因主槽位切换强制清空；
  - **进行中任务**：继续由原有 ViewModel/adapter 自主管理，不因视图切换取消；
  - **临时草稿**：由页面自行声明是否保留，Shell 不擅自承诺；
  - **过期网络数据**：允许显示缓存并支持后台静默刷新；
  - **失败状态**：保留错误上下文与重试机会。
- **实现隔离**：缓存机制属于 implementation，若后续确需手工缓存，采用带上下文的 `NavigationTargetKey = 主槽位 + 子域 + 上下文标识`；不提前做“零抖动、零数据丢失”的前置断言。

### 4. 角色与存档槽位结构预览占位 (Q4)
- **裁定结论**：接受方案 A 的“结构预览占位版”，严禁暗示真实数据已接入。
- **范围红线**：
  - 严禁接入 `Life.dat`、`Inventory.dat` 等二进制解析器与读模型；
  - 严禁实现备份、恢复、导出或写入，严禁模拟假数据；
  - 仅展示 4 大只读子域形状与功能说明；
- **标准文案规约**：
  > 角色与存档中心  
  > 当前为只读结构预览。真实本地存档浏览将在后续数据引擎阶段接入；本页面暂不读取或修改存档文件。

### 5. 左下角环境与社区中枢 Facet 架构 (Q5)
- **裁定结论**：接受方案 A 的单入口 Popup，但拆分为 3 个独立 Facet/Adapter，删除未经证明的承诺。
- **Facet 拓扑**：
  - `Account facet` → 绑定 `CommunityAuthService`
  - `Environment facet` → 现有 Steam 运行探测与状态投影
  - `Appearance facet` → 绑定 `ThemeService`（外观模式切换入口，不承诺未实现的连续值昼夜滑动条）
- **承诺收敛**：删除“Steam 免密”等未经安全审计的业务承诺；
- **交互语义**：冻结 `Esc` 关闭、点击外部关闭、关闭后焦点回到触发器、与模态窗口无冲突四大交互规约。

---

## Round 2 架构裁定决策记录 (2026-09-11 PM 终审生效)

> **刚性边界准则**：本轮依然只冻结 Seam 接口与交互约束，**不修改生产代码、不新增业务 module、不接入真实存档数据、不提前实现具体动画或缓存方案**。

### 1. 插件工坊单活跃视图与状态驻留约束 (Q1)
- **裁定结论**：接受方案 A（双子视图同一内容宿主驻留，`Visibility` 切换状态）。
- **生命周期与无障碍约束**：
  - 同一时刻**仅且仅有一个子视图**可交互、可获得键盘焦点；
  - `Collapsed` 状态的视图**严禁**参与 Tab 键盘导航、自动化可访问性树（Automation Tree）或鼠标命中测试；
  - 聚合外壳严禁重置对应子域的 ViewModel 状态，本地与社区的刷新、错误和重试状态完全隔离；
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
  - **严禁清单**：Shell 严禁注册三级详情页面、严禁理解 `modId` 业务含义、严禁使用全局万能静态路由、严禁让外部事件直接穿透修改内部控件。

### 4. In-Tree Overlay 弹窗与渲染拓扑约束 (Q4)
- **裁定结论**：接受方案 A（Shell 根 Grid 内 In-Tree Overlay）。
- **交互与遮罩契约**：
  - 透明遮罩仅在打开时参与命中测试，关闭时必须从输入树中彻底注销；
  - 点击外部关闭，但严禁吞掉关闭前发出的正常业务点击；
  - `Esc` 关闭并把焦点还给 `NavHubTriggerBtn`；Popup 内部焦点循环，禁止键盘逃逸；
  - 打开模态登录等外层流程时，Overlay 必须暂时让出焦点管理。

---

## Answer: 最终冻结架构契约与 Seam 规约 (NavigationShell Specification)

经过两轮 PM 严格审查与 Grilling 盘问，Ticket 03 的所有架构 Seam、导航契约与接口定义全部冻结。**本阶段坚守边界，不修改生产代码、不接入真实存档数据、不新增业务模块**。

### 1. 核心导航接口与强类型意图模型

```csharp
namespace UnturnedModManager.Navigation;

/// <summary>
/// 五大核心主槽位枚举 (严格映射 Unturned 原版 HUD)
/// </summary>
public enum PrimarySlot
{
    Play,       // Slot 1: 游戏启动
    Data,       // Slot 2: 角色与存档 (结构预览占位)
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
    NavigationResult ReturnToSource();

    event EventHandler<NavigationStateChangedEventArgs>? StateChanged;
}
```

### 2. 插件工坊子域聚合 Seam (`IPluginWorkshopNavigation`)

```csharp
public enum NavigationChangeResult
{
    Success,
    Failed,
    AlreadyActive
}

public interface IPluginWorkshopNavigation
{
    WorkshopSubDomain CurrentSubDomain { get; }
    NavigationChangeResult NavigateTo(WorkshopSubDomain target);
}
```

- **单活跃视图约束**：同一宿主常驻两个子页面实例，通过 `Visibility` 切换。同一时刻仅一个子视图接收输入和焦点；`Collapsed` 视图严禁参与 Tab 导航、自动化可访问性树和命中测试；
- **状态与故障隔离**：本地与社区各自的 ViewModel、搜索词、过滤条件、网络状态与重试逻辑 100% 独立，互不阻塞，互不覆盖。

### 3. 次级导航与来源恢复规约

- **单根来源锚定**：
  - 玩家从主槽位首次进入次级体系时建立 `RootReturnContext`；
  - `TaskCenter ↔ About` 之间跳转不产生多层堆叠历史，始终保留最初的根来源；
  - 页面内的“◀ 返回”按键通过 `ReturnContext` 恢复原主槽位和子路由；
  - 点击左侧任意主槽位直接重定向并清空次级上下文；
  - 面包屑仅作视觉提示，不承担实际路由职责。

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
- **严格红线**：严禁接入真实二进制解析器、严禁读模型、严禁备份恢复、严禁写入能力、严禁模拟假数据；
- 真实数据能力留待步骤 5～6 独立地图立项，届时基于实际实现设计 `IDataCenter` 深度接口（满足 Deletion Test）。
- **标准文案**：
  > 角色与存档中心  
  > 当前为只读结构预览。真实本地存档浏览将在后续数据引擎阶段接入；本页面暂不读取或修改存档文件。

### 6. 左下角中枢 Facet 拓扑规约

- 单入口 `NavHubTriggerBtn` + In-Tree Overlay 浮层（处于 Shell 根 Grid 内，保持壁纸与模糊同一视觉树）；
- 内部拆分为 3 个独立 Facet/Adapter，状态投影与操作意图解耦：
  - `IAccountFacet`：登录状态投影 + 打开登录/管理流程意图（绑定 `CommunityAuthService`）；
  - `IEnvironmentFacet`：Steam 进程与运行环境状态投影；
  - `IAppearanceFacet`：外观模式投影 + 切换主题意图（绑定 `ThemeService`，仅模式切换，不承诺连续值滑动条与免密）；
- 交互规约：点击外部关闭（不吞有效业务点击）、`Esc` 关闭、焦点循环与安全恢复触发器、模态流程让出焦点；
- 严禁使用静态 `App.Services` locator，底层服务保持独立依赖注入。




