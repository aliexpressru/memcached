using System.Net;
using Aer.Memcached.Client.Commands;
using Aer.Memcached.Client.ConnectionPool;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Aer.Memcached.Tests.TestClasses;

/// <summary>
/// Tests for async command execution to verify that commands properly timeout
/// and don't hang indefinitely when memcached server is slow or unresponsive.
/// </summary>
[TestClass]
public class AsyncCommandExecutionTests
{
    private ILogger _loggerMock;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = Substitute.For<ILogger>();
    }

    [TestMethod]
    [TestCategory("AsyncCommands")]
    public async Task GetCommand_ReadResponseAsync_ShouldRespectTimeout()
    {
        // Arrange
        var receiveTimeout = TimeSpan.FromMilliseconds(100);
        var connectionTimeout = TimeSpan.FromSeconds(5);
        
        // Create a server that accepts connections but never sends response
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = listener.LocalEndpoint;

        // Accept connection but don't send any data
        _ = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            // Don't send any data, just keep connection open
            await Task.Delay(TimeSpan.FromSeconds(10));
            client.Close();
        });

        var socket = new PooledSocket(
            serverEndpoint,
            connectionTimeout,
            receiveTimeout,
            _loggerMock);

        await socket.ConnectAsync(CancellationToken.None);

        var command = new GetCommand("test-key", isAllowLongKeys: false);

        try
        {
            // Act - try to read response, should timeout
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            var exception = await Assert.ThrowsExceptionAsync<TimeoutException>(
                async () => await command.ReadResponseAsync(socket, CancellationToken.None));

            sw.Stop();

            // Assert
            exception.Message.Should().Contain("timed out");
            
            // Verify it timed out within expected window (receiveTimeout + small buffer)
            sw.ElapsedMilliseconds.Should().BeLessThan(
                (long)(receiveTimeout.TotalMilliseconds * 2),
                "Command should timeout close to the configured ReceiveTimeout");
            
            sw.ElapsedMilliseconds.Should().BeGreaterThan(
                (long)(receiveTimeout.TotalMilliseconds * 0.8),
                "Command should wait at least close to the timeout before failing");
        }
        finally
        {
            command.Dispose();
            socket.Destroy();
            listener.Stop();
        }
    }

    [TestMethod]
    [TestCategory("AsyncCommands")]
    public async Task MultiGetCommand_ReadResponseAsync_ShouldTimeoutOnSlowServer()
    {
        // Arrange
        var receiveTimeout = TimeSpan.FromMilliseconds(200);
        var connectionTimeout = TimeSpan.FromSeconds(5);
        
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = listener.LocalEndpoint;

        _ = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            // Simulate slow server - don't respond
            await Task.Delay(TimeSpan.FromSeconds(10));
            client.Close();
        });

        var socket = new PooledSocket(
            serverEndpoint,
            connectionTimeout,
            receiveTimeout,
            _loggerMock);

        await socket.ConnectAsync(CancellationToken.None);

        var keys = new[] { "key1", "key2", "key3" };
        var command = new MultiGetCommand(keys, keys.Length, isAllowLongKeys: false);

        try
        {
            // Act - MultiGet should also respect timeout
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            var exception = await Assert.ThrowsExceptionAsync<TimeoutException>(
                async () => await command.ReadResponseAsync(socket, CancellationToken.None));

            sw.Stop();

            // Assert
            exception.Message.Should().Contain("timed out");
            sw.ElapsedMilliseconds.Should().BeLessThan(
                (long)(receiveTimeout.TotalMilliseconds * 2),
                "MultiGet command should timeout within expected window");
        }
        finally
        {
            command.Dispose();
            socket.Destroy();
            listener.Stop();
        }
    }

    [TestMethod]
    [TestCategory("AsyncCommands")]
    public async Task FlushCommand_ReadResponseAsync_ShouldHandleTimeout()
    {
        // Arrange
        var receiveTimeout = TimeSpan.FromMilliseconds(150);
        var connectionTimeout = TimeSpan.FromSeconds(5);
        
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = listener.LocalEndpoint;

        _ = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            await Task.Delay(TimeSpan.FromSeconds(10));
            client.Close();
        });

        var socket = new PooledSocket(
            serverEndpoint,
            connectionTimeout,
            receiveTimeout,
            _loggerMock);

        await socket.ConnectAsync(CancellationToken.None);

        var command = new FlushCommand();

        try
        {
            // Act
            var exception = await Assert.ThrowsExceptionAsync<TimeoutException>(
                async () => await command.ReadResponseAsync(socket, CancellationToken.None));

            // Assert
            exception.Message.Should().Contain("timed out");
        }
        finally
        {
            command.Dispose();
            socket.Destroy();
            listener.Stop();
        }
    }

    [TestMethod]
    [TestCategory("AsyncCommands")]
    [TestCategory("Parallel")]
    public async Task ParallelCommands_WithTimeouts_ShouldNotAccumulateTime()
    {
        // This test simulates the original issue: parallel GetQ commands hanging for 5.5s
        // With proper timeouts, they should all fail quickly and independently
        
        // Arrange
        var receiveTimeout = TimeSpan.FromMilliseconds(100);
        var connectionTimeout = TimeSpan.FromSeconds(5);
        var commandCount = 5;
        
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = listener.LocalEndpoint;

        // Accept connections but never respond
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < commandCount; i++)
            {
                var client = await listener.AcceptTcpClientAsync();
                // Keep connection open but don't send data
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    client.Close();
                });
            }
        });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var tasks = new List<Task>();

        // Act - Execute multiple commands in parallel
        for (int i = 0; i < commandCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var socket = new PooledSocket(
                    serverEndpoint,
                    connectionTimeout,
                    receiveTimeout,
                    _loggerMock);

                try
                {
                    await socket.ConnectAsync(CancellationToken.None);

                    var command = new GetCommand($"key-{i}", isAllowLongKeys: false);
                    
                    try
                    {
                        await command.ReadResponseAsync(socket, CancellationToken.None);
                    }
                    catch (TimeoutException)
                    {
                        // Expected
                    }
                    finally
                    {
                        command.Dispose();
                    }
                }
                finally
                {
                    socket.Destroy();
                }
            }));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert - Total time should be close to single timeout, not accumulated
        var maxExpectedTime = receiveTimeout.TotalMilliseconds * 3; // Buffer for parallel execution
        sw.ElapsedMilliseconds.Should().BeLessThan((long)maxExpectedTime,
            $"Parallel commands should timeout independently, not accumulate. " +
            $"Expected < {maxExpectedTime}ms for {commandCount} parallel commands, " +
            $"but got {sw.ElapsedMilliseconds}ms. " +
            $"This was the original issue - commands hanging for 5.5s instead of timing out quickly.");

        listener.Stop();
    }

    [TestMethod]
    [TestCategory("AsyncCommands")]
    public async Task Command_WithCancellationToken_ShouldCancelBeforeTimeout()
    {
        // Arrange
        var receiveTimeout = TimeSpan.FromSeconds(10); // Long timeout
        var connectionTimeout = TimeSpan.FromSeconds(5);
        var cancellationDelay = TimeSpan.FromMilliseconds(100);
        
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = listener.LocalEndpoint;

        _ = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            await Task.Delay(TimeSpan.FromSeconds(20));
            client.Close();
        });

        var socket = new PooledSocket(
            serverEndpoint,
            connectionTimeout,
            receiveTimeout,
            _loggerMock);

        await socket.ConnectAsync(CancellationToken.None);

        var command = new GetCommand("test-key", isAllowLongKeys: false);
        var cts = new CancellationTokenSource();

        try
        {
            // Act - Cancel before timeout
            cts.CancelAfter(cancellationDelay);
            
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            var exception = await Assert.ThrowsExceptionAsync<TaskCanceledException>(
                async () => await command.ReadResponseAsync(socket, cts.Token));

            sw.Stop();
            
            exception.Should().BeAssignableTo<OperationCanceledException>();

            // Assert - Should cancel much faster than timeout
            sw.ElapsedMilliseconds.Should().BeLessThan(
                (long)(receiveTimeout.TotalMilliseconds * 0.5),
                "Cancellation should happen before timeout");
        }
        finally
        {
            command.Dispose();
            socket.Destroy();
            listener.Stop();
            cts.Dispose();
        }
    }

    [TestMethod]
    [TestCategory("AsyncCommands")]
    [TestCategory("Clone")]
    public void GetCommand_Clone_ShouldCreateIndependentInstance()
    {
        // Arrange
        var originalCommand = new GetCommand("test-key", isAllowLongKeys: true);

        // Act
        var clonedCommand = originalCommand.Clone();

        // Assert
        clonedCommand.Should().NotBeSameAs(originalCommand);
        clonedCommand.Should().BeOfType<GetCommand>();
        
        // Both should be independent - used for parallel execution
        originalCommand.Dispose();
        
        // Cloned command should still be usable
        Action useClone = () => clonedCommand.GetBuffer();
        useClone.Should().NotThrow();
        
        clonedCommand.Dispose();
    }
}


