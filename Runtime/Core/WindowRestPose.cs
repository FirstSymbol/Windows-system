using System;
using UnityEngine;

namespace WindowsSystem
{
  /// <summary>
  /// Transform values of a fully shown window. Animations use it as the "shown" end point.
  /// </summary>
  [Serializable]
  public struct WindowRestPose
  {
    public Vector2 anchoredPosition;
    public Vector3 localPosition;
    public Vector3 localScale;

    public static WindowRestPose Capture(Transform transform)
    {
      return new WindowRestPose
      {
        anchoredPosition = transform is RectTransform rect ? rect.anchoredPosition : Vector2.zero,
        localPosition = transform.localPosition,
        localScale = transform.localScale
      };
    }
  }
}
