using Npgsql;

namespace Hify.Modules.Mcp.Tests.Support;

/// <summary>
/// DB 测试集合共享夹具：一次性把 mcp 两表按仓库根 <c>ddl.sql</c> 重建（DROP + 重跑 DDL），
/// 确保测试库 schema 与当前 DDL 一致（CREATE IF NOT EXISTS 不会补列，故先 DROP）。连不上则跳过。
/// </summary>
public sealed class McpSchemaFixture : IAsyncLifetime
{
    /// <summary>测试库是否可用；不可用时相关测试应直接跳过。</summary>
    public bool Available { get; private set; }

    public async Task InitializeAsync()
    {
        Available = await TestDb.IsAvailableAsync();
        if (!Available)
        {
            return;
        }

        var ddl = await File.ReadAllTextAsync(LocateDdl());

        // 用原生 Npgsql 执行：EF 的 ExecuteSqlRaw 会把 DDL 里的 '{}'（jsonb 默认值）当作参数占位符而报错。
        await using var connection = new NpgsqlConnection(TestDb.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "DROP TABLE IF EXISTS mcp.mcp_tool CASCADE; DROP TABLE IF EXISTS mcp.mcp_server CASCADE;");
        await ExecuteAsync(connection, ddl);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string LocateDdl()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "ddl.sql");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("找不到 ddl.sql（从测试输出目录向上未定位到仓库根）。");
    }
}

/// <summary>DB 测试集合定义：共享 <see cref="McpSchemaFixture"/>，集合内串行执行避免 schema/数据竞争。</summary>
[CollectionDefinition(Name)]
public sealed class McpDbCollection : ICollectionFixture<McpSchemaFixture>
{
    public const string Name = "Mcp Db";
}
