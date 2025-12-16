using System;
using System.Collections.Generic;
using System.Linq;
using ModestTree;

namespace WindowsSystem
{
  public class WindowsQueue
  {
    private readonly Dictionary<IWindowBase, int> _existWindows = new();
    private readonly Dictionary<int, Queue<QueueWindowItem>> _queueItems = new();
    private readonly IWindowsService WindowsService;
    private int _currentQueueIndex;

    public WindowsQueue(IWindowsService windowsService)
    {
      WindowsService = windowsService;
    }

    public Action OnQueueFinished { get; set; }
    public Action OnQueueRunned { get; set; }
    public bool IsRunning { get; private set; }

    public bool AddWindowByTypeIn(Type type)
    {
      if (WindowsService.TryGetWindow(type, out IPooledWindow pooledWindow))
        return AddWindowItemIn(new QueueWindowItem(pooledWindow));
      if (WindowsService.TryGetWindow(type, out IWindowBase nativeWindow))
        return AddWindowItemIn(new QueueWindowItem(nativeWindow));
      return false;
    }

    public bool AddWindowItemIn(in QueueWindowItem windowItem)
    {
      ValidatePriority(windowItem.WindowBase.QueuePriority);
      
      bool isExists = ExistWindowInQueue(windowItem.WindowBase);
      if (!windowItem.isPooled && isExists)
        return false;
      if (windowItem.isPooled && isExists)
        _existWindows[windowItem.WindowBase] += 1;
      else
        _existWindows.Add(windowItem.WindowBase, 1);
      
      _queueItems[windowItem.WindowBase.QueuePriority].Enqueue(windowItem);
      windowItem.WindowBase.InQueue = true;
      if (IsRunning)
        UpdateQueue(windowItem.WindowBase.QueuePriority);
      else
        WindowsService.QueueController.RunQueue();
      return true;
    }

    public bool ExistWindowInQueue(IWindowBase window)
    {
      return _existWindows.ContainsKey(window);
    }

    public void ValidatePriority(int priority)
    {
      if (!_queueItems.ContainsKey(priority))
        _queueItems.Add(priority, new Queue<QueueWindowItem>());
    }

    public async void UpdateQueue(int priority)
    {
      if (_currentQueueIndex < priority)
      {
        UnSubscribeWindow();
        await _queueItems[_currentQueueIndex].Peek().WindowBase.Hide();
        _currentQueueIndex = priority;
        Start();
      }
    }

    public void GoToNextPriorityGroup()
    {
      _queueItems.Remove(_currentQueueIndex);
      if (_queueItems.Keys.Count == 0)
      {
        Stop();
      }
      else
      {
        _currentQueueIndex = _queueItems.Keys.Max();
        Start();
      }
    }

    public bool Run()
    {
      _currentQueueIndex = _queueItems.Keys.Max();
      Start();
      IsRunning = true;
      OnQueueRunned?.Invoke();
      return true;
    }

    private async void Start()
    {
      SubscribeWindow();
      _queueItems[_currentQueueIndex].Peek().Init.Invoke();
      await _queueItems[_currentQueueIndex].Peek().WindowBase.Show();
    }

    public async void Next(Type type = null)
    {
      UnSubscribeWindow();
      RemoveCurrentItem();
      if (_queueItems[_currentQueueIndex].IsEmpty())
      {
        GoToNextPriorityGroup();
        return;
      }

      SubscribeWindow();
      var queueItem = _queueItems[_currentQueueIndex].Peek();
      queueItem.Init.Invoke();
      await queueItem.WindowBase.Show();
      queueItem.WindowBase.InQueue = false;
    }

    private void RemoveCurrentItem()
    {
      var oldItem = _queueItems[_currentQueueIndex].Dequeue();
      _existWindows[oldItem.WindowBase] -= 1;
      oldItem.Resolve.Invoke();
      if (_existWindows[oldItem.WindowBase] > 0)
        return;
      _existWindows.Remove(oldItem.WindowBase);
    }

    public void Stop()
    {
      Clear();
      OnQueueFinished?.Invoke();
    }

    private void Clear()
    {
      _currentQueueIndex = 0;
      _queueItems.Clear();
      IsRunning = false;
    }

    private void SubscribeWindow()
    {
      _queueItems[_currentQueueIndex].Peek().WindowBase.OnAfterHide += Next;
    }

    private void UnSubscribeWindow()
    {
      _queueItems[_currentQueueIndex].Peek().WindowBase.OnAfterHide -= Next;
    }
  }
}