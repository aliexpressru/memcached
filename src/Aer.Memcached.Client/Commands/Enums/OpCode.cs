using System.Diagnostics.CodeAnalysis;

namespace Aer.Memcached.Client.Commands.Enums;

/// <summary>
/// Q suffix stands for quite
/// Documentation - https://github.com/memcached/memcached/wiki/BinaryProtocolRevamped
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Using names from docummentation")]
public enum OpCode: byte
{
    Get = 0x00,
    Set = 0x01,
    Add = 0x02,
    Replace = 0x03,
    Delete = 0x04,
    Increment = 0x05,
    Decrement = 0x06,
    Quit = 0x07,
    Flush = 0x08,
    GetQ = 0x09,
    NoOp = 0x0A,
    Version = 0x0B,
    GetK = 0x0C,
    GetKQ = 0x0D,
    Append = 0x0E,
    Prepend = 0x0F,
    Stat = 0x10,
    SetQ = 0x11,
    AddQ = 0x12,
    ReplaceQ = 0x13,
    DeleteQ = 0x14,
    IncrementQ = 0x15,
    DecrementQ = 0x16,
    QuitQ = 0x17,
    FlushQ = 0x18,
    AppendQ = 0x19,
    PrependQ = 0x1A,
    Touch = 0x1C,
    GetAndTouch = 0x1D,

    // SASL authentication op-codes
    SaslList = 0x20,
    SaslStart = 0x21,
    SaslStep = 0x22
}