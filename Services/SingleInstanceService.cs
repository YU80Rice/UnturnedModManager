using System.IO.Pipes;
using System.IO;
using System.Text;

namespace UnturnedModManager.Services;

/// <summary>
/// Ensures only one UMM process manages a game's plugin directory at a time.
/// A secondary launch forwards its activation arguments to the primary process and exits.
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    private const string DefaultMutexName = @"Local\UnturnedModManager.SingleInstance";
    private const string DefaultPipeName = "UnturnedModManager.SingleInstance";

    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cancellation = new();
    private Mutex? _mutex;
    private Task? _listenTask;
    private bool _ownsMutex;

    public SingleInstanceService(string? mutexName = null, string? pipeName = null)
    {
        _mutexName = string.IsNullOrWhiteSpace(mutexName) ? DefaultMutexName : mutexName;
        _pipeName = string.IsNullOrWhiteSpace(pipeName) ? DefaultPipeName : pipeName;
    }

    public bool IsPrimary { get; private set; }

    /// <summary>
    /// Raised on a background thread when a secondary UMM launch is forwarded to this process.
    /// The application must marshal UI work to its dispatcher.
    /// </summary>
    public event Action<string[]>? Activated;

    /// <summary>
    /// Acquires the instance lock without forwarding an activation. Kept for callers that only
    /// need a lightweight ownership check.
    /// </summary>
    public bool TryAcquire() => TryAcquire(activationArgs: null);

    /// <summary>
    /// Acquires the primary lock. If another primary is listening, forwards <paramref name="activationArgs"/>
    /// over a current-user named pipe and returns <c>false</c> so the secondary process can exit.
    /// </summary>
    public bool TryAcquire(string[]? activationArgs)
    {
        if (_mutex is not null)
            return IsPrimary;

        try
        {
            _mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
            if (createdNew)
            {
                _ownsMutex = true;
                IsPrimary = true;
                return true;
            }

            // A parameterless ownership probe intentionally does not contact the pipe or wait on
            // the mutex. A mutex is recursive for the owning thread, so waiting here could make a
            // second service in the same process look like a valid primary owner.
            if (activationArgs is null)
            {
                _mutex.Dispose();
                _mutex = null;
                return false;
            }

            if (TryForwardArgs(activationArgs))
            {
                _mutex.Dispose();
                _mutex = null;
                return false;
            }

            // The first process may have exited between creating its mutex and listening on the pipe.
            // In that case it is safe to take ownership; otherwise do not start a second writer.
            try
            {
                if (_mutex.WaitOne(millisecondsTimeout: 500))
                {
                    _ownsMutex = true;
                    IsPrimary = true;
                    return true;
                }
            }
            catch (AbandonedMutexException)
            {
                _ownsMutex = true;
                IsPrimary = true;
                return true;
            }

            _mutex.Dispose();
            _mutex = null;
            return false;
        }
        catch
        {
            // A broken mutex subsystem should not make the launcher unusable. File operations still
            // retain their own validation and the app remains usable in this rare fallback case.
            _mutex?.Dispose();
            _mutex = null;
            _ownsMutex = false;
            IsPrimary = true;
            return true;
        }
    }

    public void StartListening()
    {
        if (!IsPrimary || _listenTask is not null)
            return;

        _listenTask = Task.Run(() => ListenLoopAsync(_cancellation.Token));
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                var payload = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                Activated?.Invoke(DecodeArgs(payload));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // A failed handoff is non-fatal. Keep serving future activation attempts.
                try { await Task.Delay(200, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private bool TryForwardArgs(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.Out,
                PipeOptions.CurrentUserOnly);
            client.Connect(timeout: 1500);

            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(EncodeArgs(args));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string EncodeArgs(IEnumerable<string> args) =>
        string.Join('\u001F', args.Select(arg => (arg ?? string.Empty).Replace("\u001F", string.Empty)));

    private static string[] DecodeArgs(string? payload) => string.IsNullOrEmpty(payload)
        ? Array.Empty<string>()
        : payload.Split('\u001F', StringSplitOptions.None);

    public void Dispose()
    {
        _cancellation.Cancel();
        try { _listenTask?.Wait(millisecondsTimeout: 500); }
        catch { }
        _cancellation.Dispose();

        if (_ownsMutex && _mutex is not null)
        {
            try { _mutex.ReleaseMutex(); }
            catch { }
        }

        _ownsMutex = false;
        IsPrimary = false;
        _mutex?.Dispose();
        _mutex = null;
    }
}
