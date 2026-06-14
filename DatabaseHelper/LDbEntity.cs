namespace LQ.DatabaseHelper;

[AttributeUsage(AttributeTargets.Class)]
public class LDbEntityAttribute(bool isCritical = false, uint allowDbId = 0) : Attribute
{
    public bool IsCritical { get; } = isCritical;
    public uint AllowDbId { get; } = allowDbId;  // 0 means all
}

public class PlayerBundle(uint id)
{
    public uint Id { get; set; } = id;

    // all tables
    public List<LDbBaseTable> Tables { get; set; } = [];

    public T? GetTable<T>() where T : class
        => Tables.FirstOrDefault(x => x is T) as T;

    public void AddTable(LDbBaseTable? table)
    {
        if (table == null) return;

        table.Id = Id; // ensure same id
        lock (Tables)
        {
            if (Tables.All(x => x.GetType() != table.GetType()))  // unique
            {
                Tables.Add(table);
            }
        }
    }
}

public class DataEntry
{
    public PlayerBundle Bundle { get; set; } = null!;
    public bool IsOnline { get; set; }
    public DateTime LastOfflineTime { get; set; }

    private int _referenceCount = 0;
    public void IncRef() => Interlocked.Increment(ref _referenceCount);
    public void DecRef() => Interlocked.Decrement(ref _referenceCount);

    // offline && no reference
    public bool CanUnload => !IsOnline && _referenceCount <= 0;
}