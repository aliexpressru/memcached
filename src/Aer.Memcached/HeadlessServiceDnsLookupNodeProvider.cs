using System.Net;
using Aer.Memcached.Client.Config;
using Microsoft.Extensions.Options;

namespace Aer.Memcached;

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
            var currentPods = ipAddresses.Select(i => new Pod
            {
                IpAddress = i.ToString()
            }).ToArray();

            return currentPods;
        }

        if (_config.Servers.Length != 0)
        {
            var staticPods = _config.Servers.Select(s => new Pod
            {
                IpAddress = s.IpAddress
            }).ToArray();

            return staticPods;
        }

        return Array.Empty<Pod>();
    }
}