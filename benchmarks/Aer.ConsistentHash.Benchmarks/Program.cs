using BenchmarkDotNet.Running;
using Aer.ConsistentHash.Benchmarks;
using Aer.ConsistentHash.Benchmarks.BenchmarkClasses;

BenchmarkRunner.Run<HashRingBenchmarks>();
BenchmarkRunner.Run<MemcachedKeysBatchingBenchmarks>();
BenchmarkRunner.Run<ParallelCyclesBenchmarks>();