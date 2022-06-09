// See https://aka.ms/new-console-template for more information

using System.Reflection;
using AsyncTools.Benchmarks;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run(Assembly.GetExecutingAssembly());
