using System;
using System.Collections.Generic;
using UnityEngine;

namespace WindowsSystem.Layouts
{
  /// <summary>Named slots where windows are placed, e.g. "Center Popup", "Left Panel", "Toast".</summary>
  [CreateAssetMenu(fileName = "WindowLayoutLibrary", menuName = "Windows System/Window Layout Library", order = 1)]
  public sealed class WindowLayoutLibrary : ScriptableObject
  {
    [SerializeField] private List<WindowSlot> slots = new();

    [Tooltip("Spacing between tiled windows, in canvas units.")]
    [SerializeField, Min(0f)] private float tilingSpacing = 16f;

    [Tooltip("Inset of the tiling area, in canvas units.")]
    [SerializeField, Min(0f)] private float tilingPadding = 24f;

    public IReadOnlyList<WindowSlot> Slots => slots;
    public float TilingSpacing => tilingSpacing;
    public float TilingPadding => tilingPadding;

    public event Action Changed;

    /// <summary>Finds a slot by id, or by name (case-insensitive).</summary>
    public WindowSlot Find(string idOrName)
    {
      if (string.IsNullOrEmpty(idOrName))
        return null;

      foreach (WindowSlot slot in slots)
        if (slot != null && slot.Id == idOrName)
          return slot;

      foreach (WindowSlot slot in slots)
        if (slot != null && string.Equals(slot.name, idOrName, StringComparison.OrdinalIgnoreCase))
          return slot;

      return null;
    }

    public WindowSlot Find(WindowSlotRef reference) =>
      Find(reference.id) ?? Find(reference.name);

    public WindowSlot AddSlot(string slotName, Color color, WindowSlotRect rect)
    {
      var slot = new WindowSlot(slotName, color, rect);
      slots.Add(slot);
      Changed?.Invoke();
      return slot;
    }

    public void ResetToDefaults()
    {
      slots = CreateDefaultSlots();
      Changed?.Invoke();
    }

    /// <summary>Common window places. Each one adapts to portrait roots.</summary>
    public static List<WindowSlot> CreateDefaultSlots()
    {
      Vector2 center = new(0.5f, 0.5f);
      return new List<WindowSlot>
      {
        new("Fullscreen", new Color(0.45f, 0.5f, 0.6f), WindowSlotRect.Area(Vector2.zero, Vector2.one)),
        new("Center Popup", new Color(0.36f, 0.55f, 0.95f),
          WindowSlotRect.Sized(Vector2.zero, Vector2.one, WindowSlotSizeMode.Fixed, new Vector2(880, 600), center, 48),
          WindowSlotRect.Sized(Vector2.zero, Vector2.one, WindowSlotSizeMode.Fixed, new Vector2(960, 1200), center, 32)),
        new("Left Panel", new Color(0.3f, 0.78f, 0.5f),
          WindowSlotRect.Area(Vector2.zero, new Vector2(0.32f, 1f), 24),
          WindowSlotRect.Area(Vector2.zero, new Vector2(1f, 0.5f), 24)),
        new("Right Panel", new Color(0.95f, 0.7f, 0.25f),
          WindowSlotRect.Area(new Vector2(0.68f, 0f), Vector2.one, 24),
          WindowSlotRect.Area(new Vector2(0f, 0.5f), Vector2.one, 24)),
        new("Bottom Sheet", new Color(0.55f, 0.48f, 0.9f),
          WindowSlotRect.Area(new Vector2(0.2f, 0f), new Vector2(0.8f, 0.42f), 0),
          WindowSlotRect.Area(Vector2.zero, new Vector2(1f, 0.55f), 0)),
        new("Top Bar", new Color(0.25f, 0.75f, 0.85f),
          WindowSlotRect.Area(new Vector2(0f, 0.88f), Vector2.one, 12),
          WindowSlotRect.Area(new Vector2(0f, 0.92f), Vector2.one, 12)),
        new("Toast", new Color(0.92f, 0.42f, 0.38f),
          WindowSlotRect.Sized(new Vector2(0f, 0.75f), Vector2.one, WindowSlotSizeMode.Fixed, new Vector2(720, 120), new Vector2(0.5f, 1f), 24))
      };
    }

    private void Reset() => slots = CreateDefaultSlots();

    private void OnValidate()
    {
      EnsureUniqueIds();
      Changed?.Invoke();
    }

    internal void NotifyChanged() => Changed?.Invoke();

    internal bool EnsureUniqueIds()
    {
      bool changed = false;
      var ids = new HashSet<string>();
      for (int i = 0; i < slots.Count; i++)
      {
        if (slots[i] == null)
        {
          slots[i] = new WindowSlot("Slot", Color.gray, WindowSlotRect.Area(Vector2.zero, Vector2.one));
          changed = true;
        }

        if (string.IsNullOrEmpty(slots[i].Id) || !ids.Add(slots[i].Id))
        {
          slots[i].SetId(WindowSlot.NewId());
          ids.Add(slots[i].Id);
          changed = true;
        }
      }

      return changed;
    }
  }
}
