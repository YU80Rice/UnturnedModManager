---
status: ready-for-agent
---

# Spec: Unturned 原生拟真 Shell 重塑、HomeSnapshot 响应式投影与无损页面迁移

## 问题陈述

当前 Unturned Mod Manager (UMM) 采用标准扁平卡片与左侧窄边栏布局，用户与模组作者在日常使用中面临以下核心痛点：
1. **视觉沉浸感缺失**：界面缺乏 Unturned 游戏主菜单标志性的厚重胶囊导航、暗调半透明 HUD 面板与全屏沉浸质感，体验与游戏本体割裂；
2. **首页数据流脆弱与竞态风险**：启动状态、游戏环境、Profile 概况、社区推荐与公告分散由不同视图独立请求，缺乏统一的不可变快照，网络波动时容易出现部分卡片闪烁、竞态覆盖或因单一请求失败导致界面无提示空白；
3. **导航状态易丢失**：槽位切换缺乏强类型状态保全机制，用户在模组工坊中精心筛选的条件、搜索词、滚动位置以及后台正在执行的下载任务，在切换到设置页再切回时容易发生重置或视图重建；
4. **组件交互遮挡与无障碍缺陷**：悬浮展示的小助理挂件缺乏动态安全边距约束，在紧凑视口下容易遮挡重要交互控件；弹层交互缺乏外部点击安全消费与减弱动态效果支持；
5. **大规模重构的回归风险**：现有 162 项涵盖 BepInEx 环境、DXVK 探测、模组启停、社区安装、任务调度、壁纸模糊与调色板联动的自动化业务基线，在重构 UI Shell 时面临潜在的回归隐患。

## 解决方案

构建高内聚、低耦合的 Unturned 原生拟真 UI Shell 重塑与数据投影管道系统：
1. **Unturned 原生拟真 UI Shell (UnturnedNativeShell)**：
   - 建立左侧 5 槽位（启动、角色与数据、模组工坊、设置、退出）厚重药丸导航栏、顶部沉浸状态条、左下角微卡片中枢（NavHub 三 Facet 注入）与右下角小助理自适应展台；
   - 引入语义化设计代币体系（UnturnedNativeTokens），支持 13 套调色板动态换肤与 WCAG AAA (15.6:1) 超高对比度合规；
2. **单一事实源导航宿主与强类型保全 (INavigationShellHost)**：
   - 确立 INavigationShell 为当前窗口宿主范围内唯一的导航状态机与事实源，兼容旧门面 AppNavigationService（彻底移除底阶 UI Page 依赖，改用不可变强类型结构化请求）；
   - 以单一主内容容器（Single Content Host）无侵入承载 7 大现有业务页面，现有 Page 继承关系与 ViewModel 完全不改；
   - 采用不可变强类型 NavigationCacheKey(PrimarySlot, WorkshopSubDomain?, NavigationContext?) 稳定保全用户交互上下文与后台作业流；
3. **只读不可变响应式快照投影管道 (IHomeSnapshotCoordinator & HomeSnapshot)**：
   - 首页数据流汇聚为纯只读不可变 HomeSnapshot 树与 ImmutableArray<T> 集合，杜绝 UI 端数据被篡改；
   - 实现双代际流转机制（RefreshGeneration 任务防竞态 vs PublishedRevision 视图单调递增发布）、分批局部发布与最大超时防饿死保证；
   - 确立五态离线降级模型（LiveOnline、CachedValid、CachedStale、Empty、Failed）并辅以明确的 IsInitialized 初始语义，离线断网时自动展示带时效指示的本地缓存；
4. **无障碍可用性与 0 回归质量防线**：
   - 浮层外部点击由半透明遮罩直接消费（不触发底层操作），全键盘高对比焦点环，支持系统级 prefers-reduced-motion 减弱动画，适配 960×600 紧凑视口；
   - 现有 162 项业务自动化测试 100% 绿灯通过，0 编译错误、0 新增代码警告，主程序名永久保持 UnturnedModManager.exe。

## 用户故事

1. 作为 Unturned 玩家，我想要看到与游戏主菜单深度神似的暗调半透明 HUD 面板和大药丸导航栏，以便一打开启动器就获得熟悉亲切的沉浸氛围。
2. 作为玩家，我想要在左侧 5 槽位中点击“游戏启动”槽位，直接看到醒目的英雄启动卡片与当前游戏环境就绪状态，以便快速拉起游戏。
3. 作为玩家，我想要在离线无网络状态下启动启动器，首页依然能即时展示上次缓存的推荐插件与公告内容，并带有明确的“离线缓存”状态指示，以便我的浏览不被中断。
4. 作为玩家，我想要在首次冷启动或数据尚未返回时，首页呈现优雅明确的未就绪状态（而非直接显示失败或空白），以便获得平稳的初始预期。
5. 作为模组作者，我想要在“模组工坊”中自如切换“本地插件”与“社区发现”子域，切换时子域状态完全隔离、错误互不干扰，以便专注于各自的管理场景。
6. 作为模组作者，我想要在工坊列表中输入搜索词、选择特定分类并滚动到中间位置后，切换到“设置”页再切回时，之前的搜索词与滚动偏移原样保留，以便我不必重新搜索。
7. 作为模组作者，我想要在社区发现中点击某个模组进入详情，查看完毕后点击返回按钮能精准恢复到原先的列表筛选与浏览位置，以便顺畅挑选模组。
8. 作为玩家，我想要在后台正在下载多个大型模组包时切换到其他任意槽位，下载任务与进度在后台平稳推进，顶栏与任务中心能实时观察到当前动态，以便操作互不阻塞。
9. 作为玩家，我想要点击“角色与数据”槽位时，看到规整的 4 大子域结构框架与“只读预览”提示，以便了解未来的存档管理能力，且确信当前不会意外损坏本地存档。
10. 作为玩家，我想要点击左下角的环境与社区微卡片，呼出整合了账户、环境诊断与外观切换的控制中枢，以便在一个紧凑视图内快速完成环境检查与主题切换。
11. 作为玩家，我想要在呼出左下角控制面板后，点击面板外部任意暗色遮罩区域，能够直接关闭面板且不触发底下的任何其他按钮，以便防止误触背景操作。
12. 作为玩家，我想要通过键盘按 Esc 键随时关闭当前打开的任何控制面板或浮层，并将键盘焦点自动重置回触发按钮上，以便获得顺畅的全键盘无障碍操作。
13. 作为玩家，我想要在系统设置中启用“减弱动态效果”后，启动器所有页面切换与浮层展开立即退化为瞬时显隐，以便保护眩晕敏感用户并节省低配机器渲染开销。
14. 作为使用 960×600 紧凑分辨率或 150% 高 DPI 缩放屏幕的用户，我想要看到所有导航按钮、启动卡片与文本内容完整自适应展现且安全内边距得当，以便界面绝不发生文本重叠或越界裁切。
15. 作为玩家，我想要右下角的小助理吉祥物挂件永远不会挡住主视口底部的操作按钮或滚动条，当内容滚动到底部时安全边距自适应避让，以便所有控件清晰可见、随时可点。
16. 作为玩家，我想要在设置中关闭小助理挂件时，它彻底从界面和读屏器中隐去，主视口空间自动填满，以便在需要时获得纯净专注的界面。
17. 作为老用户，我想要在新版 UI 下依然能够稳定使用已有的 BepInEx 环境检测、DXVK 一键配置、模组启用禁用以及双模壁纸模糊特效，以便我的核心管理功能完全不退化。
18. 作为主题爱好者，我想要在 13 套调色板之间任意切换，原生 Shell 的大药丸高亮、半透明 HUD 面板描边与状态徽章都能精准同步对应主题色，以便展现统一和谐的视觉质感。
19. 作为运维与发布者，我想要启动器主执行文件名始终保持 UnturnedModManager.exe，以便现有的快捷方式、批处理脚本与自动化发布流程完全无需变动。

## 实现决策

- **单一事实源导航宿主与去 UI 泄漏请求契约 (来自 Ticket 03 & 05 裁定)**：
  - INavigationShell 明确界定为当前活动 NavigationShell 宿主范围内唯一的导航状态机与事实源，不作为全局跨窗口单例；
  - AppNavigationService.Current 仅作为兼容门面适配器，统一将旧调用转交至当前宿主 Shell，彻底消解双重历史栈；
  - 彻底移除 Page source 这类将底层 WPF 页面类型泄漏给上层的参数，全面采用强类型的不可变结构化请求对象：
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
- **强类型不可变 NavigationCacheKey 与状态托管解耦 (来自 Ticket 05 裁定)**：
  - 严格废除裸字符串路由约定，缓存键采用强类型不可变复合值对象：
    ```csharp
    public sealed record NavigationCacheKey(
        PrimarySlot Slot,
        WorkshopSubDomain? SubDomain,
        NavigationContext? Context);
    ```
  - 键内部禁止包含 Page、ViewModel、委托引用或任何可变集合；
  - 状态所有权解耦：ModListPage 与 CommunityPage 及其现有 ViewModel / Adapter 继续独立托管插件业务状态与数据源，本系统绝不引入任何未经证明的全局“聚合数据服务”；PluginWorkshopPage 仅负责子视图挂载、子域切换与上下文恢复；后台任务状态归现有任务中心独立托管。
- **只读响应式快照 HomeSnapshot 树与五态降级模型 (来自 Ticket 04 裁定)**：
  - HomeSnapshot 采用纯只读不可变 Record 树，集合统一采用 ImmutableArray<T>（禁止向 UI 派发 default 数组）；
  - 确立五态离线降级模型与独立的 IsInitialized 初始语义：
    ```csharp
    public enum FeedDataState { LiveOnline, CachedValid, CachedStale, Empty, Failed }

    public sealed record FeedSectionSnapshot<T>(
        FeedDataState State,
        bool IsInitialized,
        ImmutableArray<T> Items,
        DateTimeOffset? LastSynchronized,
        string? FailureCategory)
    {
        public TimeSpan? GetCacheAge(DateTimeOffset nowUtc) =>
            LastSynchronized.HasValue
                ? (nowUtc - LastSynchronized.Value < TimeSpan.Zero ? TimeSpan.Zero : nowUtc - LastSynchronized.Value)
                : null;
    }
    ```
- **Coordinator 双代际流转与生命周期规范 (来自 Ticket 04 裁定)**：
  - RefreshGeneration：内部递增标识异步任务批次，每次触发刷新自增，过期任务结果立即丢弃；
  - PublishedRevision：快照发布版本号，仅在组装完成并向 UI 派发新 HomeSnapshot 完整帧时单调自增；
  - 分批局部发布：本地快速 Section 先行发布完整自洽帧，慢速网络 Section 随后以匹配代际返回时再次合并发布新帧；
  - 最大延迟超时（Max Delay Deadline）防饿死保证，高频触发下强制截断并发布；
  - Coordinator 与当前宿主窗口生命周期一致，随窗口关闭而 Dispose。
- **Unturned 原生设计代币与布局规范 (来自 Ticket 02 & 原型产出)**：
  - 采用来自原型的语义化设计代币（UnturnedNativeTokens.xaml），覆盖深色 HUD 表面色、药丸圆角、半透明描边与 WCAG AAA (15.6:1) 对比度；
  - 小助理挂件位于根 Grid 顶层（In-Tree Overlay），主视口 ScrollViewer 底部边距根据挂件实际测量高度动态调整安全 Insets，挂件关闭时设为 Visibility.Collapsed 并彻底从命中测试树与无障碍树移除；
  - 弹出浮层（Popup）外部区域的点击事件由半透明遮罩直接消费并关闭浮层，不触发任何底层背景控件的操作。
- **五阶段渐进交付里程碑与 Git 检查点 (来自 Ticket 05 裁定)**：
  - 里程碑 1：导航适配器公开契约定义与基础契约测试构建；
  - 里程碑 2：插件工坊与次级目标页面适配挂载；
  - 里程碑 3：首页响应式快照（HomeSnapshot）数据流对接；
  - 里程碑 4：主窗口外壳（MainWindow）装配与综合联动；
  - 里程碑 5：全量自动化验收通过与历史通路平稳退役；
  - 演进过程中以 Git 提交检查点（如 pre-native-shell-switch）作为安全节点，不长期维护两套并行运行时代码；主程序名永久保持 UnturnedModManager.exe。

## 测试决策

- **只测试公开契约与外部行为，不测内部私有实现**：
  - 测试 INavigationShellHost：断言 5 槽位切换时 ActiveSlot 正确翻转、次级入口激活时 ActiveSecondary 正确指示且主槽位取消高亮、返回操作准确恢复原单根来源上下文；
  - 测试 NavigationCacheKey：断言复合键值的相等性、值哈希一致性以及不同子域上下文的隔离性；
  - 测试 IHomeSnapshotCoordinator：断言代际防覆盖机制（旧任务晚于新任务返回时不覆盖新数据）、离线断网时正确回退五态降级模型、初始未加载状态不冒充空数据或失败、时间戳负值防御；
  - 测试 AppNavigationService 兼容门面：断言调用门面转发到宿主 Shell 时行为一致，历史堆栈不重复压入；
  - 测试设计代币与视觉对比度：断言 13 套调色板在深色 HUD 表面上的文字对比度均高于 WCAG AA 标准（大文本 ≥ 3:1，正文 ≥ 4.5:1）。
- **测试先例**：
  - 继承 UnturnedNativeTokensTests.cs、ModelBehaviorTests.cs 与 ShellAssociationTests.cs 的高速、确定性、内存化单元测试惯例。
- **质量准入度量口径**：
  - 现存全部 **162 项**测试保持 100% 绿灯（0 失败、0 错误）；
  - **0 编译错误，0 新增代码警告**；
  - 底层 SDK 环境既有的构建提示（如 NETSDK1057 预览版提示）作为客观环境事实单独记录；
  - 严禁使用 Skip 掩盖测试失败；外部依赖测试若因环境无法运行，必须显式声明运行前置条件并报告为未执行。

## 范围外

- 真实本地单机世界与角色存档文件（Block/River 协议、SaveDataWorkspace、CharacterReadModel、WorldReadModel）的只读模型与解析引擎（已明确单独立项于后续专用地图）；
- 任何存档文件的物理写入、受控编辑事务与备份还原逻辑（本阶段严禁触碰，严格另行立项）；
- 本地插件（ModListPage）与社区插件（CommunityPage）底层业务实现合并（仅在导航层通过工坊页面进行视图聚合，业务层维持各自独立）；
- 游戏运行时内存注入或内存挂钩（维持外部纯净管理工具定位，运行时功能归独立 BepInEx / BUE 插件承担）；
- 引入未经批准的“全局聚合数据服务”；
- 逐像素机械复制 Unturned 原版界面（必须保留 UMM 启动器品牌标识、高 DPI 与无障碍特性）；
- 生成名为 V2 的冗余执行程序名称。

## 补充说明

- 严格遵循 [CONTEXT.md](file:///d:/Agent-工作目录/DevelopMyUNMultiplayerModAndModloader/启动器/UnturnedModManager/CONTEXT.md)（领域模型定义）与 [AGENTS.md](file:///d:/Agent-工作目录/DevelopMyUNMultiplayerModAndModloader/启动器/UnturnedModManager/AGENTS.md)（发布规范）。
- 全面继承并闭环了 Wayfinder 决策地图中全部 5 张前置工单（Ticket 01 存档调研、Ticket 02 UI代币原型、Ticket 03 导航契约、Ticket 04 首页快照、Ticket 05 页面迁入与回归防线）的所有 PM 终审裁定。
