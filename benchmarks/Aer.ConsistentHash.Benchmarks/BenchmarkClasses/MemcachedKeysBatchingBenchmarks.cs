using Aer.ConsistentHash.Benchmarks.Model;
using Aer.Memcached.Client;
using Aer.Memcached.Client.Authentication;
using Aer.Memcached.Client.Commands;
using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;
using Aer.Memcached.Client.Serializers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoreLinq;

namespace Aer.ConsistentHash.Benchmarks.BenchmarkClasses;

[HardwareCounters(HardwareCounter.Timer)]
[MemoryDiagnoser(displayGenColumns: true)]
public class MemcachedKeysBatchingBenchmarks
{
	private IMemcachedClient _memcachedClient;
	private HashRing<Node> _nodeLocator;
	private CommandExecutor<Node> _commandExecutor;
	private BinarySerializer _binarySerializer;
	
	private readonly Dictionary<string, string> _keyValues = new();
	
	private const int TEST_KEYS_COUNT = 20_000;
	private const int ONE_COMMAND_KEYS_COUNT = 1000;
	private const int ONE_COMMAND_AUTO_BATCH_SIZE = 20;

	[GlobalSetup]
	public void Setup()
	{
		var hashCalculator = new HashCalculator();
		_nodeLocator = new HashRing<Node>(hashCalculator);
		
		_nodeLocator.AddNodes(
			new Node[]
			{
				new()
				{
					IpAddress = "127.0.0.1"
				}
			});

		using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
		var commandExecutorLogger = loggerFactory.CreateLogger<CommandExecutor<Node>>();

		var config = new MemcachedConfiguration(){
			BinarySerializerType = ObjectBinarySerializerType.Bson
		};
		
		var authProvider = new DefaultAuthenticationProvider(
			new OptionsWrapper<MemcachedConfiguration.AuthenticationCredentials>(config.MemcachedAuth));
		
		var expirationCalculator = new ExpirationCalculator(hashCalculator, new OptionsWrapper<MemcachedConfiguration>(config));

		_commandExecutor = new CommandExecutor<Node>(
			new OptionsWrapper<MemcachedConfiguration>(config),
			authProvider,
			commandExecutorLogger,
			_nodeLocator);

		_binarySerializer = new BinarySerializer(
			new ObjectBinarySerializerFactory(
				new OptionsWrapper<MemcachedConfiguration>(config),
				// TODO: add custom serializer support
				serviceProvider: null
			)
		);

		_memcachedClient = new MemcachedClient<Node>(
			_nodeLocator,
			_commandExecutor,
			expirationCalculator,
			cacheSynchronizer: null,
			_binarySerializer	
		);

		foreach (var key in Enumerable.Range(0, TEST_KEYS_COUNT))
		{ 
			_keyValues.Add(key.ToString(), Guid.NewGuid().ToString());		
		}
	}
	
	[IterationSetup]
	public void SetupBenchmark()
	{
		_memcachedClient.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
		foreach (var keyValueBatch in _keyValues.Batch(ONE_COMMAND_KEYS_COUNT))
		{
			var valuesToInsert = keyValueBatch.ToDictionary(kv => kv.Key, kv => kv.Value);
			_memcachedClient.MultiStoreAsync(valuesToInsert, TimeSpan.FromMinutes(30), CancellationToken.None).GetAwaiter().GetResult();
		}
	}

	[Benchmark]
	public async Task GetValuesNonBatched()
	{
		await Parallel.ForEachAsync(
			_keyValues.Keys.Batch(ONE_COMMAND_KEYS_COUNT),
			new ParallelOptions()
			{
				MaxDegreeOfParallelism = Environment.ProcessorCount
			},
			async (keysBatch, _) =>
			{
				await _memcachedClient.MultiGetAsync<string>(keysBatch, CancellationToken.None);
			});
	}

	[Benchmark]
	public async Task GetValuesBatchedParallelBoundedForEach()
	{
		await Parallel.ForEachAsync(
			_keyValues.Keys.Batch(ONE_COMMAND_KEYS_COUNT),
			new ParallelOptions()
			{
				MaxDegreeOfParallelism = Environment.ProcessorCount
			},
			async (keysBatch, _) =>
			{
				await _memcachedClient.MultiGetAsync<string>(
					keysBatch,
					CancellationToken.None,
					new BatchingOptions()
					{
						BatchSize = ONE_COMMAND_AUTO_BATCH_SIZE,
						MaxDegreeOfParallelism = Environment.ProcessorCount
					});
			});
	}

	[Benchmark]
	public async Task GetValuesBatchedParallelTasks()
	{
		await Parallel.ForEachAsync(
			_keyValues.Keys.Batch(ONE_COMMAND_KEYS_COUNT),
			new ParallelOptions()
			{
				MaxDegreeOfParallelism = Environment.ProcessorCount
			},
			async (keysBatch, _) =>
			{
				await MultiGetParallelTasksAsync<string>(keysBatch, ONE_COMMAND_AUTO_BATCH_SIZE);
			});
	}

	/// <summary>
	/// To avoid implementing benchmark-only method in <see cref="IMemcachedClient"/> it is implemented here ad-hoc.
	/// </summary>
	private async Task<Dictionary<string, T>> MultiGetParallelTasksAsync<T>(
		IEnumerable<string> keys,
		int batchSize)
	{
		var nodes = _nodeLocator.GetNodes(keys, replicationFactor: 0);
		if (nodes.Keys.Count == 0)
		{
			return new Dictionary<string, T>();
		}

		var getTasks = new List<Task>(nodes.Count);
		var taskToCommands = new List<(Task<CommandExecutionResult> task, MultiGetCommand command)>(nodes.Count);
		var commandsToDispose = new List<MemcachedCommandBase>(nodes.Count);
		
		foreach (var (node, keysToGet) in nodes)
		{
			foreach (var keysBatch in keysToGet.Batch(batchSize))
			{
				var command = new MultiGetCommand(keysBatch, batchSize);
				var executeTask = _commandExecutor.ExecuteCommandAsync(node, command, CancellationToken.None);

				getTasks.Add(executeTask);
				taskToCommands.Add((executeTask, command));
				commandsToDispose.Add(command);
			}
		}

		await Task.WhenAll(getTasks);

		var result = new Dictionary<string, T>();
		foreach (var taskToCommand in taskToCommands)
		{
			var taskResult = await taskToCommand.task;
			if (!taskResult.Success)
			{
				continue;
			}

			foreach (var item in taskToCommand.command.Result)
			{
				var key = item.Key;
				var cacheItem = item.Value;

				result[key] = _binarySerializer.Deserialize<T>(cacheItem).Result;
			}
		}

		// dispose only after deserialization is done and allocated memory from array pool can be returned
		foreach (var getCommand in commandsToDispose)
		{
			getCommand.Dispose();
		}

		return result;
	}
}
