using System.Collections.Generic;
using ExtDebugLogger;
using TriInspector;
using WindowsSystem.Log;

namespace WindowsSystem
{
  public abstract class PooledWindowBase<TWindow, TData> : WindowBase<TWindow>, IPooledWindow
    where TWindow : IWindowBase
    where TData : struct
  {
    protected abstract IDictionary<int, TData> WindowData { get; }
    protected abstract TData DefaultData { get; set; }

    protected abstract void ExtractAction(in TData data);

    [Button]
    public void ExtractData(int index = -1, bool removeAfterExtract = false)
    {
      if (WindowData.TryGetValue(index, out TData data))
      {
        ExtractAction(in data);
        if (removeAfterExtract) 
          RemoveData(index);
      }
    }
    
    public void ReturnToDefault() => ExtractAction(DefaultData);

    protected virtual void RemoveData(int value) => 
      WindowData.Remove(value);
  }
}