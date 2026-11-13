namespace MbdGitTool.Core.Contracts;

/// <summary>
/// Host API 稳定错误码（code-plan §3.11 / spec 附录 B）。错误码稳定、文案可改。
/// <para>Host 内部安全分类一律映射到 <c>E_*</c>；SRS 前缀（SEC/FS/CFG…）放 <c>details.srsCode</c>。</para>
/// </summary>
public static class BridgeErrorCodes
{
    public const string InvalidRequest = "E_INVALID_REQUEST";
    public const string InvalidArgument = "E_INVALID_ARGUMENT";
    public const string UnknownMethod = "E_UNKNOWN_METHOD";
    public const string Timeout = "E_TIMEOUT";
    public const string Internal = "E_INTERNAL";
    public const string ResponseTooLarge = "E_RESPONSE_TOO_LARGE";
    public const string WorkspaceUnset = "E_WORKSPACE_UNSET";
    public const string WorkspaceInvalid = "E_WORKSPACE_INVALID";
    public const string NotFound = "E_NOT_FOUND";
    public const string RepoNotLocal = "E_REPO_NOT_LOCAL";
    public const string RepoBusy = "E_REPO_BUSY";
    public const string PathInvalid = "E_PATH_INVALID";
    public const string PathOutsideRoot = "E_PATH_OUTSIDE_ROOT";
    public const string ReparsePoint = "E_REPARSE_POINT";
    public const string GitInternal = "E_GIT_INTERNAL";
    public const string AccessDenied = "E_ACCESS_DENIED";
    public const string IoError = "E_IO_ERROR";
    public const string TooManyEntries = "E_TOO_MANY_ENTRIES";
    public const string ConfirmRequired = "E_CONFIRM_REQUIRED";
    public const string DangerousExtension = "E_DANGEROUS_EXT";
    public const string Submodule = "E_SUBMODULE";
    public const string LaunchFailed = "E_LAUNCH_FAILED";
    public const string GitFailed = "E_GIT_FAILED";
    public const string ActionNotAllowed = "E_ACTION_NOT_ALLOWED";
    public const string NotImplemented = "E_NOT_IMPLEMENTED";
    public const string CiDisabled = "E_CI_DISABLED";
}

/// <summary>桥错误：<c>code</c> 稳定错误码 + 人类可读 <c>message</c> + 可选 <c>details</c>（诊断/确认流程）。</summary>
public sealed record BridgeError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, object?>? Details = null);

/// <summary>路由层统一结果：成功带 <c>data</c>，失败带 <see cref="BridgeError"/>。</summary>
public sealed record CommandResult(bool Ok, object? Data, BridgeError? Error)
{
    public static CommandResult Success(object? data) => new(true, data, null);

    public static CommandResult Failure(BridgeError error) => new(false, null, error);

    public static CommandResult Failure(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? details = null) =>
        new(false, null, new BridgeError(code, message, details));
}

/// <summary>服务内部用于中断路由的异常（携带稳定错误码，不打印内部堆栈）。</summary>
public sealed class CommandException : Exception
{
    public CommandException(BridgeError error)
        : base(error.Message) => Error = error;

    public CommandException(string code, string message, IReadOnlyDictionary<string, object?>? details = null)
        : this(new BridgeError(code, message, details))
    {
    }

    public BridgeError Error { get; }
}
