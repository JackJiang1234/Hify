namespace Hify.Modules.Mcp.Protocol;

/// <summary>握手成功后服务端自述信息，供连通性测试展示。</summary>
internal sealed record McpServerDescriptor
{
    /// <summary>服务端名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>服务端版本。</summary>
    public string Version { get; init; } = string.Empty;
}
