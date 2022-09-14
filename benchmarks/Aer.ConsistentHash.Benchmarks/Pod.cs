using System.Net;

namespace Aer.ConsistentHash.Benchmarks;

public class Node: INode
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