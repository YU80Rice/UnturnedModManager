# Ticket B: Legacy NavigationFacade 兼容桥与无底层 UI 泄漏请求重构 (Legacy NavigationFacade Adapter)

**What to build:** 将既有旧导航服务 `AppNavigationService.Current` 重构为面向当前活动 `INavigationShellHost` 的门面适配器（Facade Adapter）。彻底消除原先通过 `Page source` 将底层 WPF 页面控件实例暴露给导航层的设计，统一改由结构化强类型请求转发到宿主 Shell。确保调用兼容门面与直接调用宿主 Shell 行为完全一致，消除双重历史栈隐患，维持旧页面平滑调用。

**Input contracts:**
- `AppNavigationService.Current` 仅作为兼容门面，解析并调用当前宿主的 `INavigationShellHost`；
- 移除携带 `Page source` 的方法签名，改用不可变结构化请求对象；
- 单一历史栈事实源，门面自身不维护独立的页面栈。

**Blocked by:** Ticket A: NavigationShell 契约与逻辑宿主

**Status:** ready-for-agent

## 范围外 (Out of Scope)
- 不修改现有页面的内部 ViewModel 业务数据逻辑；
- 不修改与页面导航无关的独立业务模块。

## 验收标准 (Acceptance Criteria)
- [ ] 重构 `AppNavigationService`，内部解析并持有当前活动 `INavigationShellHost` 引用
- [ ] 移除对底层 UI `Page` 类型的依赖参数，改用强类型结构化上下文
- [ ] 兼容旧有页面（如从某页面跳转到设置或关于）的调用路径，由门面适配器无缝转发至宿主 Shell
- [ ] 确保单一历史栈事实源，门面转发与直接宿主调用的状态完全一致
- [ ] 编写适配器契约测试，验证门面桥接转发、宿主同步与异常参数防守，测试全部通过
