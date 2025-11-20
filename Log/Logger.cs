using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using WindowsSystem.Utilities.Extensions;
using Debug = UnityEngine.Debug;

namespace WindowsSystem.Log
{
    public class Logger
    {

        private static readonly LogTag[] _tagsToExclude = { };

        private static readonly Dictionary<LogTag, Color> _tagColors = new()
        {
            {LogTag.WindowsService, new Color(0.8f, 0.4f, 0.2f)},
            {LogTag.WindowsQueueController, new Color(0.8f, 0.3f, 0.2f)}
        };

        private static bool IsColoredLogs => true;
        
        [Conditional("DEV"), Conditional("UNITY_EDITOR")]
        public static void Log(string text, LogTag tag = LogTag.Default)
        {
            if (_tagsToExclude.Contains(tag)) return;

            if (_tagColors.TryGetValue(tag, out Color color) && IsColoredLogs)
            {
                Debug.Log($"{color.ColorizeWithBrackets(tag)} {text}");
            }
            else
            {
                Debug.LogFormat("[{0}] {1}", tag, text);
            }
        }

        [Conditional("DEV"), Conditional("UNITY_EDITOR")]
        public static void Warn(string text, LogTag tag = LogTag.Default)
        {
            if (_tagColors.TryGetValue(tag, out Color color) && IsColoredLogs)
            {
                Debug.LogWarning($"[Warn]{color.ColorizeWithBrackets(tag)} {text}");
            }
            else
            {
                Debug.LogWarningFormat("[Warn][{0}] {1}", tag, text);
            }
        }

        public static void Error(string text, LogTag tag = LogTag.Default)
        {
            if (_tagColors.TryGetValue(tag, out Color color) && IsColoredLogs)
            {
                Debug.LogError($"[Exception]{color.ColorizeWithBrackets(tag)} {text}");
            }
            else
            {
                Debug.LogErrorFormat("[Exception][{0}] {1}", tag, text);
            }
        }
    }
}