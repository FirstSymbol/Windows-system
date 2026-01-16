using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using WindowsSystem.Log;
using WindowsSystem.Providers;
using Zenject;
using Logger = ExtDebugLogger.Logger;
using Object = UnityEngine.Object;

namespace WindowsSystem
{
  public class WindowsService : IWindowsService
  {
    [Inject] private IWindowsProvider _windowsProvider;

    public WindowsService()
    {
      QueueController = new WindowsQueueController(this);
    }

    public Dictionary<Type, IWindowBase> Windows { get; } = new();
    public HashSet<Type> ShownWindows { get; } = new();
    public WindowsQueueController QueueController { get; }

    #region Registering

    public void RegisterWindow<T>(WindowBase<T> window) where T : IWindowBase
    {
      Windows.Add(typeof(T), window);
      window.OnAfterHide += OnAfterWindowHide;
      window.OnAfterShow += OnAfterShow;
      Logger.Log($"Registering window of type {typeof(T).ToString().Split('.')[^1]}", WSLogTag.WindowsService);
    }

    public void UnregisterWindow<T>(WindowBase<T> window) where T : IWindowBase
    {
      Windows.Remove(typeof(T));
      window.OnAfterHide -= OnAfterWindowHide;
      window.OnAfterShow -= OnAfterShow;
      Logger.Log($"Unregistering window of type {typeof(T).ToString().Split('.')[^1]}", WSLogTag.WindowsService);
    }

    private void OnAfterShow(Type windowType)
    {
      ShownWindows.Add(windowType);
    }

    private void OnAfterWindowHide(Type windowType)
    {
      ShownWindows.Remove(windowType);
    }

    #endregion

    #region Storage

    public IWindowBase GetWindow(Type type)
    {
      return Windows.GetValueOrDefault(type);
    }

    public TWindow GetWindow<TWindow>() where TWindow : class, IWindowBase
    {
      if (Windows.TryGetValue(typeof(TWindow), out var window))
        return window as TWindow;
      return null;
    }

    public bool TryGetWindow(Type type, out IWindowBase window)
    {
      return Windows.TryGetValue(type, out window);
    }

    public bool TryGetWindow(Type type, out IPooledWindow window)
    {
      Windows.TryGetValue(type, out var nativeWindow);
      window = nativeWindow as IPooledWindow;
      return window != null;
    }

    public bool TryGetWindow<TWindow>(out TWindow window) where TWindow : class, IWindowBase
    {
      if (Windows.TryGetValue(typeof(TWindow), out var value))
      {
        window = value as TWindow;
        return true;
      }

      window = null;
      return false;
    }

    public bool ExistWindow(Type type)
    {
      return Windows.ContainsKey(type);
    }

    #endregion

    #region Spawn & Destroy

    public TWindow SpawnWindow<TWindow>(Vector2 anchoredPosition, RectTransform parent)
      where TWindow : MonoBehaviour, IWindowBase
    {
      var windowPrefab = _windowsProvider.GetWindowPrefab<TWindow>();
      if (windowPrefab == null)
      {
        Logger.Warn("Could not find window prefab in windowsProvider.", WSLogTag.WindowsService);
        return null;
      }

      var window = Object.Instantiate(windowPrefab, anchoredPosition, Quaternion.identity, parent);
      window.IsSpawned = true;

      return window;
    }

    public TWindow OpenWindow<TWindow>(Vector2 anchoredPosition, RectTransform parent)
      where TWindow : MonoBehaviour, IWindowBase
    {
      var window = SpawnWindow<TWindow>(anchoredPosition, parent);

      if (window == null)
        return window;

      window.DisableShowHideActionsOnStart = true;
      window.Show().Forget();

      return window;
    }

    public bool CloseWindow(Type type)
    {
      Logger.Log($"Trying to close {type.Name} window!", WSLogTag.WindowsService);
      if (Windows.TryGetValue(type, out var window))
      {
        window.Close();
        return true;
      }

      return false;
    }

    public bool CloseWindow<T>() where T : IWindowBase
    {
      return CloseWindow(typeof(T));
    }

    #endregion

    #region Show & Hide

    public async Task<bool> ShowWindow(Type type)
    {
      if (Windows.TryGetValue(type, out var window))
      {
        await window.Show();
        return true;
      }

      return false;
    }

    public async Task<bool> HideWindow(Type type)
    {
      if (Windows.TryGetValue(type, out var window))
      {
        await window.Hide();
        return true;
      }

      return false;
    }

    public bool ToggleWindow(Type type)
    {
      if (Windows.TryGetValue(type, out var window))
      {
        window.Toggle();
        return true;
      }

      return false;
    }

    public Task<bool> ShowWindow<T>() where T : IWindowBase
    {
      return ShowWindow(typeof(T));
    }

    public Task<bool> HideWindow<T>() where T : IWindowBase
    {
      return HideWindow(typeof(T));
    }

    public bool ToggleWindow<T>() where T : IWindowBase
    {
      return ToggleWindow(typeof(T));
    }

    #endregion
  }
}