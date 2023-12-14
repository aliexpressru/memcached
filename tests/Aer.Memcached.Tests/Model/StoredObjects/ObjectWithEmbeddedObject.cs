namespace Aer.Memcached.Tests.Model.StoredObjects;

public class ObjectWithEmbeddedObject
{
    public long LongValue { get; set; }
    
    public ComplexObject ComplexObject { get; set; }
}

public class ComplexObject
{
    public string TestValue { get; set; }
    
    public SimpleObject SimpleObject { get; set; }
}