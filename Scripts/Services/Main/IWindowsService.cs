using System;
using System.Collections.Generic;

namespace WindowsSystem
{
  public interface IWindowsService
  {
    public WindowsQueueController QueueController { get; }
    public HashSet<Type> ShowedWindows { get; }
    public void ShowWindow(Type type);
    public void HideWindow(Type type);
    public void ToggleWindow(Type type);
    public void ShowWindow<T>() where T : IWindowBase;
    public void HideWindow<T>() where T : IWindowBase;
    public void ToggleWindow<T>() where T : IWindowBase;
    public void RegisterWindow<T>(WindowBase<T> windowType) where T : IWindowBase;
    public void UnregisterWindow<T>(WindowBase<T> windowType) where T : IWindowBase;
    public TWindow GetWindow<TWindow>() where TWindow : class, IWindowBase;
    public IWindowBase GetWindow(Type type);
    public bool TryGetWindow<TWindow>(out TWindow window) where TWindow : class, IWindowBase;
    public bool TryGetWindow(Type type, out IWindowBase window);
    public bool TryGetWindow(Type type, out IPooledWindow window);
    public bool ExistWindow(Type type);
  }
}