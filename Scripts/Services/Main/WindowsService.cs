using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using WindowsSystem.Log;
using WindowsSystem.Providers;
using WindowsSystem.Resolver;
using Logger = ExtDebugLogger.Logger;
using Object = UnityEngine.Object;

namespace WindowsSystem
{
  public class WindowsService : IWindowsService
  {
    public static IWindowsService Instance { get; private set; }
    public static event Action OnInitialize;
    public RectTransform defaultSpawnParent;
    private IWindowsProvider _windowsProvider;

    
    
    public WindowsService(IDependencyResolver resolver)
    {
      if (Instance != null)
      {
        throw new InvalidOperationException("Duplicate WindowsService detected!");
      }
      Instance = this;
      _windowsProvider = resolver.Resolve<IWindowsProvider>();
      QueueController = new WindowsQueueController(this);
      SceneManager.sceneUnloaded += SceneManagerOnsceneUnloaded;
      SceneManager.sceneLoaded += SceneManagerOnsceneLoaded;
      OnInitialize?.Invoke();
    }

    private void SceneManagerOnsceneUnloaded(Scene arg0)
    {
      foreach (var windowsKey in new List<Type>(Windows.Keys))
      {
        UnregisterWindow(windowsKey);
      }
    }

    ~WindowsService()
    {
      SceneManager.sceneUnloaded -= SceneManagerOnsceneUnloaded;
      SceneManager.sceneLoaded -= SceneManagerOnsceneLoaded;
    }

    private void SceneManagerOnsceneLoaded(Scene arg0, LoadSceneMode arg1)
    {
      foreach (var windowBaseInterlayer in Object.FindObjectsByType<WindowBaseInterlayer>(FindObjectsInactive.Include))
      {
        windowBaseInterlayer.Init();
      }
    }

    public Dictionary<Type, IWindowBase> Windows { get; } = new();
    public HashSet<Type> ShownWindows { get; } = new();
    public WindowsQueueController QueueController { get; }

    #region Infrastructure

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
      Instance = null;
    }

    #endregion
    
    #region Registering

    public void RegisterWindow(IWindowBase window)
    {
      if (!Windows.TryAdd(window.GetType(), window))
      {
        Logger.Error($"Window is already exist.",WSLogTag.WindowsService);
        return;
      }
      window.OnAfterHide += OnAfterWindowHide;
      window.OnAfterShow += OnAfterShow;
      Logger.Log($"Registering window of type {window.GetType().ToString().Split('.')[^1]}", WSLogTag.WindowsService);
      
    }
    
    public void RegisterWindow<T>(WindowBase<T> window) where T : IWindowBase
    {
      if (!Windows.TryAdd(typeof(T), window))
      {
        Logger.Error($"Window is already exist.",WSLogTag.WindowsService);
        return;
      }
      window.OnAfterHide += OnAfterWindowHide;
      window.OnAfterShow += OnAfterShow;
      Logger.Log($"Registering window of type {typeof(T).ToString().Split('.')[^1]}", WSLogTag.WindowsService);
    }

    public void UnregisterWindow(Type type)
    {
      if (type.GetInterface(nameof(IWindowBase)) ==  null)
        return;

      if (Windows.TryGetValue(type, out var window))
      {
        if (!Windows.Remove(type))
          return;
        window.OnAfterHide -= OnAfterWindowHide;
        window.OnAfterShow -= OnAfterShow;
        Logger.Log($"Unregistering window of type {type.ToString().Split('.')[^1]}", WSLogTag.WindowsService);
      }
      
    }
    
    public void UnregisterWindow<T>(WindowBase<T> window) where T : IWindowBase
    {
      UnregisterWindow(typeof(T));
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

      parent ??= defaultSpawnParent;
      
      var window = Object.Instantiate(windowPrefab, anchoredPosition, Quaternion.identity, parent);
      window.GetComponent<RectTransform>().anchoredPosition = anchoredPosition;
      window.IsSpawned = true;

      return window;
    }

    public async UniTask<TWindow> OpenWindow<TWindow>(Vector2 anchoredPosition, RectTransform parent, bool forceHideOnInit = true)
      where TWindow : MonoBehaviour, IWindowBase
    {
      var window = SpawnWindow<TWindow>(anchoredPosition, parent);
      window.gameObject.SetActive(false);

      if (window == null)
        return window;

      window.ForceHideOnInit = forceHideOnInit;
      await window.Hide(true);
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

    public async UniTask<bool> ShowWindow(Type type)
    {
      if (Windows.TryGetValue(type, out var window))
      {
        await window.Show();
        return true;
      }

      return false;
    }

    public async UniTask<bool> HideWindow(Type type)
    {
      if (Windows.TryGetValue(type, out var window))
      {
        await window.Hide();
        return true;
      }

      return false;
    }

    public UniTask<bool> ToggleWindow(Type type)
    {
      if (Windows.TryGetValue(type, out var window))
      {
        window.Toggle();
        return UniTask.FromResult(true);
      }

      return UniTask.FromResult(false);
    }

    public UniTask<bool> ShowWindow<T>() where T : IWindowBase
    {
      return ShowWindow(typeof(T));
    }

    public UniTask<bool> HideWindow<T>() where T : IWindowBase
    {
      return HideWindow(typeof(T));
    }

    public async UniTask<bool> ToggleWindow<T>() where T : IWindowBase
    {
      return await ToggleWindow(typeof(T));
    }

    #endregion
  }
}