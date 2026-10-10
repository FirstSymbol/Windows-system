using UnityEngine;

namespace WindowsSystem.Animations
{
  public enum Ease
  {
    Linear,
    InQuad,
    OutQuad,
    InOutQuad,
    InCubic,
    OutCubic,
    InOutCubic,
    InBack,
    OutBack,
    InOutBack,
    OutElastic,
    OutBounce,
    Custom
  }

  public static class Easing
  {
    private const float BackOvershoot = 1.70158f;

    public static float Evaluate(Ease ease, float t, AnimationCurve customCurve = null)
    {
      t = Mathf.Clamp01(t);
      switch (ease)
      {
        case Ease.Linear: return t;
        case Ease.InQuad: return t * t;
        case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
        case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        case Ease.InCubic: return t * t * t;
        case Ease.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
        case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        case Ease.InBack: return (BackOvershoot + 1f) * t * t * t - BackOvershoot * t * t;
        case Ease.OutBack:
        {
          float p = t - 1f;
          return 1f + (BackOvershoot + 1f) * p * p * p + BackOvershoot * p * p;
        }
        case Ease.InOutBack:
        {
          const float c = BackOvershoot * 1.525f;
          return t < 0.5f
            ? Mathf.Pow(2f * t, 2f) * ((c + 1f) * 2f * t - c) / 2f
            : (Mathf.Pow(2f * t - 2f, 2f) * ((c + 1f) * (t * 2f - 2f) + c) + 2f) / 2f;
        }
        case Ease.OutElastic:
        {
          if (t <= 0f || t >= 1f) return t;
          const float c = 2f * Mathf.PI / 3f;
          return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c) + 1f;
        }
        case Ease.OutBounce: return OutBounce(t);
        case Ease.Custom: return customCurve != null ? customCurve.Evaluate(t) : t;
        default: return t;
      }
    }

    private static float OutBounce(float t)
    {
      const float n = 7.5625f;
      const float d = 2.75f;
      if (t < 1f / d) return n * t * t;
      if (t < 2f / d) return n * (t -= 1.5f / d) * t + 0.75f;
      if (t < 2.5f / d) return n * (t -= 2.25f / d) * t + 0.9375f;
      return n * (t -= 2.625f / d) * t + 0.984375f;
    }
  }
}
