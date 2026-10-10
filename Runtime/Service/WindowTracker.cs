using System;
using System.Collections.Generic;
using UnityEngine;

namespace WindowsSystem
{
  /// <summary>
  /// Tracks every initialized window so services can attach to windows created before or after them.
  /// </summary>
  internal static class WindowTracker
  {
    private static readonly List<WindowBase> AliveWindows = new();

    public static event Action<WindowBase> Added;
    public static event Action<WindowBase> Removed;

    public static IReadOnlyList<WindowBase> Alive => AliveWindows;

    public static void Add(WindowBase window)
    {
      if (window == null || AliveWindows.Contains(window))
        return;

      AliveWindows.Add(window);
      Added?.Invoke(window);
    }

    public static void Remove(WindowBase window)
    {
      if (!AliveWindows.Remove(window))
        return;

      Removed?.Invoke(window);
    }

    /// <summary>Drops windows destroyed without OnDestroy (objects that were never awake).</summary>
    public static void PurgeDestroyed()
    {
      for (int i = AliveWindows.Count - 1; i >= 0; i--)
      {
        WindowBase window = AliveWindows[i];
        if (window != null)
          continue;

        AliveWindows.RemoveAt(i);
        Removed?.Invoke(window);
      }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
      AliveWindows.Clear();
      Added = null;
      Removed = null;
    }
  }
}
