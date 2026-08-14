# 参与 Unturned Mod Manager

感谢你愿意改进 UMM。请先通过 Issue 说明问题或较大的设计提案；小型修复可以直接提交 Pull Request。

## 项目缘起

UMM 的诞生不仅来自技术需求，也来自朋友们的鼓励。感谢那些在项目还不完整、维护者仍担心代码不够漂亮而犹豫是否开源时，愿意给予肯定和陪伴的人。这个项目是献给所有共同在 PEI 篝火旁度过青春岁月的伙伴们的礼物。

感谢 Nelson Sexton 和 Smartly Dressed Games 创造 Unturned，并向社区开放可供模组开发者学习的 U3-Docs/U3-SDK 资料。也感谢 PCL2、HMCL、UML 等启动器项目证明：管理工具不仅可以“能用”，还可以拥有清晰、流畅且有温度的体验。

这份初心不会因为架构升级或文档严谨化而被替换。工程规范负责保护玩家数据，开源许可负责保护共同成果，而项目最终仍是为了让玩家更自由地理解和掌控自己的游戏体验。

## 开发环境

- Windows 10/11 x64
- .NET 8 SDK
- 可选：Visual Studio 2022、Rider 或 VS Code

```powershell
dotnet build .\UnturnedModManager.csproj -c Debug
dotnet test .\UnturnedModManager.Tests\UnturnedModManager.Tests.csproj
```

提交前请至少确保 Debug/Release 构建成功、自动化测试通过，并说明是否会修改用户游戏目录、社区安装清单或账户状态。

## 代码与交互原则

- 页面代码负责界面事件和视图绑定，业务规则优先放入 ViewModel 或 Service；
- 所有文件安装、覆盖、移动和删除必须具有明确范围与失败恢复策略；
- 不自动处理 CAPTCHA，不记录密码，不在日志或 Issue 中暴露社区令牌；
- 本地插件、社区插件和玩家手动修改的文件必须清楚区分；
- 新增界面需要覆盖加载、空数据、错误、禁用和返回导航状态；
- 用户可见文字优先使用简体中文，并避免无法验证的性能或兼容性承诺。

## 许可证

向 v2.0 及之后分支提交贡献，即表示你确认有权提交相关代码，并同意该贡献按 `GPL-2.0-only` 随项目分发。不要提交来源不明、许可证不兼容或无法提供对应许可声明的代码与资源。

历史 v1.x 版本曾采用 MIT 许可证。相关说明见 [`NOTICE.md`](./NOTICE.md) 和 [`LICENSES/MIT-legacy.txt`](./LICENSES/MIT-legacy.txt)。

## 贡献与致谢

- [YU80Rice](https://github.com/YU80Rice)：项目发起、产品方向、验收与发布；
- [Ayndpa](https://github.com/Ayndpa)：[unturned-mod-loader](https://github.com/Ayndpa/unturned-mod-loader) 作者；其项目为 UMM v2.0 提供重要设计参考，并通过 [PR #7](https://github.com/YU80Rice/UnturnedModManager/pull/7) 直接贡献滚轮交互修复；
- OpenAI GPT（Codex）：参与 v2.0 架构重构、交互完善、缓存与会话可靠性、测试、发布验证和文档校订；
- Gemini、Claude、Kimi、Cherry Claw：在不同阶段参与设计讨论、问题分析与代码协作；
- Nelson Sexton、Smartly Dressed Games、BepInEx、WPF-UI、DXVK、PCL2、HMCL 及其他开源社区成员：提供游戏、工具、组件与设计启发。

AI 生成或辅助的内容必须经过人工验收。项目维护者对最终合并、许可证兼容性和发布结果负责。
