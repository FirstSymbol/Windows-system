using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WindowsSystem
{
  public interface IWindowsService
  {
    /// <summary>Registered windows, one per window type.</summary>
    IReadOnlyCollection<WindowBase> Windows { get; }

    WindowsQueue Queue { get; }

    /// <summary>Parent used by <see cref="SpawnAsync{TWindow}"/> when none is passed.</summary>
    RectTransform DefaultParent { get; set; }

    event Action<WindowBase> WindowRegistered;
    event Action<WindowBase> WindowUnregistered;
    event Action<WindowBase> WindowStateChanged;

    TWindow Get<TWindow>() where TWindow : WindowBase;
    WindowBase Get(Type windowType);
    bool TryGet<TWindow>(out TWindow window) where TWindow : WindowBase;
    bool TryGet(Type windowType, out WindowBase window);
    bool IsRegistered<TWindow>() where TWindow : WindowBase;
    bool IsRegistered(Type windowType);

    /// <returns>False if no window of the type is registered.</returns>
    UniTask<bool> ShowAsync<TWindow>(bool instant = false, CancellationToken cancellationToken = default)
      where TWindow : WindowBase;

    UniTask<bool> ShowAsync(Type windowType, bool instant = false, CancellationToken cancellationToken = default);

    UniTask<bool> HideAsync<TWindow>(bool instant = false, CancellationToken cancellationToken = default)
      where TWindow : WindowBase;

    UniTask<bool> HideAsync(Type windowType, bool instant = false, CancellationToken cancellationToken = default);

    UniTask<bool> ToggleAsync<TWindow>(bool instant = false, CancellationToken cancellationToken = default)
      where TWindow : WindowBase;

    UniTask<bool> ToggleAsync(Type windowType, bool instant = false, CancellationToken cancellationToken = default);

    /// <summary>Hides and destroys the registered window of the type.</summary>
    UniTask<bool> CloseAsync<TWindow>(bool instant = false, CancellationToken cancellationToken = default)
      where TWindow : WindowBase;

    UniTask<bool> CloseAsync(Type windowType, bool instant = false, CancellationToken cancellationToken = default);

    /// <summary>Hides every visible window.</summary>
    UniTask HideAllAsync(bool instant = false, CancellationToken cancellationToken = default);

    /// <summary>Instantiates the window prefab from the provider. The window keeps its initial state.</summary>
    UniTask<TWindow> SpawnAsync<TWindow>(RectTransform parent = null, Vector2? anchoredPosition = null,
      CancellationToken cancellationToken = default) where TWindow : WindowBase;

    /// <summary>Instantiates the window prefab and shows it.</summary>
    UniTask<TWindow> OpenAsync<TWindow>(RectTransform parent = null, Vector2? anchoredPosition = null,
      bool instant = false, CancellationToken cancellationToken = default) where TWindow : WindowBase;
  }
}
