using System.Text.Json.Serialization;

namespace LQ.DatabaseHelper.TestProgram;

[JsonSerializable(typeof(AccountTable))]
[JsonSerializable(typeof(PlayerTable))]
public partial class DbJsonContext : JsonSerializerContext;