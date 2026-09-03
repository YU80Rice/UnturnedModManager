# 02: 启动传参与单实例安全队列分发

**What to build:** 当用户在 Windows 中双击 `.ummpk` 或 `.ummtheme` 文件，或者向已运行的应用传入文件路径参数时，启动器能够准确解析外部文件意图。主进程将传入的文件请求放入 FIFO 队列中统一调度，采用单一模态守护（Single Modal Guard）依次处理；若在首次启动引导（`OnboardingWindow`）未完成期间收到外部文件，请求被安全暂存，待主窗口加载就绪后依序派发，防止窗口重叠与竞态死锁。

**Blocked by:** 01: Windows 文件类型注册与设置页管理

**Status:** resolved

- [x] 命令行参数解析器支持识别并提取 `.ummpk` 与 `.ummtheme` 本地文件绝对路径
- [x] 单实例跨进程激活（Named Pipe）正确传递并接收外部文件路径
- [x] 主窗口建立 FIFO 队列与单一向导守护，支持连续多个双击请求依序排队展示
- [x] 首次启动向导运行期间，外部传参安全挂起，向导结束后依序消费
- [x] 包含参数解析器对各类合法/非法/混合参数提取的单元测试

## Answer
已通过 TDD 循环完成 Ticket 02 的全部交付：
1. 实现了 `ShellFileIntentType` 与 `ShellFileIntent`，并在 `ShellAssociationService` 中实现 `TryParseFileIntent` 与 `FindFileIntent`，能够精确从命令行及参数列表中提取合法 `.ummpk` 与 `.ummtheme` 路径并过滤无关参数；
2. 实现了线程安全的 `ShellIntentQueue`，封装 FIFO 队列并提供单一模态守护（Single Modal Guard）状态管理；
3. 在 `App.xaml.cs` 中集成了冷启动与次级命名管道激活时的文件入队、首启动向导期间挂起暂存与主窗口就绪后的依序消费；
4. 在 `MainWindow.xaml.cs` 中接入 `HandleShellFileIntent`，并在测试用例中验证了队列时序、单一排队拦截与参数提取，单元测试 102/102 全部通过。
