using System.Net;
using Aer.ConsistentHash;

namespace Aer.Memcached.Tests;

public class Node: INode
{
    private readonly string _key;

    public Node()
    {
        _key = Guid.NewGuid().ToString();
    }
    
    public string GetKey()
    {
        return _key;
    }

    public EndPoint GetEndpoint()
    {
        throw new NotImplementedException();
    }
}