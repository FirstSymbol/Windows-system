using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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
    public UniTask<TWindow> OpenWindow<TWindow>(Vector2 anchoredPosition, RectTransform parent, bool disableShowHide = true)
      where TWindow : MonoBehaviour, IWindowBase;
    public bool CloseWindow(Type type);
    public bool CloseWindow<T>() where T : IWindowBase;
    
    public UniTask<bool> ShowWindow(Type type);
    public UniTask<bool> HideWindow(Type type);
    public bool ToggleWindow(Type type);
    public UniTask<bool> ShowWindow<T>() where T : IWindowBase;
    public UniTask<bool> HideWindow<T>() where T : IWindowBase;
    public bool ToggleWindow<T>() where T : IWindowBase;
  }
}