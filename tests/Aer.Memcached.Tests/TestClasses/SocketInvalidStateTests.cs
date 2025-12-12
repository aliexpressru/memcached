using System.Net;
using System.Diagnostics;
using System.Net.Sockets;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.ConnectionPool;
using Aer.Memcached.Client.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Aer.Memcached.Tests.TestClasses;

/// <summary>
/// Tests to reproduce the issue where socket is left in invalid state after timeout/exception
/// and subsequent commands fail with magic byte mismatch.
/// 
/// Based on errors:
/// 1. Command times out during read
/// 2. Socket has unread data (e.g., 18848 bytes)
/// 3. Next command on same socket fails with "Expected magic value 129, received: 112"
/// </summary>
[TestClass]
public class SocketInvalidStateTests
{
    private ILogger<SocketPool> _poolLogger;
    private List<KeyValuePair<string, object>> _diagnosticEvents;
    private IDisposable _diagnosticSubscription;

    [TestInitialize]
    public void Setup()
    {
        _poolLogger = Substitute.For<ILogger<SocketPool>>();
        _diagnosticEvents = new List<KeyValuePair<string, object>>();
        
        // Subscribe to diagnostic events
        _diagnosticSubscription = DiagnosticListener.AllListeners.Subscribe(
            new DiagnosticListenerObserver(listener =>
            {
                if (listener.Name == "Aer.Diagnostics.Memcached")
                {
                    listener.Subscribe(new Observer<KeyValuePair<string, object>>(_diagnosticEvents));
                }
            }));
    }

    [TestCleanup]
    public void Cleanup()
    {
        _diagnosticSubscription?.Dispose();
    }
    
    private class DiagnosticListenerObserver : IObserver<DiagnosticListener>
    {
        private readonly Action<DiagnosticListener> _onNext;

        public DiagnosticListenerObserver(Action<DiagnosticListener> onNext)
        {
            _onNext = onNext;
        }

        public void OnNext(DiagnosticListener value) => _onNext(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }
    
    private class Observer<T> : IObserver<T>
    {
        private readonly List<T> _events;

        public Observer(List<T> events)
        {
            _events = events;
        }

        public void OnNext(T value) => _events.Add(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    [TestMethod]
    [TestCategory("SocketState")]
    public async Task Socket_ShouldNotBeReused_AfterTimeoutDuringRead()
    {
        // Arrange - create a fake server that responds slowly
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = (IPEndPoint)listener.LocalEndpoint;

        // Simulate a server that accepts connection and sends partial response
        _ = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();

            // Read the request - binary protocol header is 24 bytes
            var requestBuffer = new byte[24];
            await stream.ReadExactlyAsync(requestBuffer, CancellationToken.None);

            // Send partial response and then delay (simulating slow/stuck server)
            // This creates a scenario where client times out with unread data in socket
            var partialResponse = new byte[] { 0x81, 0x00, 0x00, 0x00 }; // Binary protocol header start
            await stream.WriteAsync(partialResponse);
            await stream.FlushAsync();

            // Delay to cause timeout on client side
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Send rest of response (will remain unread after timeout)
            var remainingData = new byte[100];
            await stream.WriteAsync(remainingData);
            
            client.Close();
        });

        var config = new MemcachedConfiguration.SocketPoolConfiguration
        {
            ConnectionTimeout = TimeSpan.FromSeconds(2),
            ReceiveTimeout = TimeSpan.FromMilliseconds(100), // Short timeout to trigger issue
            MaxPoolSize = 5,
            SocketPoolingTimeout = TimeSpan.FromSeconds(1)
        };

        var pool = new SocketPool(serverEndpoint, config, _poolLogger);

        try
        {
            // Act 1 - Get socket and cause timeout during read
            var socket1 = await pool.GetSocketAsync(CancellationToken.None);
            socket1.Should().NotBeNull();

            // Send a request
            var request = new byte[] { 0x80, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00 };
            await socket1.WriteAsync(new[] { new ArraySegment<byte>(request) });

            // Try to read response - should timeout
            var buffer = new byte[1024];
            await Assert.ThrowsExceptionAsync<TimeoutException>(async () =>
            {
                await socket1.ReadAsync(buffer.AsMemory(), 1024, CancellationToken.None);
            });

            // Verify socket is marked for destruction
            socket1.ShouldDestroySocket.Should().BeTrue("Socket should be marked for destruction after timeout");

            // Verify that timeout was logged
            _poolLogger.Received().Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(v => v.ToString().Contains("timed out")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());

            var socket1Id = socket1.InstanceId;

            // Act 2 - Return socket to pool (this is what happens with 'using' statement)
            socket1.Dispose(); // Returns to pool or destroys based on ShouldDestroySocket

            // Act 3 - Try to get another socket from pool
            var socket2 = await pool.GetSocketAsync(CancellationToken.None);
            socket2.Should().NotBeNull();

            // Assert - socket2 should be a NEW socket, not the corrupted one
            socket2.InstanceId.Should().NotBe(socket1Id, 
                "Pool should not return the same socket that timed out");
        }
        finally
        {
            pool.Dispose();
            listener.Stop();
        }
    }

    [TestMethod]
    public async Task SocketWithUnreadData_ShouldBeDetectedByReset()
    {
        // This test verifies that when socket has unread data, the ResetAsync detects it
        // After the fix, socket with unread data should be destroyed, not returned to pool

        // Arrange
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = (IPEndPoint)listener.LocalEndpoint;

        // Server that sends data
        _ = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();

            // Read request - binary protocol header is 24 bytes
            var buffer = new byte[24];
            await stream.ReadExactlyAsync(buffer, CancellationToken.None);

            // Send response that won't be fully read
            var response = new byte[1000];
            response[0] = 0x81; // Binary protocol magic byte
            await stream.WriteAsync(response);
            await stream.FlushAsync();

            await Task.Delay(TimeSpan.FromSeconds(2));
            client.Close();
        });

        var config = new MemcachedConfiguration.SocketPoolConfiguration
        {
            ConnectionTimeout = TimeSpan.FromSeconds(2),
            ReceiveTimeout = TimeSpan.FromSeconds(1),
            MaxPoolSize = 5,
            SocketPoolingTimeout = TimeSpan.FromSeconds(1)
        };

        var pool = new SocketPool(serverEndpoint, config, _poolLogger);

        try
        {
            // Act
            var socket1 = await pool.GetSocketAsync(CancellationToken.None);
            socket1.Should().NotBeNull();
            var socket1Id = socket1.InstanceId;

            // Send full binary protocol request header (24 bytes)
            // Format: magic(1) + opcode(1) + keylen(2) + extlen(1) + datatype(1) + status/reserved(2) + bodylen(4) + opaque(4) + cas(8) = 24 bytes
            var request = new byte[24];
            request[0] = 0x80; // Request magic
            request[1] = 0x00; // Get opcode
            // Rest zeros
            await socket1.WriteAsync(new[] { new ArraySegment<byte>(request) });

            // Wait for server to send response
            await Task.Delay(200);

            // Socket should have unread data
            var availableBytes = socket1.Socket.Available;
            availableBytes.Should().BeGreaterThan(0, "Socket should have unread data");

            // Act - Return socket to pool
            socket1.Dispose();

            // Allow reset to complete
            await Task.Delay(200);

            // Act - Get another socket from pool
            var socket2 = await pool.GetSocketAsync(CancellationToken.None);
            socket2.Should().NotBeNull();

            // Assert - Check that diagnostic event for unread data was emitted
            var unreadDataEvents = _diagnosticEvents
                .Where(e => e.Key == MemcachedDiagnosticSource.SocketUnreadDataDetectedDiagnosticName)
                .ToList();
            
            unreadDataEvents.Should().HaveCount(1, "Should have detected unread data once");
        }
        finally
        {
            pool.Dispose();
            listener.Stop();
        }
    }

    [TestMethod]
    [TestCategory("SocketState")]
    public async Task Socket_AfterIOException_ShouldBeMarkedForDestruction()
    {
        // Arrange - create a server that closes connection abruptly
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = (IPEndPoint)listener.LocalEndpoint;

        var serverTask = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            // Close immediately to cause IOException
            client.Close();
        });

        var config = new MemcachedConfiguration.SocketPoolConfiguration
        {
            ConnectionTimeout = TimeSpan.FromSeconds(2),
            ReceiveTimeout = TimeSpan.FromSeconds(1),
            MaxPoolSize = 5,
            SocketPoolingTimeout = TimeSpan.FromSeconds(1)
        };

        var pool = new SocketPool(serverEndpoint, config, _poolLogger);

        try
        {
            // Act
            var socket = await pool.GetSocketAsync(CancellationToken.None);
            Assert.IsNotNull(socket);
            Assert.IsFalse(socket.ShouldDestroySocket);

            await serverTask;
            await Task.Delay(200);

            // Try to read from closed connection - should fail with IOException/SocketException
            var buffer = new byte[24];
            try
            {
                await socket.ReadAsync(buffer.AsMemory(), 24, CancellationToken.None);
                Assert.Fail("Should have thrown exception");
            }
            catch (IOException)
            {
                // Expected - IOException when reading from closed socket
            }

            // Assert - socket should be marked for destruction
            Assert.IsTrue(socket.ShouldDestroySocket, 
                "Socket should be marked for destruction after IOException");

            // Cleanup
            socket.Dispose();
        }
        finally
        {
            pool.Dispose();
            listener.Stop();
        }
    }

    [TestMethod]
    [TestCategory("SocketState")]
    public async Task Socket_AfterSuccessfulOperation_ShouldNotBeMarkedForDestruction()
    {
        // Arrange - create a well-behaving server
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = (IPEndPoint)listener.LocalEndpoint;

        var serverTask = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();

            // Read request
            var buffer = new byte[24];
            await stream.ReadExactlyAsync(buffer, CancellationToken.None);

            // Send proper response
            var response = new byte[24];
            response[0] = 0x81; // Response magic
            await stream.WriteAsync(response);
            await stream.FlushAsync();

            await Task.Delay(500);
            client.Close();
        });

        var config = new MemcachedConfiguration.SocketPoolConfiguration
        {
            ConnectionTimeout = TimeSpan.FromSeconds(2),
            ReceiveTimeout = TimeSpan.FromSeconds(1),
            MaxPoolSize = 5,
            SocketPoolingTimeout = TimeSpan.FromSeconds(1)
        };

        var pool = new SocketPool(serverEndpoint, config, _poolLogger);

        try
        {
            // Act
            var socket = await pool.GetSocketAsync(CancellationToken.None);
            Assert.IsNotNull(socket);
            
            // Send request
            var request = new byte[24];
            request[0] = 0x80;
            await socket.WriteAsync(new[] { new ArraySegment<byte>(request) });

            // Read response
            var responseBuffer = new byte[24];
            await socket.ReadAsync(responseBuffer.AsMemory(), 24, CancellationToken.None);

            // Assert - socket should be in good state
            Assert.IsFalse(socket.ShouldDestroySocket, 
                "Socket should NOT be marked for destruction after successful operation");

            var socketId = socket.InstanceId;

            // Return to pool
            socket.Dispose();
            await Task.Delay(100);

            // Get socket again - should reuse same one
            var socket2 = await pool.GetSocketAsync(CancellationToken.None);
            Assert.AreEqual(socketId, socket2.InstanceId, 
                "Should reuse same socket after successful operation");

            // Cleanup
            socket2.Dispose();
        }
        finally
        {
            pool.Dispose();
            listener.Stop();
        }
    }

    [TestMethod]
    [TestCategory("SocketState")]
    public async Task ResetAsync_WithUnreadData_ShouldLogError()
    {
        // Arrange
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = (IPEndPoint)listener.LocalEndpoint;

        var serverTask = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();

            // Send data without waiting for request
            var data = new byte[500];
            await stream.WriteAsync(data);
            await stream.FlushAsync();

            await Task.Delay(1000);
            client.Close();
        });

        var config = new MemcachedConfiguration.SocketPoolConfiguration
        {
            ConnectionTimeout = TimeSpan.FromSeconds(2),
            ReceiveTimeout = TimeSpan.FromSeconds(1),
            MaxPoolSize = 5,
            SocketPoolingTimeout = TimeSpan.FromSeconds(1)
        };

        var pool = new SocketPool(serverEndpoint, config, _poolLogger);

        try
        {
            // Act
            var socket = await pool.GetSocketAsync(CancellationToken.None);
            Assert.IsNotNull(socket);

            // Wait for server to send data
            await Task.Delay(200);

            // Socket should have unread data
            Assert.IsTrue(socket.Socket.Available > 0);

            // Act - call ResetAsync to clear unread data
            await socket.ResetAsync(CancellationToken.None);

            // Assert - Check that diagnostic event for unread data was emitted
            var unreadDataEvents = _diagnosticEvents
                .Where(e => e.Key == MemcachedDiagnosticSource.SocketUnreadDataDetectedDiagnosticName)
                .ToList();
            
            unreadDataEvents.Should().HaveCount(1, "Should have detected unread data once");

            // Cleanup
            socket.Dispose();
        }
        finally
        {
            pool.Dispose();
            listener.Stop();
        }
    }

    [TestMethod]
    [TestCategory("SocketState")]
    public async Task ConcurrentTimeouts_ShouldNotCausePoolCorruption()
    {
        // This test simulates multiple concurrent operations timing out
        // and verifies that pool state remains consistent

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = (IPEndPoint)listener.LocalEndpoint;

        // Server that accepts connections but responds slowly
        var serverTask = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var stream = client.GetStream();
                            var buffer = new byte[1024];
                            await stream.ReadAsync(buffer, 0, buffer.Length);
                            // Don't send response - cause timeout
                            await Task.Delay(5000);
                        }
                        catch
                        {
                            // Ignore
                        }
                        finally
                        {
                            client.Close();
                        }
                    });
                }
            }
            catch
            {
                // Listener stopped
            }
        });

        var config = new MemcachedConfiguration.SocketPoolConfiguration
        {
            ConnectionTimeout = TimeSpan.FromSeconds(2),
            ReceiveTimeout = TimeSpan.FromMilliseconds(100),
            MaxPoolSize = 5,
            SocketPoolingTimeout = TimeSpan.FromSeconds(1)
        };

        var pool = new SocketPool(serverEndpoint, config, _poolLogger);

        try
        {
            // Act - multiple concurrent operations that will timeout
            var tasks = Enumerable.Range(0, 10).Select(async _ =>
            {
                try
                {
                    var socket = await pool.GetSocketAsync(CancellationToken.None);
                    if (socket == null) return;

                    try
                    {
                        var request = new byte[24];
                        request[0] = 0x80;
                        await socket.WriteAsync(new[] { new ArraySegment<byte>(request) });

                        var buffer = new byte[1024];
                        await socket.ReadAsync(buffer.AsMemory(), 1024, CancellationToken.None);
                    }
                    catch (TimeoutException)
                    {
                        // Expected
                    }
                    finally
                    {
                        socket.Dispose();
                    }
                }
                catch
                {
                    // Ignore errors in individual operations
                }
            }).ToList();

            await Task.WhenAll(tasks);
            await Task.Delay(200);

            // Assert - pool should be in consistent state
            Assert.IsTrue(pool.UsedSocketsCount >= 0, "Used count should not be negative");
            Assert.IsTrue(pool.PooledSocketsCount >= 0, "Pooled count should not be negative");
            Assert.IsTrue(pool.RemainingPoolCapacity <= config.MaxPoolSize, 
                "Remaining capacity should not exceed max");
            Assert.IsTrue(pool.RemainingPoolCapacity >= 0, 
                "Remaining capacity should not be negative");
        }
        finally
        {
            pool.Dispose();
            listener.Stop();
        }
    }

    [TestMethod]
    [TestCategory("SocketState")]
    public async Task DisposedSocket_ShouldNotBeReturnedToPool()
    {
        // Arrange
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = (IPEndPoint)listener.LocalEndpoint;

        _ = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    await Task.Delay(100);
                    client.Close();
                }
            }
            catch { }
        });

        var config = new MemcachedConfiguration.SocketPoolConfiguration
        {
            ConnectionTimeout = TimeSpan.FromSeconds(2),
            ReceiveTimeout = TimeSpan.FromSeconds(1),
            MaxPoolSize = 5,
            SocketPoolingTimeout = TimeSpan.FromSeconds(1)
        };

        var pool = new SocketPool(serverEndpoint, config, _poolLogger);

        try
        {
            // Act
            var socket = await pool.GetSocketAsync(CancellationToken.None);
            var socketId = socket.InstanceId;

            // Dispose socket multiple times
            socket.Dispose();
            socket.Dispose();
            socket.Dispose();

            await Task.Delay(100);

            // Assert - pool should have exactly 1 socket
            Assert.AreEqual(1, pool.PooledSocketsCount, 
                "Multiple Dispose calls should only return socket once");

            // Get socket again
            var socket2 = await pool.GetSocketAsync(CancellationToken.None);
            Assert.AreEqual(socketId, socket2.InstanceId);

            // Cleanup
            socket2.Dispose();
        }
        finally
        {
            pool.Dispose();
            listener.Stop();
        }
    }
}

