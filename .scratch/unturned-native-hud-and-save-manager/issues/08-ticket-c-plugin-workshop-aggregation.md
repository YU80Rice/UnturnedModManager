# Ticket C: PluginWorkshopPage 模组工坊纯导航聚合外壳与上下文恢复 (PluginWorkshopPage Aggregation Host)

**What to build:** 构建 `PluginWorkshopPage` 作为 Slot 3（模组工坊）的纯导航聚合宿主。在单一容器内挂载现有的两个独立子页面（`ModListPage` 本地插件 与 `CommunityPage` 社区发现），通过 `Visibility`（`Visible` vs `Collapsed`）切换保持单活跃视图，隔离焦点命中与辅助功能树。为社区模组详情提供自治适配器，并在子域切换或返回时精准恢复搜索词、筛选选项与滚动位置。

**Input contracts:**
- `PluginWorkshopPage` 仅负责导航聚合、子域选择切换、标题计数与上下文恢复；
- `ModListPage` 与 `CommunityPage` 及其现有 ViewModel 保持各自独立托管状态与数据源，严禁引入全局聚合数据服务；
- 社区插件详情请求由 Slot 3 自治适配器消化与渲染，不暴露给顶级宿主 Shell。

**Blocked by:** Ticket A: NavigationShell 契约与逻辑宿主

**Status:** ready-for-agent

## 范围外 (Out of Scope)
- 严禁引入未经批准的“全局聚合数据服务”；
- 不重构 `ModListPage` 或 `CommunityPage` 内部已有的网络请求与模组管理业务逻辑。

## 验收标准 (Acceptance Criteria)
- [ ] 实现 `PluginWorkshopPage`，具备本地插件与社区发现双子域切换导航栏
- [ ] 单一容器内挂载 `ModListPage` 与 `CommunityPage`，以 `Visibility` 控制活跃态，未激活视图移出焦点树与命中测试
- [ ] 两个子域的刷新、错误展示与过滤状态互不干扰、各自隔离
- [ ] Slot 3 自治消化社区插件详情展示与返回，精准恢复原列表筛选与滚动上下文
- [ ] 编写单元测试验证子域切换状态隔离、上下文暂存恢复与单活跃视图契约，测试全部通过
