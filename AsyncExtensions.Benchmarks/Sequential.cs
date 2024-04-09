using BenchmarkDotNet.Attributes;
using DotNext.Threading;

namespace AsyncExtensions.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class Sequential
{
    [Benchmark]
    public async Task Nito_AsyncLock()
    {
        using (await _nitoAsyncLock.LockAsync())
        {
            await Task.Yield();
        }
    }

    [Benchmark]
    public async Task Reactive_AsyncGate()
    {
        using (await _reactiveAsyncGate.LockAsync())
        {
            await Task.Yield();
        }
    }

    [Benchmark]
    public async Task DotNext_AsyncExclusiveLock()
    {
        using (await _dotNextAsyncExclusiveLock.AcquireLockAsync(CancellationToken.None))
        {
            await Task.Yield();
        }
    }

    [Benchmark(Baseline = true)]
    public async Task AsyncExtensions_LockAsync()
    {
        using (await _asyncLock.LockAsync())
        {
            await Task.Yield();
        }
    }

    private AsyncLock _asyncLock = new();
    private NitoAsyncLock _nitoAsyncLock = new();
    private ReactiveAsyncGate _reactiveAsyncGate = new();
    private DotNextAsyncExclusiveLock _dotNextAsyncExclusiveLock = new();
}
