using System.Net;
using Aer.Memcached.Abstractions;
using Aer.Memcached.Client.Config;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Infrastructure;

internal class HeadlessServiceDnsLookupNodeProvider : INodeProvider<Pod>
{
    private readonly MemcachedConfiguration _config;

    public HeadlessServiceDnsLookupNodeProvider(IOptions<MemcachedConfiguration> config)
    {
        _config = config.Value;
    }

    public bool IsConfigured()
    {
        return _config.IsConfigured();
    }
    
    public ICollection<Pod> GetNodes()
    {
        if (!string.IsNullOrWhiteSpace(_config.HeadlessServiceAddress))
        {
            IPAddress[] ipAddresses = Dns.GetHostAddresses(_config.HeadlessServiceAddress);
            var currentPods = ipAddresses.Select(
                    i => new Pod(i.ToString(), _config.MemcachedPort)
                )
                .ToArray();

            return currentPods;
        }

        if (_config.Servers.Length != 0)
        {
            // means that some memecahed servers are hard-coded through the configuration
            
            var staticPods = _config.Servers.Select(
                s => new Pod(s.IpAddress, s.Port)
            ).ToArray();

            return staticPods;
        }

        return Array.Empty<Pod>();
    }
}