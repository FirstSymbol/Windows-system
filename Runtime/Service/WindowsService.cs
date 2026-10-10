using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using WindowsSystem.Providers;
using Object = UnityEngine.Object;

namespace WindowsSystem
{
  /// <summary>
  /// Central access point to windows. Create one instance (usually through a DI container) and
  /// dispose it when it is no longer needed. Windows attach themselves automatically.
  /// </summary>
  public sealed class WindowsService : IWindowsService, IDisposable
  {
    private static readonly List<WindowsService> ActiveServices = new();

    private readonly Dictionary<Type, WindowBase> _windows = new();
    private readonly List<WindowBase> _attached = new();
    private readonly IWindowsProvider _provider;
    private bool _disposed;

    public WindowsService(IWindowsProvider provider = null, RectTransform defaultParent = null)
    {
      _provider = provider;
      DefaultParent = defaultParent;
      Queue = new WindowsQueue(type => Get(type));

      ActiveServices.Add(this);
      if (ActiveServices.Count > 1)
        WindowsLog.Warning("More than one WindowsService is alive. Windows attach to the latest one.");

      WindowTracker.Added += Attach;
      WindowTracker.Removed += Detach;
      SceneManager.sceneLoaded += OnSceneLoaded;
      SceneManager.sceneUnloaded += OnSceneUnloaded;

      for (int i = 0; i < SceneManager.sceneCount; i++)
        InitializeInactiveWindows(SceneManager.GetSceneAt(i));

      foreach (WindowBase window in WindowTracker.Alive)
        Attach(window);
    }

    /// <summary>Services that are alive (used by editor tooling).</summary>
    internal static IReadOnlyList<WindowsService> Active => ActiveServices;

    public IReadOnlyCollection<WindowBase> Windows => _windows.Values;

    /// <summary>All windows attached to this service, including additional instances of a registered type.</summary>
    public IReadOnlyList<WindowBase> AttachedWindows => _attached;

    public WindowsQueue Queue { get; }
    public RectTransform DefaultParent { get; set; }
    public IWindowsProvider Provider => _provider;

    public event Action<WindowBase> WindowRegistered;
    public event Action<WindowBase> WindowUnregistered;
    public event Action<WindowBase> WindowStateChanged;

    #region Lookup

    public TWindow Get<TWindow>() where TWindow : WindowBase => Get(typeof(TWindow)) as TWindow;

    public WindowBase Get(Type windowType)
    {
      if (windowType != null && _windows.TryGetValue(windowType, out WindowBase window) && window != null)
        return window;
      return null;
    }

    public bool TryGet<TWindow>(out TWindow window) where TWindow : WindowBase
    {
      window = Get<TWindow>();
      return window != null;
    }

    public bool TryGet(Type windowType, out WindowBase window)
    {
      window = Get(windowType);
      return window != null;
    }

    public bool IsRegistered<TWindow>() where TWindow : WindowBase => Get(typeof(TWindow)) != null;

    public bool IsRegistered(Type windowType) => Get(windowType) != null;

    #endregion

    #region Show & Hide

    public UniTask<bool> ShowAsync<TWindow>(bool instant = false, CancellationToken cancellationToken = default)
      where TWindow : WindowBase => ShowAsync(typeof(TWindow), instant, cancellationToken);

    public async UniTask<bool> ShowAsync(Type windowType, bool instant = false,
      CancellationToken cancellationToken = default)
    {
      WindowBase window = GetOrLog(windowType);
      if (window == null)
        return false;

      await window.ShowAsync(instant, cancellationToken);
      return true;
    }

    public UniTask<bool> HideAsync<TWindow>(bool instant = false, CancellationToken cancellationToken = default)
      where TWindow : WindowBase => HideAsync(typeof(TWindow), instant, cancellationToken);

    public async UniTask<bool> HideAsync(Type windowType, bool instant = false,
      CancellationToken cancellationToken = default)
    {
      WindowBase window = GetOrLog(windowType);
      if (window == null)
        return false;

      await window.HideAsync(instant, cancellationToken);
      return true;
    }

    public UniTask<bool> ToggleAsync<TWindow>(bool instant = false, CancellationToken cancellationToken = default)
      where TWindow : WindowBase => ToggleAsync(typeof(TWindow), instant, cancellationToken);

    public async UniTask<bool> ToggleAsync(Type windowType, bool instant = false,
      CancellationToken cancellationToken = default)
    {
      WindowBase window = GetOrLog(windowType);
      if (window == null)
        return false;

      await window.ToggleAsync(instant, cancellationToken);
      return true;
    }

    public UniTask<bool> CloseAsync<TWindow>(bool instant = false, CancellationToken cancellationToken = default)
      where TWindow : WindowBase => CloseAsync(typeof(TWindow), instant, cancellationToken);

    public async UniTask<bool> CloseAsync(Type windowType, bool instant = false,
      CancellationToken cancellationToken = default)
    {
      WindowBase window = GetOrLog(windowType);
      if (window == null)
        return false;

      await window.CloseAsync(instant, cancellationToken);
      return true;
    }

    public UniTask HideAllAsync(bool instant = false, CancellationToken cancellationToken = default)
    {
      var tasks = new List<UniTask>();
      foreach (WindowBase window in _attached)
      {
        if (window != null && window.IsVisible)
          tasks.Add(window.HideAsync(instant, cancellationToken));
      }

      return UniTask.WhenAll(tasks);
    }

    #endregion

    #region Spawn

    public async UniTask<TWindow> SpawnAsync<TWindow>(RectTransform parent = null, Vector2? anchoredPosition = null,
      CancellationToken cancellationToken = default) where TWindow : WindowBase
    {
      if (_provider == null)
      {
        WindowsLog.Error($"Cannot spawn '{typeof(TWindow).Name}': the service has no IWindowsProvider.");
        return null;
      }

      var prefab = await _provider.GetPrefabAsync(typeof(TWindow), cancellationToken) as TWindow;
      if (cancellationToken.IsCancellationRequested)
        return null;
      if (prefab == null)
      {
        WindowsLog.Error($"Cannot spawn '{typeof(TWindow).Name}': the provider has no prefab for it.");
        return null;
      }

      RectTransform targetParent = parent != null ? parent : DefaultParent;
      if (targetParent == null)
        WindowsLog.Warning($"'{typeof(TWindow).Name}' is spawned without a parent and may not be visible.");

      TWindow window = Object.Instantiate(prefab, targetParent, false);
      window.IsSpawned = true;
      if (anchoredPosition.HasValue)
        window.MoveTo(anchoredPosition.Value);

      // Inactive prefabs skip Awake; initialization attaches the window through the tracker.
      window.Initialize();
      return window;
    }

    public async UniTask<TWindow> OpenAsync<TWindow>(RectTransform parent = null, Vector2? anchoredPosition = null,
      bool instant = false, CancellationToken cancellationToken = default) where TWindow : WindowBase
    {
      TWindow window = await SpawnAsync<TWindow>(parent, anchoredPosition, cancellationToken);
      if (window != null && !cancellationToken.IsCancellationRequested)
        await window.ShowAsync(instant, cancellationToken);
      return window;
    }

    #endregion

    public void Dispose()
    {
      if (_disposed)
        return;

      _disposed = true;
      ActiveServices.Remove(this);
      WindowTracker.Added -= Attach;
      WindowTracker.Removed -= Detach;
      SceneManager.sceneLoaded -= OnSceneLoaded;
      SceneManager.sceneUnloaded -= OnSceneUnloaded;

      Queue.Dispose();
      foreach (WindowBase window in _attached)
      {
        if (window == null)
          continue;

        window.StateChanged -= OnWindowStateChanged;
        if (window.Service == this)
          window.Service = null;
      }

      _attached.Clear();
      _windows.Clear();
    }

    private void Attach(WindowBase window)
    {
      if (_disposed || window == null || _attached.Contains(window))
        return;

      if (!window.IsInitialized)
      {
        // Initialization adds the window to the tracker, which calls Attach again.
        window.Initialize();
        return;
      }

      _attached.Add(window);
      window.Service = this;
      window.StateChanged += OnWindowStateChanged;
      TryRegister(window);
    }

    private void Detach(WindowBase window)
    {
      if (!_attached.Remove(window))
        return;

      window.StateChanged -= OnWindowStateChanged;
      Queue.Remove(window);

      Type type = window.GetType();
      if (!_windows.TryGetValue(type, out WindowBase registered) || !ReferenceEquals(registered, window))
        return;

      _windows.Remove(type);
      WindowsLog.Info($"Unregistered '{type.Name}'.");
      WindowUnregistered?.Invoke(window);

      // Promote another live instance of the same type, if any.
      foreach (WindowBase other in _attached)
      {
        if (other != null && other.GetType() == type)
        {
          TryRegister(other);
          break;
        }
      }
    }

    private void TryRegister(WindowBase window)
    {
      Type type = window.GetType();
      if (_windows.TryGetValue(type, out WindowBase existing) && existing != null && existing != window)
      {
        WindowsLog.Info($"'{type.Name}' is already registered by '{existing.name}'. " +
                        $"'{window.name}' is attached but not reachable by type.", window);
        return;
      }

      _windows[type] = window;
      WindowsLog.Info($"Registered '{type.Name}'.", window);
      WindowRegistered?.Invoke(window);
    }

    private WindowBase GetOrLog(Type windowType)
    {
      WindowBase window = Get(windowType);
      if (window == null)
        WindowsLog.Warning($"No registered window of type '{windowType?.Name}'.");
      return window;
    }

    private void OnWindowStateChanged(IWindow window) => WindowStateChanged?.Invoke((WindowBase)window);

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => InitializeInactiveWindows(scene);

    private void OnSceneUnloaded(Scene scene) => WindowTracker.PurgeDestroyed();

    /// <summary>Windows on inactive objects never run Awake, so initialize them explicitly.</summary>
    private static void InitializeInactiveWindows(Scene scene)
    {
      if (!scene.isLoaded)
        return;

      foreach (GameObject root in scene.GetRootGameObjects())
      {
        foreach (WindowBase window in root.GetComponentsInChildren<WindowBase>(true))
          window.Initialize();
      }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => ActiveServices.Clear();
  }
}
