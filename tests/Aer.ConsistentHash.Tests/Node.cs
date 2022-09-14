using System.Net;

namespace Aer.ConsistentHash.Tests;

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