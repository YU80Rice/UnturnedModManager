# Ticket 04: HomeSnapshot 响应式不可变快照投影与代际防覆盖降级模型 (HomeSnapshot Projection & Degradation Model)

Status: open
Type: grilling
Blocked by: 03

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
