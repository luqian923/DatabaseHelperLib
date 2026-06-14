using SqlSugar;

namespace LQ.DatabaseHelper;

public static class LDatabaseHelper
{

    internal static readonly List<(Type Type, bool IsCritical, uint DbId)> Registry = [];
    public static void Register<T>(bool isCritical, uint dbId) => Registry.Add((typeof(T), isCritical, dbId));
    public static Dictionary<uint, LDbManager> DbManagers { get; set; } = [];
    private static uint _dbId;

    // create manager
    public static LDbManager CreateManager(string connString, DbType dbType = DbType.Sqlite, int autoSaveInterval = 1, uint dbId = 0)
    {
        var manager = new LDbManager(connString, dbType, autoSaveInterval, dbId);

        dbId = dbId == 0 ? ++_dbId : dbId;
        DbManagers[dbId] = manager;

        return manager;
    }

    public static List<(Type Type, bool IsCritical, uint DbId)> GetRegisteredTypes(LDbManager manager) => Registry.Where(x => x.DbId == manager.DbId).ToList();
}