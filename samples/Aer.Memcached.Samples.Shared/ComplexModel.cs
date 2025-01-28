namespace Aer.Memcached.Samples.Shared;

public class ComplexModel
{
    public Dictionary<string, long> TestValues { get; set; }
    
    public SomeEnum SomeEnum { get; set; }
}

public enum SomeEnum
{
    FirstValue,
    SecondValue
}