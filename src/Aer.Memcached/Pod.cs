using System.Net;
using Aer.ConsistentHash;

namespace Aer.Memcached;

public class Pod: INode
{
    private static readonly int MemcachedPort = 11211;
    
    public string IpAddress { get; init; }

    public string GetKey()
    {
        return IpAddress;
    }

    public EndPoint GetEndpoint()
    {
        return new DnsEndPoint(IpAddress, MemcachedPort);
    }
}