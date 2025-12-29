using System.Collections.Generic;
using ExtDebugLogger.Attributes;
using ExtDebugLogger.Interfaces;
using UnityEngine;

namespace WindowsSystem.Log
{
  public class WSLogColorsConfig : IKeepDefaultLoggerTags<WSLogTag>
  {
    [field: ExtDebugLoggerTags]
    public static Dictionary<WSLogTag, Color> ColorDictionary { get; set; } = new()
    {
      { WSLogTag.WindowsService, new Color(0.88f,0.50f,0.24f, 1f) },
      { WSLogTag.WindowsQueueController, new Color(0.88f,0.63f,0.24f, 1f) }
    };
  }
}