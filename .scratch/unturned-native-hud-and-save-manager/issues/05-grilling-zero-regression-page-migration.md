# Ticket 05: 现有 7 大业务页面平稳迁入与 0 回归验证方案 (Zero-Regression Page Migration & Verification Specification)

Status: resolved
Type: grilling
Blocked by: 

## 问题

针对 PM 裁定的本张地图正式 DoD：“完成 Unturned 风格 Shell、BackdropHost、HomeSnapshot 和现有页面平稳迁入，原有启动、插件、设置、社区功能行为不回归，任务中心、账户、关于次级入口可用，数据失败时仍可降级显示”：

1. **页面迁入实施步骤与契约测试**：
   - 确立 7 个现有页面挂载入 `NavigationShell` 的平滑切换路径；
   - 编写针对 `NavigationShell` 的宿主契约测试，断言 5 槽位点击、次级入口呼出、后退栈导航与默认展示行为。
2. **全链路 0 回归防线（Zero-Regression Guardrails）**：
   - 验证现有全套 162 项自动化测试（包括 BepInEx 环境、DXVK 探测、模组启停、社区安装、任务中心、主题与壁纸模糊联动、滚轮路由）在原生 Shell 下持续 100% 绿灯，0 失败、0 新增代码警告、0 错误；
   - 编写针对 `HomeSnapshot` 离线缓存、超时降级与代际防覆盖的新增单元测试。
3. **人工视觉验收与异常降级核验清单**：
   - 建立高覆盖度视觉验收标准（含 13 套调色板适配、WCAG AA 对比度复查、网络断开离线展示、初次引导体验）。

---

## Round 1 & Round 2 架构裁定决策记录 (2026-09-11 PM 终审生效)

> **核心原则**：Ticket 05 仅冻结页面迁入契约、状态保持规约与质量准入标准，**严禁提前修改生产代码、严禁直接实现 NavigationShell、严禁改动业务 ViewModel 逻辑**。

### 1. 7 个现有页面的兼容挂载与单一宿主容器 (Q1)
- **容器与挂载拓扑**：
  - `NavigationShell` 设立单一主内容宿主（Single Content Host）；
  - 4 个一级主槽位（`HomePage`、`DataCenterPlaceholderPage`、`PluginWorkshopPage`、`SettingsPage`）与 2 个次级入口（`TaskCenterPage`、`AboutPage`）统一载入该单一主宿主容器；
  - `CommunityDetailPage` 属于 Slot 3 的自治详情视图，由其对应适配器按需承载与展示；
  - 现有 Page 的继承关系、DataContext 绑定体系与对应 ViewModel 完全保持原样，不作侵入式修改。
- **历史栈规约**：
  - 杜绝双重历史栈，`INavigationShell` 是导航历史管理的唯一事实源；
  - 次级页面返回由 `RootReturnContext` 承载，返回后恢复原主槽位与子域。

### 2. 交互状态保全与职责归属分离 (Q2)
- **保全目标与范围**：
  - 放弃“全量页面强行常驻系统资源”的硬性承诺，聚焦于保全用户高价值交互上下文：搜索关键字、筛选选项、列表滚动偏移位置以及后台正在运行的异步作业；
- **业务状态所有权解耦**：
  - 插件与工坊状态继续由现有 `ModListPage`、`CommunityPage` 及其各自的 ViewModel / Adapter 独立托管，本工单绝不新建或引入任何全局聚合数据服务；
  - `PluginWorkshopPage` 仅负责导航聚合、子视图容器挂载和上下文恢复；
  - 后台任务流继续由现有任务调度模块托管。

### 3. 分阶段演进里程碑与 Git 检查点 (Q3)
- **有序演进五大里程碑（Milestones）**：
  - **里程碑 1**：导航适配器公开契约定义与基础契约测试构建；
  - **里程碑 2**：插件工坊与次级目标页面适配挂载；
  - **里程碑 3**：首页响应式快照（`HomeSnapshot`）数据流对接；
  - **里程碑 4**：主窗口外壳（`MainWindow`）装配与综合联动；
  - **里程碑 5**：全量自动化验收通过与历史通路平稳退役。
- **发布与演进防线**：
  - 依靠 Git 明确的提交检查点与发布标签（如在主外壳切换前打上 `pre-native-shell-switch` 标记）提供确定的版本演进防线，不长期维护并行的双运行时体系；
  - 最终可执行文件严格遵循发布规范，主程序文件名称永久保持 `UnturnedModManager.exe`。

### 4. 自动化测试矩阵与质量准入度量 (Q4)
- **不可削弱的既有基线**：
  - 现存全部 **162 项**业务自动化测试保持 100% 绿灯（0 失败）；
  - 新增针对 `NavigationShell` 槽位切换、次级入口调度、状态保全及快照投影的契约测试；
- **度量口径标准**：
  - **0 编译错误，0 新增代码警告**；
  - 底层 SDK 环境既有的构建提示（如 `NETSDK1057` 预览版提示）作为客观环境事实单独记录，不混入业务代码质量度量；
  - 严禁通过标记跳过（Skip）来回避用例验证；外部依赖测试若因环境无法运行，必须显式声明前置运行条件并报告为未执行，不得虚报为通过。

### 5. 无障碍、安全边距与视口验收准则 (Q5)
- **浮层交互安全**：
  - 遵循 Ticket 03 既定规范，弹出浮层（Popup）外部区域的点击事件由半透明遮罩直接消费并关闭浮层，不触发任何底层背景控件的操作，杜绝误触；
- **无障碍与视觉可用性**：
  - 支持系统级减弱动态效果设置（`prefers-reduced-motion` 友好降级）；
  - 全键盘导航提供高对比度、清晰的焦点指示框（Focus Indicator）；
  - 视口自适应支持最小 960×600 紧凑分辨率及各级系统高 DPI 缩放，保持界面元素安全边距完整，文字与重要信息无裁切。

---

## Round 3 架构终审结论 (2026-09-11 PM 终审冻结)

### 1. 宿主作用域单一事实源与去 UI 泄漏请求对象

```csharp
public sealed record CommunityDetailRequest(
    int ModId,
    NavigationContext? SourceContext = null);

public sealed record PageNavigationRequest(
    PrimarySlot TargetSlot,
    WorkshopSubDomain? SubDomain = null,
    NavigationContext? Context = null);

public interface INavigationShellHost
{
    PrimarySlot ActiveSlot { get; }
    SecondaryTarget? ActiveSecondary { get; }
    void NavigateTo(PageNavigationRequest request);
    void OpenCommunityDetail(CommunityDetailRequest request);
    bool CanNavigateBack { get; }
    void NavigateBack();
}
```

- **宿主作用域限定**：
  - `INavigationShell` 明确界定为**当前活动 `NavigationShell` 宿主范围内唯一的导航状态机与事实源**，生命周期与当前主窗口一致，不作为跨窗口全局单例；
  - `AppNavigationService.Current` 仅作为对外兼容的门面适配器（Facade Adapter），内部统一解析并转发至当前活动宿主 Shell，彻底杜绝双重历史栈；
- **UI 引用彻底消除**：
  - 门面方法彻底移除 `Page source` 这类将底层 WPF 页面类型泄漏给上层的参数，全面采用强类型的不可变结构化请求对象（`CommunityDetailRequest`、`PageNavigationRequest`）。

### 2. 强类型不可变 NavigationCacheKey 与状态托管规范

```csharp
public sealed record NavigationCacheKey(
    PrimarySlot Slot,
    WorkshopSubDomain? SubDomain,
    NavigationContext? Context);
```

- **强类型键契约**：
  - 严格废除 `string? SubRoute` 与 `string? ContextIdentity` 裸字符串定义；
  - 缓存索引键必须由强类型主槽位（`PrimarySlot`）、工坊子域枚举（`WorkshopSubDomain?`）以及派生自 Ticket 03 的不可变值对象上下文（`NavigationContext?`）共同构成；
  - 键内部禁止包含 Page、ViewModel、委托引用或任何可变集合；
- **业务状态所有权明确规范**：
  - 本工单与后续实现严禁引入未经批准的“全局聚合数据服务”；
  - `ModListPage` 与 `CommunityPage` 及其现有 ViewModel / Adapter 继续各自独立托管插件与工坊的业务状态与数据源；
  - `PluginWorkshopPage` 仅负责子视图挂载聚合、子域切换与上下文恢复；
  - 后台任务流状态继续由现有任务中心调度模块独立托管。

### 3. 演进检查点与单一可执行文件红线
- 版本演进过程中，依靠 Git 提交检查点（Checkpoints / Tags，例如 `pre-native-shell-switch`）保障平稳推进，避免长期保留两套并行运行时代码；
- 最终程序输出严格遵循项目发布规范，主可执行文件名称永久保持 `UnturnedModManager.exe`，不生成任何名为 V2 的冗余文件。

### 4. 0 回归门禁准则
- 现存 162 项测试必须保持 100% 绿灯；
- 门禁指标严格执行：0 编译错误，0 新增代码警告；既有环境构建提示（如 NETSDK1057）单独客观记录；
- 契约测试必须真实验证槽位调度、次级入口呼出、后退栈一致性与不可变键值匹配，严禁通过虚报或绕过测试手段掩饰验证缺口。

---

## 验收核对结果 (Sign-off)

- [x] 确立 7 个现有页面在单一内容宿主下的挂载拓扑，消除双重历史栈；
- [x] 消除公开契约对底层 `Page` 类型的依赖，采用强类型结构化请求；
- [x] `NavigationCacheKey` 完全使用强类型子域与不可变强类型上下文，禁止裸字符串约定；
- [x] 状态所有权严格解耦，删除未经证明的聚合数据服务，现有页面与 Adapter 各自托管业务状态；
- [x] 明确当前活动宿主 Shell 唯一事实源，消除全局状态机与未来多窗口的潜在冲突；
- [x] 锁定 5 阶段演进里程碑、Git 提交检查点与单一程序文件名称；
- [x] 确定 162 项基线全绿守卫、0 编译错误与 0 新增代码警告度量标准；
- [x] 浮层外部点击由遮罩消费不触发底层、支持减弱动态效果、键盘焦点环与 960×600 紧凑视口验收规约落盘。
