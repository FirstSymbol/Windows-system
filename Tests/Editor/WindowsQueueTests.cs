using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace WindowsSystem.Tests
{
  public sealed class WindowsQueueTests
  {
    private sealed class FakeWindow : IWindow
    {
      private readonly List<string> _log;

      public FakeWindow(string name, List<string> log, int priority = 0)
      {
        Name = name;
        _log = log;
        QueuePriority = priority;
      }

      public string Name { get; }
      public WindowState State { get; private set; } = WindowState.Hidden;
      public bool IsAlive { get; set; } = true;
      public int QueuePriority { get; }
      public event Action<IWindow> StateChanged;

      public UniTask ShowAsync(bool instant = false, CancellationToken cancellationToken = default)
      {
        if (State != WindowState.Shown)
          _log.Add($"show {Name}");
        SetState(WindowState.Shown);
        return UniTask.CompletedTask;
      }

      public UniTask HideAsync(bool instant = false, CancellationToken cancellationToken = default)
      {
        SetState(WindowState.Hidden);
        return UniTask.CompletedTask;
      }

      public void Destroy()
      {
        IsAlive = false;
        SetState(WindowState.Hidden);
      }

      private void SetState(WindowState state)
      {
        if (State == state)
          return;
        State = state;
        StateChanged?.Invoke(this);
      }
    }

    private List<string> _log;
    private WindowsQueue _queue;

    [SetUp]
    public void SetUp()
    {
      _log = new List<string>();
      _queue = new WindowsQueue();
    }

    [TearDown]
    public void TearDown() => _queue.Dispose();

    [Test]
    public void ShowsWindowsOneByOne()
    {
      var a = new FakeWindow("A", _log);
      var b = new FakeWindow("B", _log);

      _queue.Enqueue(a);
      _queue.Enqueue(b);
      CollectionAssert.AreEqual(new[] { "show A" }, _log);

      a.HideAsync().Forget();
      b.HideAsync().Forget();

      CollectionAssert.AreEqual(new[] { "show A", "show B" }, _log);
      Assert.IsFalse(_queue.IsRunning);
    }

    [Test]
    public void HigherPriorityGoesFirstAndEqualPriorityIsFifo()
    {
      var first = new FakeWindow("First", _log);
      var low = new FakeWindow("Low", _log, 0);
      var high = new FakeWindow("High", _log, 10);
      var low2 = new FakeWindow("Low2", _log, 0);

      _queue.Enqueue(first, 20);
      _queue.Enqueue(low);
      _queue.Enqueue(high);
      _queue.Enqueue(low2);

      first.HideAsync().Forget();
      high.HideAsync().Forget();
      low.HideAsync().Forget();
      low2.HideAsync().Forget();

      CollectionAssert.AreEqual(new[] { "show First", "show High", "show Low", "show Low2" }, _log);
    }

    [Test]
    public void PlainWindowCannotBeQueuedTwice()
    {
      var a = new FakeWindow("A", _log);

      Assert.IsTrue(_queue.Enqueue(a));
      Assert.IsFalse(_queue.Enqueue(a));
      Assert.AreEqual(1, _queue.Count);
    }

    [Test]
    public void HigherPriorityPreemptsCurrentWhichIsShownAgainLater()
    {
      var low = new FakeWindow("Low", _log);
      var other = new FakeWindow("Other", _log);
      var urgent = new FakeWindow("Urgent", _log);

      _queue.Enqueue(low, 0);
      _queue.Enqueue(other, 0);
      _queue.Enqueue(urgent, 5);

      Assert.AreSame(urgent, _queue.Current.Window);
      Assert.AreEqual(WindowState.Hidden, low.State);
      CollectionAssert.AreEqual(new IWindow[] { low, other }, new[] { _queue.Pending[0].Window, _queue.Pending[1].Window });

      urgent.HideAsync().Forget();
      low.HideAsync().Forget();
      other.HideAsync().Forget();

      CollectionAssert.AreEqual(new[] { "show Low", "show Urgent", "show Low", "show Other" }, _log);
    }

    [Test]
    public void DataRequestsKeepTheirOwnDataWhenPreempted()
    {
      var window = new FakeWindow("Data", _log);
      var bound = new List<string>();

      void Enqueue(string data, int priority) =>
        _queue.Enqueue(window, () => bound.Add(data), data, priority);

      Enqueue("low", 0);
      Enqueue("low2", 0);
      Enqueue("high", 5);

      for (int i = 0; i < 3; i++)
        window.HideAsync().Forget();

      CollectionAssert.AreEqual(new[] { "low", "high", "low", "low2" }, bound);
      Assert.IsFalse(_queue.IsRunning);
    }

    [Test]
    public void DestroyedCurrentWindowLetsQueueContinue()
    {
      var a = new FakeWindow("A", _log);
      var b = new FakeWindow("B", _log);

      _queue.Enqueue(a);
      _queue.Enqueue(b);
      a.Destroy();

      Assert.AreSame(b, _queue.Current.Window);
    }

    [Test]
    public void ClearKeepsCurrentAndStopHidesIt()
    {
      var a = new FakeWindow("A", _log);
      var b = new FakeWindow("B", _log);

      _queue.Enqueue(a);
      _queue.Enqueue(b);
      _queue.Clear();
      Assert.AreEqual(0, _queue.Pending.Count);
      Assert.AreSame(a, _queue.Current.Window);

      _queue.Stop();
      Assert.AreEqual(WindowState.Hidden, a.State);
      Assert.IsFalse(_queue.IsRunning);
    }

    [Test]
    public void EventsAreRaised()
    {
      var a = new FakeWindow("A", _log);
      int started = 0, completed = 0, items = 0;
      _queue.Started += () => started++;
      _queue.Completed += () => completed++;
      _queue.ItemCompleted += _ => items++;

      _queue.Enqueue(a);
      a.HideAsync().Forget();

      Assert.AreEqual(1, started);
      Assert.AreEqual(1, completed);
      Assert.AreEqual(1, items);
    }
  }
}
