using FreeSql.DataAnnotations;

namespace LQ.DatabaseHelper.TestProgram;

[LDbEntity(false, 1)]
[Table(Name = "player_data")]
public class PlayerTable : LDbBaseTable
{
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public string Signature { get; set; } = "";
}