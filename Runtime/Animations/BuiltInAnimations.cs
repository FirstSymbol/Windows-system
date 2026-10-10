using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WindowsSystem.Animations
{
  /// <summary>Fades the window <see cref="CanvasGroup"/>.</summary>
  [Serializable, DisplayName("Fade")]
  public sealed class FadeAnimation : TweenWindowAnimation
  {
    [NonSerialized] private float _from;
    [NonSerialized] private float _to;

    protected override void Begin(in WindowAnimationContext context)
    {
      CanvasGroup group = context.Window.CanvasGroup;
      if (group == null)
      {
        WindowsLog.Warning($"'{context.Window.name}' uses a Fade animation but has no CanvasGroup.", context.Window);
        return;
      }

      _from = context.StartsHidden ? 0f : group.alpha;
      _to = context.IsShow ? 1f : 0f;
    }

    protected override void Evaluate(in WindowAnimationContext context, float t)
    {
      CanvasGroup group = context.Window.CanvasGroup;
      if (group != null)
        group.alpha = Mathf.LerpUnclamped(_from, _to, Mathf.Clamp01(t));
    }
  }

  /// <summary>Scales the window between its rest scale and a hidden scale.</summary>
  [Serializable, DisplayName("Scale")]
  public sealed class ScaleAnimation : TweenWindowAnimation
  {
    [Tooltip("Scale multiplier of the hidden window.")]
    [Min(0f)] public float hiddenScale = 0.85f;

    [NonSerialized] private Vector3 _from;
    [NonSerialized] private Vector3 _to;

    public ScaleAnimation()
    {
      ease = Ease.OutBack;
    }

    protected override void Begin(in WindowAnimationContext context)
    {
      Vector3 rest = context.Window.RestPose.localScale;
      Vector3 hidden = rest * hiddenScale;
      _from = context.StartsHidden ? hidden : context.Window.transform.localScale;
      _to = context.IsShow ? rest : hidden;
    }

    protected override void Evaluate(in WindowAnimationContext context, float t) =>
      context.Window.transform.localScale = Vector3.LerpUnclamped(_from, _to, t);
  }

  public enum SlideEdge
  {
    Left,
    Right,
    Top,
    Bottom
  }

  /// <summary>Slides the window in from (and out to) a screen edge.</summary>
  [Serializable, DisplayName("Slide")]
  public sealed class SlideAnimation : TweenWindowAnimation
  {
    public SlideEdge edge = SlideEdge.Bottom;

    [Tooltip("Slide distance in pixels. 0 = size of the parent rect along the slide axis.")]
    [Min(0f)] public float distance;

    [NonSerialized] private Vector2 _from;
    [NonSerialized] private Vector2 _to;

    protected override void Begin(in WindowAnimationContext context)
    {
      Vector2 rest = GetRestPosition(context.Window);
      Vector2 hidden = rest + GetOffset(context.Window);
      _from = context.StartsHidden ? hidden : GetPosition(context.Window);
      _to = context.IsShow ? rest : hidden;
    }

    protected override void Evaluate(in WindowAnimationContext context, float t)
    {
      Vector2 position = Vector2.LerpUnclamped(_from, _to, t);
      if (context.Window.RectTransform != null)
        context.Window.RectTransform.anchoredPosition = position;
      else
        context.Window.transform.localPosition = new Vector3(position.x, position.y, context.Window.transform.localPosition.z);
    }

    private static Vector2 GetRestPosition(WindowBase window) =>
      window.RectTransform != null ? window.RestPose.anchoredPosition : (Vector2)window.RestPose.localPosition;

    private static Vector2 GetPosition(WindowBase window) =>
      window.RectTransform != null ? window.RectTransform.anchoredPosition : (Vector2)window.transform.localPosition;

    private Vector2 GetOffset(WindowBase window)
    {
      Vector2 direction = edge switch
      {
        SlideEdge.Left => Vector2.left,
        SlideEdge.Right => Vector2.right,
        SlideEdge.Top => Vector2.up,
        _ => Vector2.down
      };

      float length = distance;
      if (length <= 0f)
      {
        Vector2 size = window.transform.parent is RectTransform parent
          ? parent.rect.size
          : new Vector2(Screen.width, Screen.height);
        length = Mathf.Abs(direction.x) > 0f ? size.x : size.y;
      }

      return direction * length;
    }
  }

  /// <summary>Plays several animations at the same time.</summary>
  [Serializable, DisplayName("Parallel")]
  public sealed class ParallelAnimation : WindowAnimation
  {
    [SerializeReference, SubclassSelector] public List<WindowAnimation> animations = new();

    public override UniTask Play(WindowAnimationContext context, CancellationToken cancellationToken)
    {
      if (animations == null || animations.Count == 0)
        return UniTask.CompletedTask;

      var tasks = new List<UniTask>(animations.Count);
      foreach (WindowAnimation animation in animations)
      {
        if (animation != null && animation != this)
          tasks.Add(animation.Play(context, cancellationToken));
      }

      return UniTask.WhenAll(tasks);
    }
  }
}
