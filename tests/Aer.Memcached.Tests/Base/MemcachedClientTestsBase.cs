using Aer.ConsistentHash;
using Aer.Memcached.Client;
using Aer.Memcached.Client.Authentication;
using Aer.Memcached.Client.Config;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Aer.Memcached.Tests.Base;

public abstract class MemcachedClientTestsBase
{
	protected readonly MemcachedClient<Pod> Client;
	protected readonly Fixture Fixture;

	protected const int CacheItemExpirationSeconds = 3;
	
	protected MemcachedClientTestsBase(bool isSingleNodeCluster)
	{
		var hashCalculator = new HashCalculator();
		
		var nodeLocator = new HashRing<Pod>(hashCalculator);

		if (isSingleNodeCluster)
		{
			nodeLocator.AddNodes(
				new Pod("localhost", 11211)
			);
		}
		else
		{
			// means we are building multi-node cluster
			nodeLocator.AddNodes(
				new Pod("localhost", 11211),
				new Pod("localhost", 11212),
				new Pod("localhost", 11213)
			);
		}

		using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
		
		var commandExecutorLogger = loggerFactory.CreateLogger<CommandExecutor<Pod>>();
		var config = new MemcachedConfiguration();
		var authProvider = new DefaultAuthenticationProvider(
			new OptionsWrapper<MemcachedConfiguration.AuthenticationCredentials>(config.MemcachedAuth));

		Client = new MemcachedClient<Pod>(
			nodeLocator,
			new CommandExecutor<Pod>(
				new OptionsWrapper<MemcachedConfiguration>(config),
				authProvider,
				commandExecutorLogger,
				nodeLocator)
		);

		Fixture = new Fixture();

		Memcached.Client.Commands.Infrastructure.BinaryConverter.Serializer = JsonSerializer.Create(
			new JsonSerializerSettings
			{
				Converters = new List<JsonConverter>(new[] {new StringEnumConverter()}),
				NullValueHandling = NullValueHandling.Ignore,
				ContractResolver = new DefaultContractResolver
				{
					NamingStrategy = new SnakeCaseNamingStrategy()
				},
				ReferenceLoopHandling = ReferenceLoopHandling.Ignore
			}); 
	}
}
