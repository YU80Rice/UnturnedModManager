using System.Threading;

namespace UnturnedModManager.Services;

/// <summary>
/// 防止多个 UMM 进程同时修改同一个 Unturned 插件目录。
/// 当前阶段只负责拒绝第二个实例；协议唤醒和已有窗口激活会在协议安装功能中接入。
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = @"Local\UnturnedModManager.SingleInstance";
    private readonly string _mutexName;
    private Mutex? _mutex;

    public SingleInstanceService(string? mutexName = null) =>
        _mutexName = string.IsNullOrWhiteSpace(mutexName) ? MutexName : mutexName;

    public bool IsPrimary { get; private set; }

    public bool TryAcquire()
    {
        if (_mutex is not null)
            return IsPrimary;

        try
        {
            _mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
            IsPrimary = createdNew;
            if (!createdNew)
            {
                _mutex.Dispose();
                _mutex = null;
            }
            return IsPrimary;
        }
        catch
        {
            // 锁创建失败不应阻止用户使用启动器；继续运行并交由文件操作层保护。
            IsPrimary = true;
            return true;
        }
    }

    public void Dispose()
    {
        if (!IsPrimary || _mutex is null)
            return;

        try { _mutex.ReleaseMutex(); } catch { }
        _mutex.Dispose();
        _mutex = null;
        IsPrimary = false;
    }
}
