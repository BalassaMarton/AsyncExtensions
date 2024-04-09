using System.Reflection;
using AsyncExtensions.Benchmarks;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

#if DEBUG
BenchmarkRunner.Run(Assembly.GetExecutingAssembly(), new DebugInProcessConfig());
#else
BenchmarkRunner.Run(Assembly.GetExecutingAssembly());
#endif
