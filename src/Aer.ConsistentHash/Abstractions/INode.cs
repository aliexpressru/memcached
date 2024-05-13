using System.Net;

namespace Aer.ConsistentHash.Abstractions;

public interface INode: IEquatable<INode>
{
    string GetKey();

    EndPoint GetEndpoint();
}