using System.Collections.Generic;
using TriInspector;

namespace WindowsSystem
{
  [DeclareFoldoutGroup("Pool Buttons")]
  public abstract class PooledWindowBase<TWindow, TData> : WindowBase<TWindow>, IPooledWindow
    where TWindow : IWindowBase
  {
    protected abstract IDictionary<int, TData> WindowData { get; }
    protected abstract TData DefaultData { get; set; }

    protected abstract void ExtractAction(in TData data);

    [Button]
    [Group("Pool Buttons")]
    public void ExtractData(int index = -1, bool removeAfterExtract = false)
    {
      if (WindowData.TryGetValue(index, out TData data))
      {
        ExtractAction(in data);
        if (removeAfterExtract) 
          RemoveData(index);
      }
    }
    
    [Button]
    [Group("Pool Buttons")]
    public void ReturnToDefault() => ExtractAction(DefaultData);
    
    [Button]
    [Group("Pool Buttons")]
    public void AddInQueue(int index, bool removeAfterExtract, bool returnToDefault = true, bool hideIfFirstEntry = true, bool hideForce = false)
    {
      WindowService.QueueController.AddWindowInQueue(this, index, removeAfterExtract,  returnToDefault, hideIfFirstEntry, hideForce);
    }

    protected virtual void RemoveData(int value) => 
      WindowData.Remove(value);
  }
}