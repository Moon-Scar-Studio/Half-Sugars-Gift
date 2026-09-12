namespace HalfSugarGift.Core;

public static class HsgDebug
{
    private static readonly string LogFilePath;
    private static readonly string StartupTime;

    static HsgDebug()
    {
        StartupTime = DateTime.Now.ToString("yyyy/M/d HH:mm:ss");
        string gameRootPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Gift.log");
        string determinedPath = null;

        try
        {
            using (File.Open(gameRootPath, FileMode.OpenOrCreate, FileAccess.Write)) { }
            determinedPath = gameRootPath;
        }
        catch
        {
            string localLow = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "..", "LocalLow", "Innersloth", "Among Us"
            );
            determinedPath = Path.Combine(Path.GetFullPath(localLow), "GiftLog.log");
            string directory = Path.GetDirectoryName(determinedPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        LogFilePath = determinedPath;
        AppendLog("=====Log Started " + StartupTime + "=====");
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }
    public static void Log(string? message = "undefined")
    {
        if(message == null)
        {
            message = "日志值为null";
        }
        string logLine = $"[{DateTime.Now:yyyy/M/d HH:mm:ss}]: \"{message}\"";
        AppendLog(logLine);
    }
    /// <summary>
    /// 记录错误。只写日志，不抛异常：调用方大多在 catch 块里，抛出会把已经捕获的异常重新扔回去，
    /// 让本应"记录后继续"的容错逻辑（补丁跳过、静态构造函数、Harmony postfix）直接中断。
    /// 需要中断流程的地方请显式 throw。
    /// </summary>
    public static void LogError(string? message = "undefined", string reason = "undefined")
    {
        if (message == null)
        {
            message = "日志值为null";
        }
        string logLine = reason == "undefined"
            ? $"[Error-{DateTime.Now:yyyy/M/d HH:mm:ss}]: \"{message}\""
            : $"[Error-{DateTime.Now:yyyy/M/d HH:mm:ss}]: \"{message}\" (reason: {reason})";
        AppendLog(logLine);
    }
    /// <summary>记录异常（含堆栈）。</summary>
    public static void LogException(string? message, Exception exception)
        => LogError($"{message ?? "undefined"} :: {exception}");
    public static void LogWarning(string? message = "undefined")
    {
        if (message == null)
        {
            message = "日志值为null";
        }
        string logLine = $"[Warning-{DateTime.Now:yyyy/M/d HH:mm:ss}]: \"{message}\"";
        AppendLog(logLine);
    }
    static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        string exceptionName = (e.ExceptionObject as Exception)?.GetType().Name ?? "UnknownException";
        string logLine = $"Error<{DateTime.Now:HH:mm:ss}>: {exceptionName}";
        AppendLog(logLine);
    }
    static void AppendLog(string content)
    {
        try
        {
            File.AppendAllText(LogFilePath, content + Environment.NewLine);
        }
        catch { }
    }
    public static void ClearLog()
    {
        try
        {
            File.WriteAllText(LogFilePath, string.Empty);
        }
        catch { }
    }
}