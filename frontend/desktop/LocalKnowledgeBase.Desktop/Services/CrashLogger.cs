using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LocalKnowledgeBase.Desktop.Services;

internal enum CrashLogDisposition
{
    Logged,
    Suppressed,
    Repeated
}

/// <summary>
/// Writes unhandled exceptions to a bounded crash log.
/// Repeated exceptions are suppressed to prevent layout loops from filling the disk.
/// </summary>
internal static class CrashLogger
{
    private const long MaxFileBytes = 5 * 1024 * 1024;
    private const int MaxEntriesPerSignature = 3;
    private static readonly object Gate = new();
    private static readonly Dictionary<string, int> Counts = new(StringComparer.Ordinal);

    public static CrashLogDisposition Log(Exception exception)
    {
        var signature = GetSignature(exception);

        lock (Gate)
        {
            Counts.TryGetValue(signature, out var count);
            count++;
            Counts[signature] = count;

            if (count == MaxEntriesPerSignature + 1)
            {
                TryAppend(
                    $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] Further identical exceptions suppressed; " +
                    "the application will shut down to avoid an infinite loop.\n\n");
                return CrashLogDisposition.Repeated;
            }

            if (count > MaxEntriesPerSignature)
                return CrashLogDisposition.Repeated;

            var text = count == 1
                ? $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {exception}\n\n"
                : $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] Repeated exception " +
                  $"({count}/{MaxEntriesPerSignature}): {signature}\n\n";

            return TryAppend(text) ? CrashLogDisposition.Logged : CrashLogDisposition.Suppressed;
        }
    }

    private static bool TryAppend(string text)
    {
        try
        {
            var logPath = GetLogPath();
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            RotateIfNeeded(logPath);
            File.AppendAllText(logPath, text, Encoding.UTF8);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GetLogPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LocalKnowledgeBase",
            "crash.log");

    private static void RotateIfNeeded(string logPath)
    {
        if (!File.Exists(logPath))
            return;

        var info = new FileInfo(logPath);
        if (info.Length <= MaxFileBytes)
            return;

        File.WriteAllText(
            logPath,
            $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] Crash log exceeded " +
            $"{MaxFileBytes / 1024 / 1024} MB and was truncated.\n\n",
            Encoding.UTF8);
    }

    private static string GetSignature(Exception exception)
    {
        var firstFrame = exception.StackTrace?
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?.Trim();

        return string.IsNullOrWhiteSpace(firstFrame)
            ? $"{exception.GetType().FullName}: {exception.Message}"
            : $"{exception.GetType().FullName}: {exception.Message} | {firstFrame}";
    }
}
