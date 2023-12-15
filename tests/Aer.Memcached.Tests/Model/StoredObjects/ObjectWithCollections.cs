namespace Aer.Memcached.Tests.Model.StoredObjects;

public class ObjectWithCollections
{
    public string TestValue { get; set; }
    
    public List<SimpleObject> SimpleObjects { get; set; }
    
    public List<string> StrValues { get; set; }
}