namespace Umbra.Core;

/// <summary>
/// Coordinates the desktop process so launching Umbra again activates the
/// already-running dashboard instead of creating another tray application.
/// Watchdog processes do not use this coordinator.
/// </summary>
public sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly EventWaitHandle _activationEvent;
    private readonly Mutex _instanceMutex;
    private RegisteredWaitHandle? _activationRegistration;
    private int _disposed;

    public SingleInstanceCoordinator(string identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);

        // Create/open the event before the mutex. If two processes start at
        // nearly the same time, the secondary can signal immediately and the
        // primary will observe the pending signal when it starts listening.
        _activationEvent = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            $@"Local\{identity}.Activate");

        _instanceMutex = new Mutex(
            false,
            $@"Local\{identity}.SingleInstance",
            out var createdNew);

        IsPrimary = createdNew;
    }

    public bool IsPrimary { get; }

    public void Listen(Action onActivation)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(onActivation);
        if (!IsPrimary)
            throw new InvalidOperationException("Only the primary instance can listen for activation.");
        if (_activationRegistration is not null)
            throw new InvalidOperationException("The activation listener is already registered.");

        _activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            (_, _) => onActivation(),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public void SignalPrimary()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _activationEvent.Set();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _activationRegistration?.Unregister(null);
        _instanceMutex.Dispose();
        _activationEvent.Dispose();
    }
}
