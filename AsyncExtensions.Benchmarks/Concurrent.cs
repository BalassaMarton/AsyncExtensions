using BenchmarkDotNet.Attributes;
using NitoLock = Nito.AsyncEx.AsyncLock;

namespace AsyncExtensions.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class Concurrent
{
    public Concurrent()
    {
        GenerateRandomDelays();
    }

    [Params(4, 8, 16)]
    public int Concurrency { get; set; }

    [GlobalSetup(Target = nameof(Nito_LockAsync))]
    public void Nito_LockAsync_Setup()
    {
        _nitoLock = new NitoLock();
    }

    [Benchmark(Baseline = true)]
    public async Task Nito_LockAsync()
    {
        var tasks = new List<Task>();

        foreach (var i in _randomDelays)
            tasks.Add(Task.Run(() => CreateTask(i)));

        await Task.WhenAll(tasks);

        async Task CreateTask(int delay)
        {
            await Task.Delay(delay);

            for (var i = 0; i < NumberOfOperations; i++)
            {
                using (await _nitoLock.LockAsync())
                {
                    await Task.Delay(delay);
                }
            }
        }
    }

    [GlobalSetup(Target = nameof(AsyncExtensions_LockAsync))]
    public void AsyncExtensions_LockAsync_Setup()
    {
        _lock = new AsyncLock();
    }

    [Benchmark]
    public async Task AsyncExtensions_LockAsync()
    {
        var tasks = new List<Task>();

        foreach (var i in _randomDelays)
            tasks.Add(Task.Run(() => CreateTask(i)));

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

    private const int NumberOfOperations = 10;

    private AsyncLock _lock;

    private NitoLock _nitoLock;

    private int[] _randomDelays;

    private void GenerateRandomDelays()
    {
        var random = new Random(0);
        _randomDelays = Enumerable.Range(0, Concurrency).Select(_ => random.Next(10)).ToArray();
    }
}
