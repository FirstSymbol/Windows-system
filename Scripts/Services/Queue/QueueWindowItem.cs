using System;
using ExtDebugLogger;
using WindowsSystem.Log;

namespace WindowsSystem
{
  public readonly struct QueueWindowItem
  {
    public readonly IWindowBase WindowBase;
    public readonly bool isPooled;
    
    public readonly Action Init;
    public readonly Action Resolve;
    public QueueWindowItem(IWindowBase window)
    {
      WindowBase = window;
      isPooled = false;
      Init = () => { };
      Resolve = () => { };
    }

    public QueueWindowItem(IPooledWindow window, int index, bool removeAfterExtract = false, bool returnToDefault = true)
    {
      WindowBase = window;
      isPooled = true;
      Init = () => {window.ExtractData(index, removeAfterExtract); Logger.Log("Init log", WSLogTag.WindowsQueueController);};
      Resolve = returnToDefault ? window.ReturnToDefault : () => {};
    }
  }
}