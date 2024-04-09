using BenchmarkDotNet.Attributes;
using DotNext.Threading;

namespace AsyncExtensions.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class Concurrent
{
    [Benchmark]
    public async Task Nito_AsyncLock()
    {
        await RunBenchmark(
            () => new ValueTask<IDisposable>(_nitoAsyncLock.LockAsync().AsTask()));
    }

    [Benchmark]
    public async Task Reactive_AsyncGate()
    {
        await RunBenchmark(
            () => _reactiveAsyncGate.LockAsync());
    }

    [Benchmark]
    public async Task DotNext_AsyncExclusiveLock()
    {
        await RunBenchmark(
            () => _dotNextAsyncExclusiveLock.AcquireLockAsync(CancellationToken.None));
    }

    [Benchmark(Baseline = true)]
    public async Task AsyncExtensions_LockAsync()
    {
        await RunBenchmark(() => _asyncLock.LockAsync());
    }

    private async Task RunBenchmark<TReleaser>(Func<ValueTask<TReleaser>> lockFunc) where TReleaser : IDisposable
    {
        var tasks = Task.WhenAll(
            GatedTask(_gates[0], _gates[1]),
            GatedTask(_gates[1], _gates[2]),
            GatedTask(_gates[2], null));

        _gates[0].Release();

        async Task GatedTask(SemaphoreSlim gate, SemaphoreSlim? next)
        {
            await gate.WaitAsync();

            using (await lockFunc())
            {
                next?.Release();
                await Task.Yield();
            }
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _gates = new SemaphoreSlim[] { new(0), new(0), new(0) };
    }

    private SemaphoreSlim[] _gates = Array.Empty<SemaphoreSlim>();
    
    private AsyncLock _asyncLock = new();
    private NitoAsyncLock _nitoAsyncLock = new();
    private ReactiveAsyncGate _reactiveAsyncGate = new();
    private DotNextAsyncExclusiveLock _dotNextAsyncExclusiveLock = new();
}
