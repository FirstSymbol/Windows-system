using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace WindowsSystem
{
  /// <summary>
  /// Minimal window contract used by the queue. Implemented by <see cref="WindowBase"/>.
  /// </summary>
  public interface IWindow
  {
    WindowState State { get; }

    /// <summary>False once the window has been destroyed.</summary>
    bool IsAlive { get; }

    int QueuePriority { get; }

    event Action<IWindow> StateChanged;

    UniTask ShowAsync(bool instant = false, CancellationToken cancellationToken = default);
    UniTask HideAsync(bool instant = false, CancellationToken cancellationToken = default);
  }
}
