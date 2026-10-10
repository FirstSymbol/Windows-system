using System.Collections.Generic;
using UnityEngine;

namespace WindowsSystem.Layouts
{
  public enum WindowTilingMode
  {
    /// <summary>Side by side, equal widths.</summary>
    Horizontal,

    /// <summary>Stacked top to bottom, equal heights.</summary>
    Vertical,

    /// <summary>Rows and columns chosen for the area's proportions.</summary>
    Grid,

    /// <summary>The first window large on the left, the others stacked on the right.</summary>
    MainAndStack,

    /// <summary>Overlapping windows offset diagonally.</summary>
    Cascade,

    /// <summary>Every window centered at the same size.</summary>
    Center,

    /// <summary>Every window fills the area.</summary>
    Maximize
  }

  /// <summary>Pure layout math (no scene access), used at runtime and by editor previews.</summary>
  public static class WindowLayoutMath
  {
    /// <summary>Rect of a slot inside the area (both in the same coordinate space).</summary>
    public static Rect ComputeSlot(WindowSlotRect slot, Rect area)
    {
      Vector2 min = area.min + Vector2.Scale(slot.anchorMin, area.size);
      Vector2 max = area.min + Vector2.Scale(slot.anchorMax, area.size);
      var region = Rect.MinMaxRect(
        min.x + slot.padding.left, min.y + slot.padding.bottom,
        max.x - slot.padding.right, max.y - slot.padding.top);
      region.width = Mathf.Max(0f, region.width);
      region.height = Mathf.Max(0f, region.height);

      Vector2 size;
      switch (slot.sizeMode)
      {
        case WindowSlotSizeMode.Fixed:
          size = Vector2.Min(Vector2.Max(slot.size, Vector2.zero), region.size);
          break;
        case WindowSlotSizeMode.FitAspect:
        {
          float aspect = slot.size.y > 0f && slot.size.x > 0f ? slot.size.x / slot.size.y : 1f;
          Vector2 limit = region.size;
          if (slot.size.x > 0f && slot.size.y > 0f)
            limit = Vector2.Min(limit, slot.size);
          size = limit.x / aspect <= limit.y ? new Vector2(limit.x, limit.x / aspect) : new Vector2(limit.y * aspect, limit.y);
          break;
        }
        default:
          return region;
      }

      Vector2 position = region.min + Vector2.Scale(region.size - size, slot.alignment);
      return new Rect(position, size);
    }

    /// <summary>Fills <paramref name="results"/> with one rect per window for the tiling mode.</summary>
    public static void ComputeTiling(WindowTilingMode mode, int count, Rect area, float spacing, List<Rect> results)
    {
      results.Clear();
      if (count <= 0)
        return;

      spacing = Mathf.Max(0f, spacing);
      switch (mode)
      {
        case WindowTilingMode.Horizontal:
          Split(area, count, 1, spacing, results);
          break;
        case WindowTilingMode.Vertical:
          Split(area, 1, count, spacing, results);
          break;
        case WindowTilingMode.Grid:
        {
          float aspect = area.height > 0f ? area.width / area.height : 1f;
          int columns = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(count * aspect)), 1, count);
          int rows = Mathf.CeilToInt(count / (float)columns);
          columns = Mathf.CeilToInt(count / (float)rows);
          Split(area, columns, rows, spacing, results, count);
          break;
        }
        case WindowTilingMode.MainAndStack:
        {
          if (count == 1)
          {
            results.Add(area);
            break;
          }

          float mainWidth = (area.width - spacing) * 0.62f;
          results.Add(new Rect(area.x, area.y, mainWidth, area.height));
          var stack = new Rect(area.x + mainWidth + spacing, area.y, area.width - mainWidth - spacing, area.height);
          var stacked = new List<Rect>();
          Split(stack, 1, count - 1, spacing, stacked);
          results.AddRange(stacked);
          break;
        }
        case WindowTilingMode.Cascade:
        {
          Vector2 size = area.size * 0.6f;
          Vector2 step = count > 1 ? (area.size - size) / (count - 1) : Vector2.zero;
          step = Vector2.Min(step, new Vector2(48f, 48f));
          for (int i = 0; i < count; i++)
            results.Add(new Rect(area.x + step.x * i, area.yMax - size.y - step.y * i, size.x, size.y));
          break;
        }
        case WindowTilingMode.Center:
        {
          Vector2 size = area.size * 0.6f;
          for (int i = 0; i < count; i++)
            results.Add(new Rect(area.center - size / 2f, size));
          break;
        }
        default:
          for (int i = 0; i < count; i++)
            results.Add(area);
          break;
      }
    }

    /// <summary>Insets a rect on every side.</summary>
    public static Rect Inset(Rect rect, float amount) =>
      Rect.MinMaxRect(rect.xMin + amount, rect.yMin + amount,
        Mathf.Max(rect.xMin + amount, rect.xMax - amount), Mathf.Max(rect.yMin + amount, rect.yMax - amount));

    /// <summary>
    /// Places <paramref name="target"/> at <paramref name="rectInRoot"/> (root local space) by setting its anchors,
    /// so it keeps the place when the parent is resized.
    /// </summary>
    public static void ApplyRect(RectTransform target, RectTransform root, Rect rectInRoot)
    {
      GetAnchors(target, root, rectInRoot, out Vector2 anchorMin, out Vector2 anchorMax);
      SetAnchors(target, anchorMin, anchorMax);
    }

    public static void GetAnchors(RectTransform target, RectTransform root, Rect rectInRoot,
      out Vector2 anchorMin, out Vector2 anchorMax)
    {
      var parent = target.parent as RectTransform;
      if (parent == null)
        parent = root;

      Vector2 min = parent.InverseTransformPoint(root.TransformPoint(rectInRoot.min));
      Vector2 max = parent.InverseTransformPoint(root.TransformPoint(rectInRoot.max));
      Rect bounds = parent.rect;
      Vector2 size = new(Mathf.Max(bounds.width, 0.0001f), Mathf.Max(bounds.height, 0.0001f));
      anchorMin = (min - bounds.min) / size;
      anchorMax = (max - bounds.min) / size;
    }

    /// <summary>Current rect of the target expressed as anchors with zero offsets.</summary>
    public static void GetCurrentAnchors(RectTransform target, out Vector2 anchorMin, out Vector2 anchorMax)
    {
      var parent = target.parent as RectTransform;
      if (parent == null)
      {
        anchorMin = target.anchorMin;
        anchorMax = target.anchorMax;
        return;
      }

      Rect bounds = parent.rect;
      Vector2 size = new(Mathf.Max(bounds.width, 0.0001f), Mathf.Max(bounds.height, 0.0001f));
      Vector2 min = parent.InverseTransformPoint(target.TransformPoint(target.rect.min));
      Vector2 max = parent.InverseTransformPoint(target.TransformPoint(target.rect.max));
      anchorMin = (min - bounds.min) / size;
      anchorMax = (max - bounds.min) / size;
    }

    public static void SetAnchors(RectTransform target, Vector2 anchorMin, Vector2 anchorMax)
    {
      target.anchorMin = anchorMin;
      target.anchorMax = anchorMax;
      target.offsetMin = Vector2.zero;
      target.offsetMax = Vector2.zero;
    }

    private static void Split(Rect area, int columns, int rows, float spacing, List<Rect> results, int limit = int.MaxValue)
    {
      float width = (area.width - spacing * (columns - 1)) / columns;
      float height = (area.height - spacing * (rows - 1)) / rows;
      for (int row = 0; row < rows; row++)
      {
        for (int column = 0; column < columns; column++)
        {
          if (results.Count >= limit)
            return;
          // Rows go from top to bottom.
          float x = area.x + column * (width + spacing);
          float y = area.yMax - (row + 1) * height - row * spacing;
          results.Add(new Rect(x, y, width, height));
        }
      }
    }
  }
}
