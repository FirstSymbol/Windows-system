using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WindowsSystem.Layouts
{
  /// <summary>Slot and tiling helpers for <see cref="IWindowsService"/>.</summary>
  public static class WindowLayoutExtensions
  {
    /// <summary>Moves the registered window into the slot and shows it.</summary>
    public static async UniTask<bool> ShowInSlotAsync<TWindow>(this IWindowsService service, WindowSlotRef slot,
      bool instant = false, CancellationToken cancellationToken = default) where TWindow : WindowBase
    {
      TWindow window = service.Get<TWindow>();
      if (window == null)
      {
        WindowsLog.Warning($"No registered window of type '{typeof(TWindow).Name}'.");
        return false;
      }

      window.MoveToSlot(slot);
      await window.ShowAsync(instant, cancellationToken);
      return true;
    }

    /// <summary>Spawns the window under the layout root, places it into the slot and shows it.</summary>
    public static async UniTask<TWindow> OpenInSlotAsync<TWindow>(this IWindowsService service, WindowSlotRef slot,
      bool instant = false, CancellationToken cancellationToken = default) where TWindow : WindowBase
    {
      RectTransform parent = service.DefaultParent;
      if (parent == null && WindowLayoutRoot.Main != null)
        parent = WindowLayoutRoot.Main.RectTransform;

      TWindow window = await service.SpawnAsync<TWindow>(parent, null, cancellationToken);
      if (window == null || cancellationToken.IsCancellationRequested)
        return window;

      window.MoveToSlot(slot);
      await window.ShowAsync(instant, cancellationToken);
      return window;
    }

    /// <summary>Tiles every visible window of the service over the main layout root.</summary>
    public static void TileVisible(this IWindowsService service, WindowTilingMode mode, bool animate = true)
    {
      WindowLayoutRoot root = WindowLayoutRoot.Main;
      if (root == null)
      {
        WindowsLog.Warning("Cannot tile windows: no enabled WindowLayoutRoot.");
        return;
      }

      var visible = new List<WindowBase>();
      foreach (WindowBase window in service.Windows)
        if (window != null && window.IsVisible)
          visible.Add(window);

      root.Tile(mode, visible, animate);
    }
  }
}
