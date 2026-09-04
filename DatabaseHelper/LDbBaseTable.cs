using FreeSql.DataAnnotations;

namespace LQ.DatabaseHelper;

public class LDbBaseTable
{
    [Column(IsPrimary = true)]
    public uint Id { get; set; }
}