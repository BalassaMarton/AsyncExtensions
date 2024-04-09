using System.Diagnostics;
using Moq;

namespace AsyncExtensions.Tests;

public partial class AsyncLockTests
{
    [Fact]
    public void LockAsync_returns_synchronously_when_the_lock_is_not_already_taken()
    {
        var vt = _lock.LockAsync();

        vt.IsCompletedSuccessfully.Should().BeTrue();

        vt.Result.Dispose();
    }

    [Fact]
    public async Task LockAsync_returns_a_Task_when_the_lock_is_already_taken()
    {
        using var first = await _lock.LockAsync();
        var second = _lock.LockAsync();

        second.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task LockAsync_queues_callers()
    {
        var messages = new List<int>();

        var count = 10;
        var tasks = new List<Task>();

        for (var i = 0; i < count; i++)
            tasks.Add(CreateTask(i));

        await Task.WhenAll(tasks);

        messages.Should().Equal(Enumerable.Range(0, count).ToArray());

        async Task CreateTask(int key)
        {
            using (await _lock.LockAsync())
            {
                messages.Add(key);
            }
        }
    }

    [Fact]
    public async Task Recursive_LockAsync_with_token()
    {
        using var token = await _lock.LockAsync();
        using var token2 = await _lock.LockAsync(token);
        using var token3 = await _lock.LockAsync(token2);
        var token4 = _lock.LockAsync();
        token4.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task Recursive_LockAsync_without_token()
    {
        var mutex = new AsyncLock(new AsyncLockOptions { AllowReentrancy = true });

        using (await mutex.LockAsync())
        {
            using (await mutex.LockAsync()) { }
        }

        var task = mutex.LockAsync();
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Recursive_LockAsync_race_using_WithReentrancy()
    {
        var mutex = new AsyncLock(new AsyncLockOptions { AllowReentrancy = true });
        var gates = Enumerable.Range(0, 5).Select(_ => new SemaphoreSlim(0)).ToArray();
        var events = new List<string>();

        var task1 = DoWork("1", gates[0], gates[1], gates[2]);
        var task2 = DoWork("2", gates[2], gates[3], null);
        gates[0].Release();
        gates[1].Release();
        gates[3].Release();

        await Task.WhenAll(task1, task2).WaitAsync(Timeout);

        events.Should()
            .Equal(
                "1 outer",
                "1 inner",
                "2 outer",
                "2 inner");

        async Task DoWork(string label, SemaphoreSlim gate1, SemaphoreSlim gate2, SemaphoreSlim? next)
        {
            await gate1.WaitAsync();

            using ((await mutex.LockAsync().ConfigureAwait(false)).WithReentrancy())
            {
                events.Add($"{label} outer");
                next?.Release();

                using (await mutex.LockAsync())
                {
                    events.Add($"{label} inner");
                    await gate2.WaitAsync();
                }
            }
        }
    }

    [Fact]
    public async Task Recursive_RunAsync_race_without_token()
    {
        var mutex = new AsyncLock(new AsyncLockOptions { AllowReentrancy = true });
        var gates = Enumerable.Range(0, 5).Select(_ => new SemaphoreSlim(0)).ToArray();
        var events = new List<string>();

        var task1 = DoWork("1", gates[0], gates[1], gates[2]);
        var task2 = DoWork("2", gates[2], gates[3], null);
        gates[0].Release();
        gates[1].Release();
        gates[3].Release();

        await Task.WhenAll(task1, task2).WaitAsync(Timeout);

        events.Should()
            .Equal(
                "1 outer",
                "1 inner",
                "2 outer",
                "2 inner");

        async Task DoWork(string label, SemaphoreSlim gate1, SemaphoreSlim gate2, SemaphoreSlim? next)
        {
            await gate1.WaitAsync();

            await mutex.RunAsync(
                async () =>
                {
                    events.Add($"{label} outer");
                    next?.Release();

                    mutex.RunAsync(
                        async () =>
                        {
                            events.Add($"{label} inner");
                            await gate2.WaitAsync();
                        });
                });
        }
    }

    [Fact]
    public async Task Recursive_LockAsync_without_token_StartNew()
    {
        var mutex = new AsyncLock(new AsyncLockOptions { AllowReentrancy = true });

        using (await mutex.LockAsync())
        {
            await Task.Factory.StartNew(
                async () =>
                {
                    using (await mutex.LockAsync()) { }
                });
        }

        var task = mutex.LockAsync();
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Recursive_LockAsync_without_token_Run()
    {
        var mutex = new AsyncLock(new AsyncLockOptions { AllowReentrancy = true });

        using (await mutex.LockAsync())
        {
            await Task.Run(
                async () =>
                {
                    using (await mutex.LockAsync()) { }
                });
        }

        var task = mutex.LockAsync();
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Recursive_LockAsync_without_token_Run_Run()
    {
        var mutex = new AsyncLock(new AsyncLockOptions { AllowReentrancy = true });

        await Task.Run(
            async () =>
            {
                using (await mutex.LockAsync())
                {
                    await Task.Run(
                        async () =>
                        {
                            using (await mutex.LockAsync()) { }
                        });
                }
            });

        var task = mutex.LockAsync();
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Recursive_LockAsync_without_token_CAF()
    {
        var action = Mock.Of<Action>();
        var mutex = new AsyncLock(new AsyncLockOptions { AllowReentrancy = true });

        await Task.Run(
            async () =>
            {
                using (await mutex.LockAsync())
                {
                    action();
                    await DoWork().ConfigureAwait(false);
                    await DoWork().ConfigureAwait(false);
                }
            });

        var task = mutex.LockAsync();
        task.IsCompletedSuccessfully.Should().BeTrue();

        Mock.Get(action).Verify(_ => _(), Times.Exactly(3));

        async Task DoWork()
        {
            using (await mutex.LockAsync())
            {
                action();
            }
        }
    }

    [Theory]
    [InlineData(new[] { 0, 1, 2 })]
    [InlineData(new[] { 1, 2, 0 })]
    [InlineData(new[] { 2, 1, 0 })]
    public async Task Lock_with_parent_chain(int[] ordering)
    {
        var locks = Enumerable.Range(0, ordering.Length).Select(i => new AsyncLock()).ToList();
        AsyncLock.Chain(locks);
        var invocations = new List<int>();
        var semaphore = new TaskCompletionSource();
        var allTasks = Task.WhenAll(ordering.Select(i => AddTask(i, locks[i])));
        semaphore.SetResult();
        await allTasks;

        invocations.Should().Equal(ordering);

        async Task AddTask(int id, AsyncLock asyncLock)
        {
            using (await asyncLock.LockAsync())
            {
                await semaphore.Task;
                invocations.Add(id);
            }
        }
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(128)]
    public async Task Contention_test(int concurrency)
    {
        var maxDelay = 50;
        var random = new Random(0);
        var randomDelays = Enumerable.Range(0, concurrency).Select(_ => random.Next(maxDelay)).ToArray();

        var tasks = new List<Task>();

        foreach (var i in randomDelays)
            tasks.Add(CreateTask(i));

        await Task.WhenAll(tasks);

        async Task CreateTask(int delay)
        {
            await Task.Delay(delay);

            using (await _lock.LockAsync())
            {
                await Task.Delay(delay);
            }
        }
    }
    
    private static TimeSpan Timeout =>
        Debugger.IsAttached ? System.Threading.Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(1);

    private readonly AsyncLock _lock = new();
}
