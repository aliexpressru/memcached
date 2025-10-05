﻿using System.Diagnostics;
using Aer.ConsistentHash;
using Aer.Memcached.Client;
using Aer.Memcached.Client.Authentication;
using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Diagnostics;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Serializers;
using Aer.Memcached.Diagnostics.Listeners;
using Aer.Memcached.Tests.Infrastructure;
using AutoFixture;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Tests.Base;

public abstract class MemcachedClientTestsBase
{
	protected const int CacheItemExpirationSeconds = 3;
	
	protected readonly MemcachedClient<Pod> Client;
	
	protected readonly Fixture Fixture;

	protected readonly ObjectBinarySerializerType BinarySerializerType;

	protected readonly ServiceProvider ServiceProvider;
	
	protected MemcachedClientTestsBase(
		bool isSingleNodeCluster, 
		ObjectBinarySerializerType binarySerializerType = ObjectBinarySerializerType.Bson,
		bool isAllowLongKeys = false)
	{
		BinarySerializerType = binarySerializerType;
		
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
		var memcachedClientLogger = loggerFactory.CreateLogger<MemcachedClient<Pod>>();
		
		var config = new MemcachedConfiguration()
		{
			Diagnostics = new MemcachedConfiguration.MemcachedDiagnosticsSettings()
			{
				DisableDiagnostics = true,
				DisableRebuildNodesStateLogging = true,
				DisableSocketPoolDiagnosticsLogging = false,
				SocketPoolDiagnosticsLoggingEventLevel = LogLevel.Information
			},
			BinarySerializerType = binarySerializerType,
			IsAllowLongKeys = isAllowLongKeys,
            SyncSettings = new (){
                SyncEndpointsAuthAllowAnonymous = false
            }
		};
		
		var authProvider = new DefaultAuthenticationProvider(
			new OptionsWrapper<MemcachedConfiguration.AuthenticationCredentials>(config.MemcachedAuth));

		var configWrapper = new OptionsWrapper<MemcachedConfiguration>(config);
		
		var expirationCalculator = new ExpirationCalculator(hashCalculator, configWrapper);
		
		// add test custom binary serializer
		
		ServiceCollection sc = new ServiceCollection();
		
		sc.AddSingleton<IObjectBinarySerializer, TestObjectBinarySerializer>();
		ServiceProvider = sc.BuildServiceProvider();
		
		Client = new MemcachedClient<Pod>(
			nodeLocator,
			new CommandExecutor<Pod>(
				configWrapper,
				authProvider,
				commandExecutorLogger,
				nodeLocator),
			expirationCalculator,
			null,
			new BinarySerializer(
				new ObjectBinarySerializerFactory(
					configWrapper,
					ServiceProvider
				)
			),
			memcachedClientLogger,
			configWrapper
		);

		Fixture = new Fixture();

		DiagnosticListener diagnosticSource = MemcachedDiagnosticSource.Instance;

		IOptions<MemcachedConfiguration> memcachedOptions = new OptionsWrapper<MemcachedConfiguration>(config);

		var loggingListener = new LoggingMemcachedDiagnosticListener(
			loggerFactory.CreateLogger<LoggingMemcachedDiagnosticListener>(),
			memcachedOptions);
		
		diagnosticSource.SubscribeWithAdapter(loggingListener);
	}

	protected string GetTooLongKey()
	{
		var uniquePart = Guid.NewGuid().ToString();

		var tooLongKey =
			uniquePart
			+
			new string(
				'*',
				// this length is 1 byte too long to be stored
				MemcachedCommandBase.MemcachedKeyLengthMaxLengthBytes - uniquePart.Length + 1);

		return tooLongKey;
	}
}
