namespace Hify.Modules.Workflow.Domain;

/// <summary>工作流状态常量（对齐 workflow.status 取值）。</summary>
internal static class WorkflowStatus
{
    /// <summary>草稿（保存即此态，校验仅警告不拦截）。</summary>
    public const string Draft = "draft";

    /// <summary>已发布（发布时跑图校验，不过则拒绝）。</summary>
    public const string Published = "published";
}
