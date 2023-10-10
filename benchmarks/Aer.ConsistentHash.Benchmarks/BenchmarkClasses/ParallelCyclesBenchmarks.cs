using BenchmarkDotNet.Attributes;

namespace Aer.ConsistentHash.Benchmarks.BenchmarkClasses;

[MemoryDiagnoser(displayGenColumns: true)]
public class ParallelCyclesBenchmarks
{
	[Params(-1, 2, 8, 16, 32, 64, 100)]
	public int MaxDegreeOfParallelism { set; get; }

	private static List<int> _externalCollection = Enumerable.Range(0, 100).ToList();
	private static List<int> _internalCollection = Enumerable.Range(0, 50).ToList();

	[Benchmark]
	public async Task NestedParallelForEachAsync()
	{
		await Parallel.ForEachAsync(
			_externalCollection,
			new ParallelOptions()
			{
				MaxDegreeOfParallelism = MaxDegreeOfParallelism
			},
			async (_, _) =>
			{
				await Parallel.ForEachAsync(
					_internalCollection,
					new ParallelOptions()
					{
						MaxDegreeOfParallelism = MaxDegreeOfParallelism
					},
					async (_, _) =>
					{
						// to simulate continuation on parallel thread
						await Task.Yield();
						
						// simulate some CPU work;
						int i = 1_000_000;
						while (i > 0)
						{
							i--;
						}
					});
			});
	}

	[Benchmark]
	public async Task ExternalLoopParallelInternalSequential()
	{
		await Parallel.ForEachAsync(
			_externalCollection,
			new ParallelOptions()
			{
				MaxDegreeOfParallelism = MaxDegreeOfParallelism
			},
			async (_, _) =>
			{
				foreach (var v in _internalCollection)
				{
					// to simulate continuation on parallel thread
					await Task.Yield();

					// simulate some CPU work;
					int i = 1_000_000;
					while (i > 0)
					{
						i--;
					}
				}
			});
	}
}
