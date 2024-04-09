using BenchmarkDotNet.Attributes;

namespace AsyncExtensions.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class Recursive
{
    [Params(2, 4)]
    public int Recursion { get; set; }

    [Benchmark(Baseline = true)]
    public async Task AsyncExtensions_LockAsync_without_token()
    {
        await LockAsync(_recursiveLock, Recursion);

        static async Task LockAsync(AsyncLock asyncLock, int level)
        {
            level--;

            using ((await asyncLock.LockAsync()).WithReentrancy())
            {
                if (level > 0)
                {
                    await LockAsync(asyncLock, level);
                }
            }
        }
    }

    [Benchmark]
    public async Task AsyncExtensions_LockAsync_with_token()
    {
        await LockAsync(_nonRecursiveLock, Recursion);

        static async Task LockAsync(AsyncLock asyncLock, int level, AsyncLockToken token = default)
        {
            level--;

            using (var nextToken = await asyncLock.LockAsync(token))
            {
                if (level > 0)
                {
                    await LockAsync(asyncLock, level, nextToken);
                }
            }
        }
    }

    private AsyncLock _recursiveLock = new AsyncLock(new AsyncLockOptions { AllowReentrancy = true });
    private AsyncLock _nonRecursiveLock = new AsyncLock();

    [Benchmark]
    public async Task Reactive_AsyncGate()
    {
        await LockAsync(_reactiveAsyncGate, Recursion);

        static async Task LockAsync(ReactiveAsyncGate gate, int level)
        {
            level--;

            using (await gate.LockAsync())
            {
                if (level > 0)
                {
                    await LockAsync(gate, level);
                }
            }
        }
    }

    private ReactiveAsyncGate _reactiveAsyncGate = new();

    // AsyncExclusiveLock does not support recursion
    //[Benchmark]
    //public async Task DotNext_AsyncExclusiveLock()
    //{
    //    await LockAsync(_dotNextAsyncExclusiveLock, Recursion);

    //    static async Task LockAsync(DotNextAsyncExclusiveLock asyncLock, int level)
    //    {
    //        level--;

    //        using (await asyncLock.AcquireLockAsync(CancellationToken.None))
    //        {
    //            if (level > 0)
    //            {
    //                await LockAsync(asyncLock, level);
    //            }
    //        }
    //    }
    //}

    //private DotNextAsyncExclusiveLock _dotNextAsyncExclusiveLock = new();
}