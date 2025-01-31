namespace Aer.ConsistentHash.Config;

public class HashRingSettings
{
    /// <summary>
    /// Default number of virtual nodes for HashRing
    /// </summary>
    public static readonly int DefaultNumberOfVirtualNodes = 256;
    
    /// <summary>
    /// Number of virtual nodes for HashRing
    /// </summary>
    public int NumberOfVirtualNodes { get; set; } = DefaultNumberOfVirtualNodes;
        
    /// <summary>
    /// Degree of parallelism while getting nodes
    /// </summary>
    public int? MaxDegreeOfParallelismForGettingNodes { get; set; }
}