using SqlSugar;

namespace LQ.DatabaseHelper;

public class LDbBaseTable
{
    [SugarColumn(IsPrimaryKey = true)]
    public uint Id { get; set; }
}