using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WindowsSystem.Animations
{
  /// <summary>
  /// Base class for time-based animations. Implement <see cref="Begin"/> to capture start values
  /// and <see cref="Evaluate"/> to apply an eased progress in [0..1].
  /// </summary>
  [Serializable]
  public abstract class TweenWindowAnimation : WindowAnimation
  {
    [Min(0f)] public float duration = 0.25f;
    [Min(0f)] public float delay;
    public Ease ease = Ease.OutCubic;

    [Tooltip("Used when Ease is Custom.")]
    public AnimationCurve customCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Keep animating while Time.timeScale is 0 (paused game).")]
    public bool unscaledTime = true;

    public override async UniTask Play(WindowAnimationContext context, CancellationToken cancellationToken)
    {
      Begin(context);

      if (context.Instant)
      {
        Evaluate(context, 1f);
        return;
      }

      Evaluate(context, 0f);

      if (delay > 0f)
      {
        await UniTask.Delay(TimeSpan.FromSeconds(delay),
          unscaledTime ? DelayType.UnscaledDeltaTime : DelayType.DeltaTime,
          PlayerLoopTiming.Update, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
          return;
      }

      await WindowTween.Run(duration, unscaledTime, this, context, cancellationToken);
    }

    internal void Step(in WindowAnimationContext context, float progress) =>
      Evaluate(context, Easing.Evaluate(ease, progress, customCurve));

    /// <summary>Captures the start and end values of the transition.</summary>
    protected abstract void Begin(in WindowAnimationContext context);

    /// <summary>Applies the eased progress (may overshoot [0..1] for Back/Elastic eases).</summary>
    protected abstract void Evaluate(in WindowAnimationContext context, float t);
  }

  internal static class WindowTween
  {
    public static async UniTask Run(float duration, bool unscaledTime, TweenWindowAnimation animation,
      WindowAnimationContext context, CancellationToken cancellationToken)
    {
      if (duration <= 0f)
      {
        animation.Step(context, 1f);
        return;
      }

      float elapsed = 0f;
      while (elapsed < duration)
      {
        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

        // Checked explicitly: awaiting a cancelled UniTask must not be relied on to throw.
        if (cancellationToken.IsCancellationRequested)
          return;

        elapsed += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        animation.Step(context, Mathf.Clamp01(elapsed / duration));
      }
    }
  }
}
