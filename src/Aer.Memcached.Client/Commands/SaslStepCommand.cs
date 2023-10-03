using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Infrastructure;

namespace Aer.Memcached.Client.Commands;

internal class SaslStepCommand: SaslCommandBase
{
    private readonly byte[] _continuation;

    public SaslStepCommand(byte[] continuation) : base(OpCode.SaslStep)
    {
        _continuation = continuation;
    }

    internal override IList<ArraySegment<byte>> GetBuffer()
    {
        var request = new BinaryRequest(OpCode)
        {
            Key = SaslMechanism,
            Data = _continuation
        };

        return request.CreateBuffer();
    }
}