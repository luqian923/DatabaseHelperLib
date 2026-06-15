using SqlSugar;
using System.Collections;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Timer = System.Timers.Timer;

namespace LQ.DatabaseHelper;

public class LDbManager : IDisposable
{
    private readonly SqlSugarScope _sqlSugarScope;
    public readonly ConcurrentDictionary<uint, LDbPlayerPack> CriticalInstances = [];
    public readonly ConcurrentDictionary<uint, LDbPlayerPack> DynamicInstances = [];
    public ConcurrentBag<uint> SaveList { get; set; } = [];
    public Timer SaveTimer;
    public bool LoadCriticalData { get; set; }
    public bool LoadDynamicData { get; set; }
    public uint DbId { get; set; }
    public int AutoSaveInterval { get; set; }

    private readonly List<Type> _criticalTypes;
    private readonly List<Type> _dynamicTypes;

    public event Action<double>? OnSaveDatabase;
    public event Action<string, Exception>? OnError;

    public LDbManager(string connString, DbType dbType = DbType.Sqlite, int autoSaveInterval = 5, uint dbId = 1)
    {
        _sqlSugarScope = new SqlSugarScope(new ConnectionConfig
        {
            ConnectionString = connString,
            DbType = dbType,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                SerializeService = new CustomSerializeService()
            }
        });

        AutoSaveInterval = autoSaveInterval;
        DbId = dbId;

        InitializeSqlite();

        var types = LDatabaseHelper.GetRegisteredTypes(this);
        _criticalTypes = types.Where(x => x.IsCritical).Select(x => x.Type).ToList();
        _dynamicTypes = types.Where(x => !x.IsCritical).Select(x => x.Type).ToList();

        // load critical instances
        foreach (var pType in _criticalTypes)
        {
            // load data from db
            var dataList = _sqlSugarScope.QueryableByObject(pType).ToList();
            if (dataList is not IEnumerable datas) continue;

            foreach (var data in datas)
            {
                if (data is not LDbBaseTable table) continue;

                if (!CriticalInstances.TryGetValue(table.Id, out var value))
                {
                    value = new LDbPlayerPack(table.Id, this);
                }

                value.Add(table);

                CriticalInstances[table.Id] = value;
            }
        }

        // start dispatch server
        LoadCriticalData = true;

        SaveTimer = new Timer(AutoSaveInterval * 60 * 1000);
        SaveTimer.Elapsed += (_, _) =>
        {
            SaveDatabase();
        };
        SaveTimer.AutoReset = true;
        SaveTimer.Start();

        LoadDynamicData = true;
    }

    public void InitializeSqlite()
    {
        _sqlSugarScope.DbMaintenance.CreateDatabase();

        var types = LDatabaseHelper.GetRegisteredTypes(this).Select(x => x.Type).ToList();
        foreach (var type in types)
        {
            _sqlSugarScope.CodeFirst.InitTables(type);
        }
    }

    public LDbPlayerPack GetOrCreatePack(uint uid, bool critical)
    {
        if (critical)
        {
            if (CriticalInstances.TryGetValue(uid, out var criticalPack))
            {
                return criticalPack;
            }

            // create
            var pack = new LDbPlayerPack(uid, this);
            CriticalInstances.TryAdd(uid, pack);

            return pack;
        }

        // dynamic pack
        var dynPack = DynamicInstances.GetOrAdd(uid, u =>
        {
            var pack = new LDbPlayerPack(u, this);
            // load data
            foreach (var type in _dynamicTypes)
            {
                var instance = LoadSingleTable(uid, type);
                if (instance != null)
                    pack.Add(instance);
            }
            return pack;
        });
        dynPack.IncRef();

        return dynPack;
    }

    public LDbPlayerPack? GetPack(uint uid, bool critical)
    {
        if (critical)
        {
            return CriticalInstances.GetValueOrDefault(uid);
        }

        // dynamic pack
        var dynPack = DynamicInstances.GetValueOrDefault(uid);
        dynPack?.IncRef();

        return dynPack;
    }

    private LDbBaseTable? LoadSingleTable(uint uid, Type type)
    {
        var result = _sqlSugarScope.QueryableByObject(type)
            .Where("Id = @uid", new { uid })
            .Take(1)
            .ToList();
        return result is IList { Count: > 0 } list ? list[0] as LDbBaseTable : null;
    }

    public bool IsDynamicType(Type type) => _dynamicTypes.Contains(type);
    
    public T? LoadTableForPack<T>(uint uid) where T : LDbBaseTable
    {
        var type = typeof(T);
        if (!_dynamicTypes.Contains(type))
            return null;

        return LoadSingleTable(uid, type) as T;
    }

    public List<T>? GetAllInstance<T>() where T : class, new()
    {
        return _sqlSugarScope.Queryable<T>().ToList();
    }

    public List<T> GetAllInstanceFromMap<T>() where T : class, new()
    {
        var dict = _criticalTypes.Contains(typeof(T)) ? CriticalInstances : DynamicInstances;
        return dict.Values.SelectMany(p => p.GetSnapshot()).OfType<T>().ToList();
    }

    public void SaveInstance<T>(T instance) where T : LDbBaseTable, new()
    {
        _sqlSugarScope.Insertable(instance).ExecuteCommand();

        // add to pack
        GetOrCreatePack(instance.Id, _criticalTypes.Contains(instance.GetType())).Add(instance);
    }

    public void SaveDatabase() // per 5 min
    {
        try
        {
            var prev = DateTime.Now;
            var toSaveUids = SaveList.ToHashSet();
            SaveList.Clear();
            foreach (var uid in toSaveUids)
            {
                try
                {
                    if (CriticalInstances.TryGetValue(uid, out var cPack))
                    {
                        SavePlayerPack(cPack);
                    }

                    if (DynamicInstances.TryGetValue(uid, out var dPack))
                    {
                        SavePlayerPack(dPack);
                    }
                }
                catch (Exception e)
                {
                    // trigger event
                    OnError?.Invoke("An error occurred when saving database", e);
                    SaveList.Add(uid);
                }
            }

            var t = (DateTime.Now - prev).TotalSeconds;

            // trigger event
            OnSaveDatabase?.Invoke(t);

            // clean offline
            foreach (var inst in DynamicInstances.Values.ToList().Where(inst => inst.CanUnload))
            {
                DynamicInstances.TryRemove(inst.Id, out _);
            }
        }
        catch (Exception e)
        {
            // trigger event
            OnError?.Invoke("An error occurred when saving database", e);
        }
    }

    public void SavePlayerPack(LDbPlayerPack pack)
    {
        var snapshot = pack.GetSnapshot();
        if (snapshot.Count == 0) return;

        _sqlSugarScope.Ado.BeginTran();
        try
        {
            var groups = snapshot.GroupBy(e => e.GetType());
            foreach (var group in groups)
            {
                 _sqlSugarScope.StorageableByObject(group.ToList()).ExecuteCommand();
            }

            _sqlSugarScope.Ado.CommitTran();
        }
        catch (Exception e)
        {
            _sqlSugarScope.Ado.RollbackTran();
            OnError?.Invoke("An error occurred when saving database", e);
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var pack in DynamicInstances.Values.Concat(CriticalInstances.Values))
        {
            try
            {
                SavePlayerPack(pack);  // save all data
            }
            catch (Exception e)
            {
                OnError?.Invoke("An error occurred when saving database", e);
            }
        }

        LDatabaseHelper.DbManagers.Remove(DbId);

        _sqlSugarScope.Dispose();
        SaveTimer.Dispose();
    }
}