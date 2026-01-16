using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Windows_system.Scripts.Providers;
using WindowsSystem.Log;
using Zenject;
using Logger = ExtDebugLogger.Logger;
using Object = UnityEngine.Object;

namespace WindowsSystem
{
    public class WindowsService : IWindowsService
    {
        private readonly Dictionary<Type, IWindowBase> _windows = new();
        public HashSet<Type> ShowedWindows { get; } = new();
        public WindowsQueueController QueueController { get; }

        [Inject] private IWindowsProvider _windowsProvider;
        
        public WindowsService()
        {
            QueueController = new WindowsQueueController(this);
        }

        public bool ExistWindow(Type type) =>
            _windows.ContainsKey(type);

        public TWindow GetWindow<TWindow>() where TWindow : class, IWindowBase
        {
            if (_windows.TryGetValue(typeof(TWindow), out var window))
                return window as TWindow;
            return null;
        }

        public IWindowBase GetWindow(Type type) =>
            _windows.GetValueOrDefault(type);

        public bool TryGetWindow<TWindow>(out TWindow window) where TWindow : class, IWindowBase
        {
            if (_windows.TryGetValue(typeof(TWindow), out var value))
            {
                window = value as TWindow;
                return true;
            }

            window = null;
            return false;
        }

        public bool TryGetWindow(Type type, out IWindowBase window) =>
            _windows.TryGetValue(type, out window);
        
        public bool TryGetWindow(Type type, out IPooledWindow window)
        {
            _windows.TryGetValue(type, out var nativeWindow);
            window = nativeWindow as IPooledWindow;
            return window != null;
        }

        public async Task<bool> ShowWindow(Type type)
        {
            if (_windows.TryGetValue(type, out var window))
            {
                await window.Show();
                return true;
            }
            return false;
        }

        public async Task<bool> HideWindow(Type type)
        {
            if (_windows.TryGetValue(type, out var window))
            {
                await window.Hide();
                return true;
            }
            return false;
        }

        public bool CloseWindow(Type type)
        {
            if (_windows.TryGetValue(type, out var window))
            {
                window.Close();
                return true;
            }
            return false;
        }

        public bool ToggleWindow(Type type)
        {
            if (_windows.TryGetValue(type, out var window))
            {
                window.Toggle();
                return true;
            }
            return false;
        }

        public TWindow OpenWindow<TWindow>(Vector2 anchoredPosition, RectTransform parent) where TWindow : MonoBehaviour, IWindowBase
        {
            TWindow window = SpawnWindow<TWindow>(anchoredPosition,  parent);
            
            if (window == null)
                return window;
            
            window.DisableShowHideActionsOnStart = true;
            window.Show().Forget();

            return window;
        }
        
        public TWindow SpawnWindow<TWindow>(Vector2 anchoredPosition, RectTransform parent) where TWindow : MonoBehaviour, IWindowBase
        {
            TWindow windowPrefab = _windowsProvider.GetWindowPrefab<TWindow>();
            if (windowPrefab == null)
            {
                Logger.Warn("Could not find window prefab in windowsProvider.", WSLogTag.WindowsService);
                return null;
            }
            
            TWindow window = Object.Instantiate(windowPrefab, anchoredPosition, Quaternion.identity, parent);
            window.IsSpawned = true;
            
            return window;
        }

        public Task<bool> ShowWindow<T>() where T : IWindowBase => ShowWindow(typeof(T));

        public Task<bool> HideWindow<T>() where T : IWindowBase => HideWindow(typeof(T));
        public bool CloseWindow<T>() where T : IWindowBase
        {
            return CloseWindow(typeof(T));
        }

        public bool ToggleWindow<T>() where T : IWindowBase => ToggleWindow(typeof(T));

        public void RegisterWindow<T>(WindowBase<T> window) where T : IWindowBase
        {
            _windows.Add(typeof(T), window);
            window.OnAfterHide += OnAfterWindowHide;
            window.OnAfterShow += OnAfterShow;
            Logger.Log($"Registering window of type {typeof(T).ToString().Split('.')[^1]}", WSLogTag.WindowsService);
        }

        public void UnregisterWindow<T>(WindowBase<T> window) where T : IWindowBase
        {
            _windows.Remove(typeof(T));
            window.OnAfterHide -= OnAfterWindowHide;
            window.OnAfterShow -= OnAfterShow;
            Logger.Log($"Unregistering window of type {typeof(T).ToString().Split('.')[^1]}", WSLogTag.WindowsService);
        }

        private void OnAfterShow(Type windowType) =>
            ShowedWindows.Add(windowType);

        private void OnAfterWindowHide(Type windowType) =>
            ShowedWindows.Remove(windowType);
    }
}