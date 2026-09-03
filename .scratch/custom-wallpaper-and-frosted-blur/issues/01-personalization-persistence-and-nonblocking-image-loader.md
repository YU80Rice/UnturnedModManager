# 01: 壁纸个性化持久化存储与非阻塞图片加载引擎

Type: task
Status: resolved
Blocked by: None (can start immediately)

## 问题

当前启动器缺少玩家个性化壁纸设置的数据持久化定义（壁纸路径、模糊半径、遮罩浓度），且在读取外部图片文件时若直接使用标准 URI 加载会导致 Windows 进程锁定玩家图片文件句柄，无法删除或重命名；且缺少对破损或越界图片的安全加载与合法值边界约束保护。

如何建立一套安全、非阻塞、具备合法边界约束的壁纸个性化持久化模型与图片加载引擎？

## 验收条件

- [x] 在 `AppSettings.cs` 与配置数据契约中新增 `LauncherCustomWallpaperPath`、`LauncherWallpaperBlurRadius`（0~40px，默认 15.0）、`LauncherWallpaperDimOpacity`（0.1~0.8，默认 0.35）
- [x] 实现内存流非阻塞式安全加载辅助逻辑（读取 bytes 复制进 `MemoryStream` 并 `Freeze()`，立即释放文件锁）
- [x] 边界约束机制：对非法模糊半径与遮罩浓度进行合法值 Clamp 校验
- [x] 容错降级机制：当指定路径为空、不存在或图片字节损坏时，能够安全返回失败并优雅回退，绝不导致程序崩溃
- [x] 编写单元测试覆盖：配置存取序列化、边界值 Clamp、非阻塞文件锁释放及损坏文件降级
