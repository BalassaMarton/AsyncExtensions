using BenchmarkDotNet.Attributes;
using NitoLock = Nito.AsyncEx.AsyncLock;

namespace AsyncExtensions.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class Sequential
{
    public const int NumberOfOperations = 10000;

    [GlobalSetup(Target = nameof(Nito_LockAsync))]
    public void Nito_LockAsync_Setup()
    {
        _nitoLock = new NitoLock();
    }

    [Benchmark(Baseline = true)]
    public async Task Nito_LockAsync()
    {
        for (var i = 0; i < NumberOfOperations; i++)
            using (await _nitoLock.LockAsync()) { }
    }

    [GlobalSetup(Target = nameof(AsyncExtensions_LockAsync))]
    public void AsyncExtensions_LockAsync_Setup()
    {
        _lock = new AsyncLock();
    }

    [Benchmark]
    public async Task AsyncExtensions_LockAsync()
    {
        for (var i = 0; i < NumberOfOperations; i++)
            using (await _lock.LockAsync()) { }
    }

    private AsyncLock _lock;

    private NitoLock _nitoLock;
}
