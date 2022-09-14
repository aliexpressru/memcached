using System.Net;

namespace Aer.ConsistentHash;

public interface INode
{
    string GetKey();

    EndPoint GetEndpoint();
}