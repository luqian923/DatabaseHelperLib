using FreeSql.DataAnnotations;

namespace LQ.DatabaseHelper.TestProgram;

[LDbEntity(true, 1)]
[Table(Name = "account_table")]
public class AccountTable : LDbBaseTable
{
    public string Name { get; set; } = "";
    public string Token { get; set; } = "";

    [JsonMap]
    public TestClass Class { get; set; } = new TestClass();
}

public class TestClass
{
    public string Name { get; set; } = "Test";
}