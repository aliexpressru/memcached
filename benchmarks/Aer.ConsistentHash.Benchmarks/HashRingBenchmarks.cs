using BenchmarkDotNet.Attributes;

namespace Aer.ConsistentHash.Benchmarks;

public class HashRingBenchmarks
{
    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private HashRing<Node> _hashRing = new(new HashCalculator());

    private string[] _keys;

    [Params(1, 128, 512, 2048, 5000, 10000, 20000)]
    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once UnassignedField.Global
    public int KeysNumber;

    [Params(1, 16, 32)]
    // ReSharper disable once UnassignedField.Global
    // ReSharper disable once MemberCanBePrivate.Global
    public int NodesNumber;

    [GlobalSetup]
    public void Setup()
    {
        var nodes = Enumerable.Range(0, NodesNumber).Select(n => new Node
        {
            IpAddress = Guid.NewGuid().ToString()
        });
        _keys = Enumerable.Range(0, KeysNumber).Select(n => Guid.NewGuid().ToString()).ToArray();
        
        _hashRing.AddNodes(nodes);
    }

    [Benchmark]
    public void GetNodes()
    {
        _hashRing.GetNodes(_keys);
    }
}