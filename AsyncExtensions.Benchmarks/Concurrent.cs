using BenchmarkDotNet.Attributes;
using NitoLock = Nito.AsyncEx.AsyncLock;

namespace AsyncExtensions.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class Concurrent
{
    //[Params(8, 16, 32)]
    public int Concurrency { get; set; } = 8;

    private const int NumberOfOperations = 10; 

    private int[] _randomDelays;

    private void GenerateRandomDelays()
    {
        var random = new Random(0);
        _randomDelays = Enumerable.Range(0, Concurrency).Select(_ => random.Next(10)).ToArray();
    }
    
    private NitoLock _nitoLock;
    
    [GlobalSetup(Target = nameof(Nito_LockAsync))]
    public void Nito_LockAsync_Setup()
    {
        GenerateRandomDelays();
        _nitoLock = new NitoLock();
    }
    
    [Benchmark]
    public async Task Nito_LockAsync()
    {
        var tasks = new List<Task>();
        foreach (var i in _randomDelays)
        {
            tasks.Add(Task.Run(() => CreateTask(i)));
        }

        await Task.WhenAll(tasks);

        async Task CreateTask(int delay)
        {
            await Task.Delay(delay);
            for (int i = 0; i < NumberOfOperations; i++)
            {
                using (await _nitoLock.LockAsync())
                {
                    await Task.Delay(delay);
                }
            }
        }
    }
    
    private AsyncLock _lock;
    
    [GlobalSetup(Target = nameof(AsyncTools_LockAsync))]
    public void AsyncTools_LockAsync_Setup()
    {
        GenerateRandomDelays();
        _lock = new AsyncLock();
    }
    
    [Benchmark]
    public async Task AsyncTools_LockAsync()
    {
        var tasks = new List<Task>();
        foreach (var i in _randomDelays)
        {
            tasks.Add(Task.Run(() => CreateTask(i)));
        }

        await Task.WhenAll(tasks);

        async Task CreateTask(int delay)
        {
            await Task.Delay(delay);
            for (var i = 0; i < NumberOfOperations; i++)
            {
                using (await _lock.LockAsync())
                {
                    await Task.Delay(delay);
                }
            }
        }
    }
}
