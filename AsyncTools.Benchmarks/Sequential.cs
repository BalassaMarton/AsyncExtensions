using BenchmarkDotNet.Attributes;
using Nito.AsyncEx;
using AsyncTools;
using Microsoft.CodeAnalysis.Operations;
using NitoLock = Nito.AsyncEx.AsyncLock;
using AsyncLock = AsyncTools.AsyncLock;

namespace AsyncTools.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class Sequential
{
    private const int NumberOfOperations = 10000; 
    
    private NitoLock _nitoLock;
    
    [GlobalSetup(Target = nameof(Nito_LockAsync))]
    public void Nito_LockAsync_Setup()
    {
        _nitoLock = new NitoLock();
    }
    
    [Benchmark(Baseline = true)]
    public async Task Nito_LockAsync()
    {
        for (int i = 0; i < NumberOfOperations; i++)
        {
            using (await _nitoLock.LockAsync())
            {
            }
        }
    }
    
    private AsyncLock _lock;
    
    [GlobalSetup(Target = nameof(AsyncTools_LockAsync))]
    public void AsyncTools_LockAsync_Setup()
    {
        _lock = new AsyncLock();
    }
    
    [Benchmark]
    public async Task AsyncTools_LockAsync()
    {
        for (int i = 0; i < NumberOfOperations; i++)
        {
            using (await _lock.LockAsync())
            {
            }
        }
    }
}
