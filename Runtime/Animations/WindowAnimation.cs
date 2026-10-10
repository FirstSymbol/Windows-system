using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace WindowsSystem.Animations
{
  /// <summary>
  /// Show or hide animation of a window. Assigned through <c>[SerializeReference]</c>,
  /// so any serializable subclass appears in the inspector dropdown.
  /// </summary>
  /// <remarks>
  /// When <see cref="WindowAnimationContext.Instant"/> is true the animation must apply
  /// its final values synchronously.
  /// </remarks>
  [Serializable]
  public abstract class WindowAnimation
  {
    public abstract UniTask Play(WindowAnimationContext context, CancellationToken cancellationToken);
  }

  public readonly struct WindowAnimationContext
  {
    public readonly WindowBase Window;

    /// <summary>True for a show transition, false for a hide transition.</summary>
    public readonly bool IsShow;

    public readonly bool Instant;

    /// <summary>State of the window before the transition started.</summary>
    public readonly WindowState PreviousState;

    public WindowAnimationContext(WindowBase window, bool isShow, bool instant, WindowState previousState)
    {
      Window = window;
      IsShow = isShow;
      Instant = instant;
      PreviousState = previousState;
    }

    /// <summary>
    /// True when a show transition starts from a fully hidden window,
    /// so the animation should start from its hidden values instead of the current ones.
    /// </summary>
    public bool StartsHidden => IsShow && PreviousState == WindowState.Hidden;
  }
}
