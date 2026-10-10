using System;
using UnityEngine;

namespace WindowsSystem.Layouts
{
  public enum WindowSlotSizeMode
  {
    /// <summary>Fill the slot area.</summary>
    Stretch,

    /// <summary>Fixed size (clamped to the area), placed by alignment.</summary>
    Fixed,

    /// <summary>Largest rect with the aspect of <see cref="WindowSlotRect.size"/> that fits the area (and the size).</summary>
    FitAspect
  }

  [Serializable]
  public struct WindowPadding
  {
    public float left;
    public float right;
    public float top;
    public float bottom;

    public WindowPadding(float all)
    {
      left = right = top = bottom = all;
    }
  }

  /// <summary>Placement of a window inside the layout root, in normalized root coordinates.</summary>
  [Serializable]
  public struct WindowSlotRect
  {
    [Tooltip("Lower-left corner of the slot area (0..1 of the root).")]
    public Vector2 anchorMin;

    [Tooltip("Upper-right corner of the slot area (0..1 of the root).")]
    public Vector2 anchorMax;

    [Tooltip("Inset of the area in canvas units.")]
    public WindowPadding padding;

    public WindowSlotSizeMode sizeMode;

    [Tooltip("Fixed: window size. Fit Aspect: aspect source and maximum size (0 = unlimited).")]
    public Vector2 size;

    [Tooltip("Position of a smaller window inside the area (0,0 = bottom-left, 0.5,0.5 = center).")]
    public Vector2 alignment;

    public static WindowSlotRect Area(Vector2 min, Vector2 max, float padding = 0f) => new()
    {
      anchorMin = min,
      anchorMax = max,
      padding = new WindowPadding(padding),
      sizeMode = WindowSlotSizeMode.Stretch,
      alignment = new Vector2(0.5f, 0.5f)
    };

    public static WindowSlotRect Sized(Vector2 min, Vector2 max, WindowSlotSizeMode mode, Vector2 size, Vector2 alignment,
      float padding = 0f) => new()
    {
      anchorMin = min,
      anchorMax = max,
      padding = new WindowPadding(padding),
      sizeMode = mode,
      size = size,
      alignment = alignment
    };
  }

  /// <summary>A named place for windows, defined in a <see cref="WindowLayoutLibrary"/>.</summary>
  [Serializable]
  public sealed class WindowSlot
  {
    // "name" comes first so the inspector uses it as the element label.
    public string name;
    [SerializeField, HideInInspector] private string id;
    public Color color = new(0.3f, 0.6f, 0.95f);
    public WindowSlotRect rect;

    [Tooltip("Use a different placement when the root is taller than wide.")]
    public bool usePortraitRect;

    public WindowSlotRect portraitRect;

    public WindowSlot()
    {
      id = NewId();
    }

    public WindowSlot(string name, Color color, WindowSlotRect rect)
    {
      id = NewId();
      this.name = name;
      this.color = color;
      this.rect = rect;
      portraitRect = rect;
    }

    public WindowSlot(string name, Color color, WindowSlotRect rect, WindowSlotRect portrait) : this(name, color, rect)
    {
      usePortraitRect = true;
      portraitRect = portrait;
    }

    public string Id => id;

    public static string NewId() => Guid.NewGuid().ToString("N");

    /// <summary>Placement for a root of the given size.</summary>
    public WindowSlotRect Resolve(Vector2 rootSize) =>
      usePortraitRect && rootSize.y > rootSize.x ? portraitRect : rect;

    internal void SetId(string value) => id = value;

    public override string ToString() => name;
  }

  /// <summary>Serializable reference to a slot of a library (shown as a dropdown in the inspector).</summary>
  [Serializable]
  public struct WindowSlotRef
  {
    public string id;

    [Tooltip("Name at the time of selection, used for display and as a fallback.")]
    public string name;

    public WindowSlotRef(string id, string name = null)
    {
      this.id = id;
      this.name = name;
    }

    public bool IsEmpty => string.IsNullOrEmpty(id) && string.IsNullOrEmpty(name);

    /// <summary>The id when set, otherwise the name.</summary>
    public string Key => string.IsNullOrEmpty(id) ? name : id;

    public static implicit operator WindowSlotRef(string idOrName) => new(idOrName, idOrName);

    public override string ToString() => string.IsNullOrEmpty(name) ? id : name;
  }
}
