using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WindowsSystem
{
  public enum WindowsLogLevel
  {
    None = 0,
    Error = 1,
    Warning = 2,
    Info = 3
  }

  /// <summary>
  /// Minimal logger used by the windows system.
  /// Assign <see cref="Handler"/> to route messages to any logging backend.
  /// </summary>
  public static class WindowsLog
  {
    public const string Tag = "Windows";

    /// <summary>Messages above this level are discarded.</summary>
    public static WindowsLogLevel Level { get; set; } = WindowsLogLevel.Warning;

    /// <summary>
    /// Custom output. When null, messages go to <see cref="Debug.unityLogger"/>.
    /// </summary>
    public static Action<LogType, string, Object> Handler { get; set; }

    public static bool IsEnabled(WindowsLogLevel level) => level != WindowsLogLevel.None && Level >= level;

    public static void Info(string message, Object context = null) =>
      Write(WindowsLogLevel.Info, LogType.Log, message, context);

    public static void Warning(string message, Object context = null) =>
      Write(WindowsLogLevel.Warning, LogType.Warning, message, context);

    public static void Error(string message, Object context = null) =>
      Write(WindowsLogLevel.Error, LogType.Error, message, context);

    private static void Write(WindowsLogLevel level, LogType type, string message, Object context)
    {
      if (!IsEnabled(level))
        return;

      if (Handler != null)
        Handler(type, message, context);
      else
        Debug.unityLogger.Log(type, Tag, message, context);
    }
  }
}
