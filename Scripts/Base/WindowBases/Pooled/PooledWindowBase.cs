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

    protected virtual void RemoveData(int value)
    {
      if (!InQueue)
      {
        WindowData.Remove(value);
        return;
      }
      Logger.Warn("You cannot delete data from a window if the window is in the queue!", WSLogTag.WindowsService);
    }
  }
}