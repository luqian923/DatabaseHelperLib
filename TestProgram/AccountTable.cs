using SqlSugar;
using System.ComponentModel;

namespace LQ.DatabaseHelper.TestProgram;

[LDbEntity(true, 1)]
[SugarTable]
public class AccountTable : LDbBaseTable
{
    public string Name { get; set; } = "";
    public string Token { get; set; } = "";
}