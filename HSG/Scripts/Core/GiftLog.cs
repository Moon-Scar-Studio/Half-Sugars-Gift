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
    public static void LogError(string? message = "undefined", string reason= "undefined")
    {
        if (message == null)
        {
            message = "日志值为null";
        }
        string logLine = $"[Error-{DateTime.Now:yyyy/M/d HH:mm:ss}]: \"{message}\"";
        AppendLog(logLine);
        throw new Exception($"Error. msg:{message},reason:{reason}");
    }
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