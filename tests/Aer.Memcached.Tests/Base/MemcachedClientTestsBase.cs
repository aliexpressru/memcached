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
	
	/// <summary>
	/// File-based lock to ensure expiration tests run sequentially across all test processes
	/// This prevents race conditions when running tests in parallel on different frameworks (net8.0, net10.0)
	/// Using file lock is cross-platform (works on Windows, Linux, macOS)
	/// </summary>
	private static readonly string LockFilePath = Path.Combine(Path.GetTempPath(), "memcached_expiration_test.lock");
	
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

	/// <summary>
	/// Acquires a file-based lock for cross-process synchronization of expiration tests.
	/// Returns a FileStream that should be disposed to release the lock.
	/// </summary>
	protected static FileStream AcquireExpirationTestLock()
	{
		const int maxRetries = 100;
		const int retryDelayMs = 200;
		
		// Retry logic in case file is being used by another process
		for (int i = 0; i < maxRetries; i++)
		{
			try
			{
				// FileShare.None ensures exclusive access across processes
				return new FileStream(
					LockFilePath,
					FileMode.OpenOrCreate,
					FileAccess.ReadWrite,
					FileShare.None,
					bufferSize: 1,
					FileOptions.DeleteOnClose);
			}
			catch (IOException)
			{
				// Another process holds the lock, wait and retry
				if (i < maxRetries - 1)
				{
					Thread.Sleep(retryDelayMs);
				}
				else
				{
					// Last attempt failed, throw with context
					throw new InvalidOperationException(
						$"Failed to acquire expiration test lock after {maxRetries} retries " +
						$"(waited {maxRetries * retryDelayMs}ms total). " +
						$"Lock file: {LockFilePath}");
				}
			}
		}
		
		// This should never be reached due to throw in catch block
		throw new InvalidOperationException("Unexpected: failed to acquire lock");
	}

	/// <summary>
	/// Acquires expiration test lock and flushes the cache to ensure clean state.
	/// Returns an AsyncLockHandle that should be disposed to release the lock.
	/// </summary>
	protected async Task<AsyncLockHandle> AcquireExpirationTestLockAndFlushAsync()
	{
		var lockFile = AcquireExpirationTestLock();
		try
		{
			await Client.FlushAsync(CancellationToken.None);
			return new AsyncLockHandle(lockFile);
		}
		catch
		{
			// If flush fails, release the lock
			lockFile.Dispose();
			throw;
		}
	}

	/// <summary>
	/// Wrapper for FileStream that implements IAsyncDisposable for proper async disposal.
	/// </summary>
	protected sealed class AsyncLockHandle : IAsyncDisposable, IDisposable
	{
		private readonly FileStream _lockFile;

		public AsyncLockHandle(FileStream lockFile)
		{
			_lockFile = lockFile ?? throw new ArgumentNullException(nameof(lockFile));
		}

		public ValueTask DisposeAsync()
		{
			_lockFile.Dispose();
			return ValueTask.CompletedTask;
		}

		public void Dispose()
		{
			_lockFile.Dispose();
		}
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
