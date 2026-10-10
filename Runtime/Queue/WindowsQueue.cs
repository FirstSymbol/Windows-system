using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace WindowsSystem
{
  /// <summary>
  /// Shows windows one after another. A window stays current until it is hidden, then the next request
  /// with the highest priority is shown (FIFO within the same priority). A request with a higher priority
  /// than the current one hides the current window and puts it back at the front of its priority group.
  /// </summary>
  public sealed class WindowsQueue : IDisposable
  {
    public sealed class Request
    {
      internal readonly Action Prepare;

      internal Request(IWindow window, int priority, Action prepare, object data)
      {
        Window = window;
        Priority = priority;
        Prepare = prepare;
        Data = data;
      }

      public IWindow Window { get; }
      public int Priority { get; }

      /// <summary>Payload of a <see cref="DataWindowBase{TData}"/> request; null for plain requests.</summary>
      public object Data { get; }

      public bool HasData => Prepare != null;
    }

    private readonly List<Request> _pending = new();
    private readonly Func<Type, IWindow> _resolver;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private CancellationTokenSource _preemptCts;
    private bool _disposed;

    /// <param name="resolver">Resolves a window by type for <see cref="Enqueue(Type, int?)"/>.</param>
    public WindowsQueue(Func<Type, IWindow> resolver = null)
    {
      _resolver = resolver;
    }

    public bool IsRunning { get; private set; }
    public Request Current { get; private set; }
    public IReadOnlyList<Request> Pending => _pending;
    public int Count => _pending.Count + (Current != null ? 1 : 0);

    public event Action Started;
    public event Action Completed;
    public event Action<Request> ItemStarted;
    public event Action<Request> ItemCompleted;

    /// <summary>Raised whenever the current request or the pending list changes.</summary>
    public event Action Changed;

    /// <summary>Queues a window. A window can be queued only once unless it is queued with data.</summary>
    public bool Enqueue(IWindow window, int? priority = null) =>
      Add(window, priority, null, null, allowDuplicates: false);

    /// <summary>Queues a data window. Every request keeps its own data.</summary>
    public bool Enqueue<TData>(DataWindowBase<TData> window, TData data, int? priority = null) =>
      Add(window, priority, () => window.SetData(data), data, allowDuplicates: true);

    /// <summary>Queues a window with a callback invoked right before it is shown.</summary>
    internal bool Enqueue(IWindow window, Action prepare, object data, int? priority = null) =>
      Add(window, priority, prepare, data, allowDuplicates: true);

    public bool Enqueue<TWindow>(int? priority = null) where TWindow : WindowBase =>
      Enqueue(typeof(TWindow), priority);

    public bool Enqueue(Type windowType, int? priority = null)
    {
      IWindow window = _resolver?.Invoke(windowType);
      if (window == null)
      {
        WindowsLog.Error($"Cannot queue '{windowType?.Name}': no registered window of this type.");
        return false;
      }

      return Enqueue(window, priority);
    }

    /// <summary>Queues several windows by type. Returns the number of queued windows.</summary>
    public int EnqueueRange(IEnumerable<Type> windowTypes)
    {
      int count = 0;
      foreach (Type type in windowTypes)
      {
        if (Enqueue(type))
          count++;
      }

      return count;
    }

    /// <summary>True if the window is current or pending.</summary>
    public bool Contains(IWindow window)
    {
      if (window == null)
        return false;
      if (Current != null && Current.Window == window)
        return true;

      foreach (Request request in _pending)
      {
        if (request.Window == window)
          return true;
      }

      return false;
    }

    /// <summary>Removes pending requests of the window. The current request is not affected.</summary>
    public int Remove(IWindow window)
    {
      int removed = _pending.RemoveAll(request => request.Window == window);
      if (removed > 0)
        Changed?.Invoke();
      return removed;
    }

    /// <summary>Removes all pending requests. The current window stays shown.</summary>
    public void Clear()
    {
      if (_pending.Count == 0)
        return;

      _pending.Clear();
      Changed?.Invoke();
    }

    /// <summary>Hides the current window so the queue moves on.</summary>
    public void Skip()
    {
      if (Current != null && Current.Window.IsAlive)
        Current.Window.HideAsync().Forget();
    }

    /// <summary>Clears pending requests and hides the current window.</summary>
    public void Stop()
    {
      Clear();
      Skip();
    }

    public void Dispose()
    {
      if (_disposed)
        return;

      _disposed = true;
      _pending.Clear();
      _lifetimeCts.Cancel();
      _lifetimeCts.Dispose();
    }

    private bool Add(IWindow window, int? priority, Action prepare, object data, bool allowDuplicates)
    {
      if (_disposed || window == null || !window.IsAlive)
        return false;

      if (!allowDuplicates && Contains(window))
      {
        WindowsLog.Warning($"'{GetName(window)}' is already in the queue.");
        return false;
      }

      var request = new Request(window, priority ?? window.QueuePriority, prepare, data);
      Insert(request, beforeEqualPriority: false);
      WindowsLog.Info($"Queued '{GetName(window)}' (priority {request.Priority}).");

      if (!IsRunning)
        RunAsync().Forget();
      else if (Current != null && request.Priority > Current.Priority)
        _preemptCts?.Cancel();

      return true;
    }

    private void Insert(Request request, bool beforeEqualPriority)
    {
      int index = _pending.Count;
      for (int i = 0; i < _pending.Count; i++)
      {
        int other = _pending[i].Priority;
        if (other < request.Priority || (beforeEqualPriority && other == request.Priority))
        {
          index = i;
          break;
        }
      }

      _pending.Insert(index, request);
      Changed?.Invoke();
    }

    private async UniTaskVoid RunAsync()
    {
      IsRunning = true;
      Started?.Invoke();

      try
      {
        while (!_disposed && _pending.Count > 0)
        {
          Request request = _pending[0];
          _pending.RemoveAt(0);
          if (!request.Window.IsAlive)
            continue;

          Current = request;
          Changed?.Invoke();
          ItemStarted?.Invoke(request);

          bool preempted = await ProcessAsync(request);

          Current = null;
          if (preempted)
            Insert(request, beforeEqualPriority: true);
          else
          {
            ItemCompleted?.Invoke(request);
            Changed?.Invoke();
          }
        }
      }
      catch (OperationCanceledException)
      {
        // The queue was disposed.
      }
      catch (Exception exception)
      {
        WindowsLog.Error($"Windows queue failed: {exception}");
      }
      finally
      {
        Current = null;
        IsRunning = false;
        if (!_disposed)
        {
          Changed?.Invoke();
          Completed?.Invoke();
        }
      }
    }

    /// <returns>True if the request was preempted and must be shown again later.</returns>
    private async UniTask<bool> ProcessAsync(Request request)
    {
      CancellationToken lifetime = _lifetimeCts.Token;
      _preemptCts = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
      try
      {
        request.Prepare?.Invoke();
        await request.Window.ShowAsync(false, lifetime);

        bool hidden = await WaitUntilHidden(request.Window, _preemptCts.Token);
        if (hidden || lifetime.IsCancellationRequested)
          return false;

        WindowsLog.Info($"'{GetName(request.Window)}' was preempted by a request with a higher priority.");
        if (request.Window.IsAlive)
          await request.Window.HideAsync(false, lifetime);
        return true;
      }
      finally
      {
        _preemptCts.Dispose();
        _preemptCts = null;
      }
    }

    /// <returns>True once the window is hidden or destroyed, false if the wait was cancelled first.</returns>
    /// <remarks>Completes with a result instead of throwing: cancellation raised synchronously from a
    /// completion source callback is not reliably propagated to awaiting UniTask methods.</remarks>
    private static async UniTask<bool> WaitUntilHidden(IWindow window, CancellationToken cancellationToken)
    {
      if (!window.IsAlive || window.State == WindowState.Hidden)
        return true;
      if (cancellationToken.IsCancellationRequested)
        return false;

      var source = new UniTaskCompletionSource<bool>();

      void OnStateChanged(IWindow changed)
      {
        if (!changed.IsAlive || changed.State == WindowState.Hidden)
          source.TrySetResult(true);
      }

      window.StateChanged += OnStateChanged;
      try
      {
        using (cancellationToken.Register(() => source.TrySetResult(false)))
          return await source.Task;
      }
      finally
      {
        window.StateChanged -= OnStateChanged;
      }
    }

    private static string GetName(IWindow window) => window.GetType().Name;
  }
}
