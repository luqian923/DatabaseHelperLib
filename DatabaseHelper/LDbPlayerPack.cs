namespace LQ.DatabaseHelper;

public class LDbPlayerPack(uint id, LDbManager manager)
{
    public uint Id { get; } = id;
    private readonly List<LDbBaseTable> _tables = [];
    private readonly ReaderWriterLockSlim _lock = new();
    private int _refCount;

    public void Add(LDbBaseTable table)
    {
        _lock.EnterWriteLock();
        try
        {
            _tables.Add(table);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public List<LDbBaseTable> GetSnapshot()
    {
        _lock.EnterReadLock();
        try
        {
            return _tables.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public T? GetTable<T>() where T : LDbBaseTable
    {
        // load from cache
        var snapshot = GetSnapshot();
        var existing = snapshot.OfType<T>().FirstOrDefault();
        if (existing != null)
            return existing;

        if (!manager.IsDynamicType(typeof(T))) return null;

        // load table
        var loaded = manager.LoadTableForPack<T>(Id);
        if (loaded != null)
        {
            Add(loaded);
            return loaded;
        }
        return null;
    }

    public T GetOrCreateTable<T>() where T : LDbBaseTable, new()
    {
        var table = GetTable<T>();
        if (table != null)
            return table;

        table = new T { Id = Id };

        manager.SaveInstance(table);
        Add(table);

        return table;
    }

    public void IncRef() => Interlocked.Increment(ref _refCount);
    public void DecRef() => Interlocked.Decrement(ref _refCount);
    public bool CanUnload => _refCount <= 0;
}