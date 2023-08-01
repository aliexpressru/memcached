using Aer.ConsistentHash;

namespace Aer.Memcached.Client.Config;

public class MemcachedConfiguration
{
    /// <summary>
    /// List of servers with hosted memcached
    /// </summary>
    public Server[] Servers { get; set; }
    
    /// <summary>
    /// Headless service to lookup all the memcached ip addresses.
    /// Use either <see cref="MemcachedConfiguration.Servers"/> or <see cref="HeadlessServiceAddress"/>
    /// </summary>
    public string HeadlessServiceAddress { get; set; }
    
    /// <summary>
    /// Configuration of <see cref="ConnectionPool.SocketPool"/>
    /// </summary>
    public SocketPoolConfiguration SocketPool { get; set; } = SocketPoolConfiguration.DefaultConfiguration();

    /// <summary>
    /// Configuration of maintainer
    /// </summary>
    public MaintainerConfiguration MemcachedMaintainer { get; set; } = MaintainerConfiguration.DefaultConfiguration();
    
    public AuthenticationCredentials MemcachedAuth { get; set; }

    /// <summary>
    /// Internal workings diagnostics configuration.
    /// </summary>
    public MemcachedDiagnosticsSettings Diagnostics { get; set; } = new();

    /// <summary>
    /// Checks that either <see cref="HeadlessServiceAddress"/> or <see cref="Servers"/> are specified
    /// </summary>
    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(HeadlessServiceAddress) || (Servers != null && Servers.Length != 0);
    }

    /// <summary>
    /// Represents diagnostics settings such as logging settings.
    /// </summary>
    public class MemcachedDiagnosticsSettings
    {
        /// <summary>
        /// Determines whether the memcached node rebuild process should report current state details as logs.
        /// </summary>
        public bool DisableRebuildNodesStateLogging { set; get; } = false;

        /// <summary>
        /// Determines whether the memcached metrics should be written out.
        /// </summary>
        public bool DisableDiagnostics { get; set; } = false;
    }
    
    public class Server
    {
        public string IpAddress { get; set; }
        
        public int Port { get; set; } = 11211;
    }

    public class SocketPoolConfiguration
    {
        /// <summary>
        /// Amount of time after which the connection attempt will fail
        /// </summary>
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Amount of time after which receiving data from the socket will fail
        /// </summary>
        public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Amount of time to acquire socket from pool
        /// </summary>
        public TimeSpan SocketPoolingTimeout { get; set; } = TimeSpan.FromMilliseconds(150);

        /// <summary>
        /// Maximum amount of sockets per memcached instance in the socket pool
        /// </summary>
        public int MaxPoolSize { get; set; } = 100;

        public static SocketPoolConfiguration DefaultConfiguration()
        {
            return new SocketPoolConfiguration
            {
                ConnectionTimeout = TimeSpan.FromSeconds(1),
                ReceiveTimeout = TimeSpan.FromSeconds(1),
                SocketPoolingTimeout = TimeSpan.FromMilliseconds(150),
                MaxPoolSize = 100
            };
        }

        public void Validate()
        {
            if (MaxPoolSize <= 0)
            {
                throw new InvalidOperationException($"{nameof(MaxPoolSize)} must be grater than 0");
            }

            if (ConnectionTimeout <= TimeSpan.Zero)
            {
                throw new InvalidOperationException($"{nameof(ConnectionTimeout)} must be > TimeSpan.Zero");
            }
            
            if (ReceiveTimeout <= TimeSpan.Zero)
            {
                throw new InvalidOperationException($"{nameof(ReceiveTimeout)} must be > TimeSpan.Zero");
            }
        }
    }

    public class MaintainerConfiguration
    {
        /// <summary>
        /// Period to rebuild nodes in <see cref="INodeLocator{TNode}"/>
        /// </summary>
        public TimeSpan? NodesRebuildingPeriod { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Period to check if nodes are responsive
        /// If node is not responded during <see cref="SocketPoolConfiguration.ConnectionTimeout"/> it is marked as dead
        /// and will be deleted from node locator until it is responsive again
        /// </summary>
        public TimeSpan? NodesHealthCheckPeriod { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Enables health check of nodes to remove dead nodes
        /// </summary>
        public bool NodeHealthCheckEnabled { get; set; } = true;

        public static MaintainerConfiguration DefaultConfiguration()
        {
            return new MaintainerConfiguration
            {
                NodesRebuildingPeriod = TimeSpan.FromSeconds(15),
                NodesHealthCheckPeriod = TimeSpan.FromSeconds(15),
                NodeHealthCheckEnabled = true
            };
        }
    }

    public class AuthenticationCredentials
    {
        public string Username { get; set; }
        
        public string Password { get; set; }
    }
}