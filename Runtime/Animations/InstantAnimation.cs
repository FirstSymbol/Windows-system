using System;
using System.ComponentModel;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace WindowsSystem.Animations
{
  /// <summary>Shows or hides the window immediately.</summary>
  [Serializable, DisplayName("Instant")]
  public sealed class InstantAnimation : WindowAnimation
  {
    public override UniTask Play(WindowAnimationContext context, CancellationToken cancellationToken)
    {
      // Restore the alpha a previous fade may have left behind.
      if (context.IsShow && context.Window.CanvasGroup != null)
        context.Window.CanvasGroup.alpha = 1f;

      return UniTask.CompletedTask;
    }
  }
}
