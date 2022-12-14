using BenchmarkDotNet.Running;
using Aer.ConsistentHash.Benchmarks;

BenchmarkRunner.Run<HashRingBenchmarks>();
BenchmarkRunner.Run<MemcachedKeysBatchingBenchmarks>();
BenchmarkRunner.Run<ParallelCyclesBenchmarks>();