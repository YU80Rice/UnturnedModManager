# Ticket 05: 现有 7 大业务页面平稳迁入与 0 回归验证方案 (Zero-Regression Page Migration & Verification Specification)

Status: open
Type: grilling
Blocked by: 

## 问题

针对 PM 裁定的本张地图正式 DoD：“完成 Unturned 风格 Shell、BackdropHost、HomeSnapshot 和现有页面平稳迁入，原有启动、插件、设置、社区功能行为不回归，任务中心、账户、关于次级入口可用，数据失败时仍可降级显示”：

1. **页面迁入实施步骤与契约测试**：
   - 确立 7 个现有页面挂载入 `NavigationShell` 的平滑切换路径；
   - 编写针对 `NavigationShell` 的宿主契约测试，断言 5 槽位点击、次级入口呼出、后退栈导航与默认展示行为。
2. **全链路 0 回归防线（Zero-Regression Guardrails）**：
   - 验证现有全套 155 项自动化测试（包括 BepInEx 环境、DXVK 探测、模组启停、社区安装、任务中心、主题与壁纸模糊联动、滚轮路由）在原生 Shell 下持续 100% 绿灯，0 失败、0 警告、0 错误；
   - 编写针对 `HomeSnapshot` 离线缓存、超时降级与代际防覆盖的新增单元测试。
3. **人工视觉验收与异常降级核验清单**：
   - 建立高覆盖度视觉验收标准（含 13 套调色板适配、WCAG AA 对比度复查、网络断开离线展示、初次引导体验）。
