using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor.PackageManager.UI;
using UnityEngine;

namespace WindowsSystem
{
  public interface IWindowsService
  {
    public Dictionary<Type, IWindowBase> Windows { get; }
    public WindowsQueueController QueueController { get; }
    public HashSet<Type> ShownWindows { get; }
    
    public void RegisterWindow<T>(WindowBase<T> windowType) where T : IWindowBase;
    public void UnregisterWindow<T>(WindowBase<T> windowType) where T : IWindowBase;
    
    public IWindowBase GetWindow(Type type);
    public TWindow GetWindow<TWindow>() where TWindow : class, IWindowBase;
    public bool TryGetWindow(Type type, out IWindowBase window);
    public bool TryGetWindow(Type type, out IPooledWindow window);
    public bool TryGetWindow<TWindow>(out TWindow window) where TWindow : class, IWindowBase;
    public bool ExistWindow(Type type);
    
    public TWindow SpawnWindow<TWindow>(Vector2 anchoredPosition, RectTransform parent)
      where TWindow : MonoBehaviour, IWindowBase;
    public TWindow OpenWindow<TWindow>(Vector2 anchoredPosition, RectTransform parent)
      where TWindow : MonoBehaviour, IWindowBase;
    public bool CloseWindow(Type type);
    public bool CloseWindow<T>() where T : IWindowBase;
    
    public Task<bool> ShowWindow(Type type);
    public Task<bool> HideWindow(Type type);
    public bool ToggleWindow(Type type);
    public Task<bool> ShowWindow<T>() where T : IWindowBase;
    public Task<bool> HideWindow<T>() where T : IWindowBase;
    public bool ToggleWindow<T>() where T : IWindowBase;
  }
}