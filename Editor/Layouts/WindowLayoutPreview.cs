using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using WindowsSystem.Layouts;

namespace WindowsSystem.Editor
{
  /// <summary>
  /// Mock screen that shows the slots (or a tiling) of a library at a chosen aspect ratio.
  /// In editable slot mode the selected slot area can be moved and resized with the mouse.
  /// </summary>
  internal sealed class WindowLayoutPreview : IMGUIContainer
  {
    public enum PreviewMode
    {
      Slots,
      Tiling
    }

    private const float ReferenceShortSide = 1080f;
    private const float Grab = 6f;
    private const float MaxHeight = 300f;

    private readonly List<Rect> _tiles = new();
    private WindowSlot _drag;
    private int _dragEdges; // 1 left, 2 right, 4 bottom, 8 top, 15 = move
    private Vector2 _dragStart;
    private WindowSlotRect _dragOrigin;

    public WindowLayoutPreview()
    {
      onGUIHandler = Draw;
      style.height = 220;
    }

    public WindowLayoutLibrary Library { get; set; }
    public float Aspect { get; set; } = 16f / 9f;
    public bool SafeArea { get; set; }
    public PreviewMode Mode { get; set; }
    public WindowTilingMode TilingMode { get; set; } = WindowTilingMode.Grid;
    public int TilingCount { get; set; } = 4;
    public bool Editable { get; set; }
    public string SelectedId { get; set; }

    public event Action<WindowSlot> SlotClicked;

    /// <summary>Size of the simulated layout root in canvas units.</summary>
    public Vector2 RootSize => Aspect >= 1f
      ? new Vector2(ReferenceShortSide * Aspect, ReferenceShortSide)
      : new Vector2(ReferenceShortSide, ReferenceShortSide / Aspect);

    private void Draw()
    {
      if (Library == null)
        return;

      float width = contentRect.width > 1f ? contentRect.width : 300f;
      float screenHeight = Mathf.Min(MaxHeight, (width - 16f) / Aspect);
      float screenWidth = screenHeight * Aspect;
      float height = screenHeight + 16f;
      if (Math.Abs(style.height.value.value - height) > 0.5f)
        style.height = height;

      Rect rect = GUILayoutUtility.GetRect(width, height);
      rect.width = width;
      EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.13f, 0.13f, 0.13f) : new Color(0.8f, 0.8f, 0.8f));

      var screen = new Rect(rect.x + (width - screenWidth) / 2f, rect.y + 8f, screenWidth, screenHeight);
      Rounded(new Rect(screen.x - 4f, screen.y - 4f, screen.width + 8f, screen.height + 8f), new Color(0.05f, 0.05f, 0.05f), 0f, 8f);
      Rounded(screen, new Color(0.19f, 0.2f, 0.23f), 0f, 4f);

      Vector2 root = RootSize;
      Rect area = new(Vector2.zero, root);
      if (SafeArea)
      {
        Rect safe = SafeAreaRect(root);
        DrawSafeArea(screen, root, area, safe);
        area = safe;
      }

      if (Mode == PreviewMode.Tiling)
        DrawTiling(screen, root, area);
      else
        DrawSlots(screen, root, area);
    }

    /// <summary>Typical phone insets: a notch on the long side.</summary>
    private Rect SafeAreaRect(Vector2 root) => Aspect >= 1f
      ? Rect.MinMaxRect(root.x * 0.045f, 0f, root.x * 0.955f, root.y)
      : Rect.MinMaxRect(0f, root.y * 0.03f, root.x, root.y * 0.955f);

    private static void DrawSafeArea(Rect screen, Vector2 root, Rect area, Rect safe)
    {
      var shade = new Color(0f, 0f, 0f, 0.35f);
      foreach (Rect band in new[]
               {
                 Rect.MinMaxRect(area.xMin, area.yMin, safe.xMin, area.yMax),
                 Rect.MinMaxRect(safe.xMax, area.yMin, area.xMax, area.yMax),
                 Rect.MinMaxRect(safe.xMin, area.yMin, safe.xMax, safe.yMin),
                 Rect.MinMaxRect(safe.xMin, safe.yMax, safe.xMax, area.yMax)
               })
      {
        if (band.width > 0f && band.height > 0f)
          EditorGUI.DrawRect(ToGui(screen, root, band), shade);
      }
    }

    private void DrawSlots(Rect screen, Vector2 root, Rect area)
    {
      var label = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.UpperLeft, clipping = TextClipping.Clip };
      var hitRects = new List<(Rect rect, WindowSlot slot)>();
      bool anySelected = false;
      foreach (WindowSlot slot in Library.Slots)
        anySelected |= slot != null && slot.Id == SelectedId;

      foreach (WindowSlot slot in Library.Slots)
      {
        if (slot == null)
          continue;

        Rect rootRect = WindowLayoutMath.ComputeSlot(slot.Resolve(root), area);
        Rect gui = ToGui(screen, root, rootRect);
        bool selected = slot.Id == SelectedId;
        Color color = slot.color;

        // Selected slot stands out; the others are dimmed so overlapping slots stay readable.
        float fill = selected ? 0.5f : anySelected ? 0.06f : 0.16f;
        float outline = selected ? 1f : anySelected ? 0.35f : 0.85f;
        Rounded(gui, new Color(color.r, color.g, color.b, fill), 0f, 3f);
        Rounded(gui, new Color(color.r, color.g, color.b, outline), selected ? 2f : 1f, 3f);
        if (gui.width > 30f && gui.height > 12f && (selected || !anySelected))
        {
          label.normal.textColor = selected ? Color.white : new Color(color.r, color.g, color.b, 1f);
          GUI.Label(new Rect(gui.x + 4f, gui.y + 2f, gui.width - 6f, 14f), slot.name, label);
        }

        hitRects.Add((gui, slot));

        if (selected && Editable)
        {
          // The slot area (before padding and size mode) is what the mouse edits.
          WindowSlotRect slotRect = slot.Resolve(root);
          Rect region = ToGui(screen, root, new Rect(
            area.min + Vector2.Scale(slotRect.anchorMin, area.size),
            Vector2.Scale(slotRect.anchorMax - slotRect.anchorMin, area.size)));
          DrawDashed(region, Color.white);
          EditorGUIUtility.AddCursorRect(region, MouseCursor.MoveArrow);
          EditorGUIUtility.AddCursorRect(new Rect(region.x - Grab, region.y, Grab * 2f, region.height), MouseCursor.ResizeHorizontal);
          EditorGUIUtility.AddCursorRect(new Rect(region.xMax - Grab, region.y, Grab * 2f, region.height), MouseCursor.ResizeHorizontal);
          EditorGUIUtility.AddCursorRect(new Rect(region.x, region.y - Grab, region.width, Grab * 2f), MouseCursor.ResizeVertical);
          EditorGUIUtility.AddCursorRect(new Rect(region.x, region.yMax - Grab, region.width, Grab * 2f), MouseCursor.ResizeVertical);
          HandleDrag(screen, root, area, slot, region);
        }
      }

      Event e = Event.current;
      if (e.type == EventType.MouseDown && e.button == 0 && _drag == null)
      {
        // Prefer the smallest rect under the mouse so nested slots can be picked.
        WindowSlot picked = null;
        float best = float.MaxValue;
        foreach ((Rect rect, WindowSlot slot) in hitRects)
        {
          float size = rect.width * rect.height;
          if (rect.Contains(e.mousePosition) && size < best)
          {
            best = size;
            picked = slot;
          }
        }

        if (picked != null)
        {
          SelectedId = picked.Id;
          SlotClicked?.Invoke(picked);
          e.Use();
          MarkDirtyRepaint();
        }
      }
    }

    private void HandleDrag(Rect screen, Vector2 root, Rect area, WindowSlot slot, Rect region)
    {
      Event e = Event.current;
      bool portrait = slot.usePortraitRect && root.y > root.x;

      switch (e.type)
      {
        case EventType.MouseDown when e.button == 0 && Near(region, e.mousePosition):
        {
          int edges = 0;
          if (Mathf.Abs(e.mousePosition.x - region.x) < Grab) edges |= 1;
          if (Mathf.Abs(e.mousePosition.x - region.xMax) < Grab) edges |= 2;
          if (Mathf.Abs(e.mousePosition.y - region.yMax) < Grab) edges |= 4;
          if (Mathf.Abs(e.mousePosition.y - region.y) < Grab) edges |= 8;
          _dragEdges = edges == 0 ? 15 : edges;
          _drag = slot;
          _dragStart = e.mousePosition;
          _dragOrigin = portrait ? slot.portraitRect : slot.rect;
          Undo.RecordObject(Library, "Edit Window Slot");
          e.Use();
          break;
        }
        case EventType.MouseDrag when _drag == slot:
        {
          Vector2 delta = e.mousePosition - _dragStart;
          Vector2 normalized = new(delta.x / screen.width * root.x / area.width, -delta.y / screen.height * root.y / area.height);
          WindowSlotRect r = _dragOrigin;
          if (_dragEdges == 15)
          {
            Vector2 size = r.anchorMax - r.anchorMin;
            r.anchorMin = Clamp01(r.anchorMin + normalized, size);
            r.anchorMax = r.anchorMin + size;
          }
          else
          {
            if ((_dragEdges & 1) != 0) r.anchorMin.x = Mathf.Clamp(r.anchorMin.x + normalized.x, 0f, r.anchorMax.x - 0.02f);
            if ((_dragEdges & 2) != 0) r.anchorMax.x = Mathf.Clamp(r.anchorMax.x + normalized.x, r.anchorMin.x + 0.02f, 1f);
            if ((_dragEdges & 4) != 0) r.anchorMin.y = Mathf.Clamp(r.anchorMin.y + normalized.y, 0f, r.anchorMax.y - 0.02f);
            if ((_dragEdges & 8) != 0) r.anchorMax.y = Mathf.Clamp(r.anchorMax.y + normalized.y, r.anchorMin.y + 0.02f, 1f);
          }

          r.anchorMin = Round(r.anchorMin);
          r.anchorMax = Round(r.anchorMax);
          if (portrait)
            slot.portraitRect = r;
          else
            slot.rect = r;

          EditorUtility.SetDirty(Library);
          Library.NotifyChanged();
          e.Use();
          MarkDirtyRepaint();
          break;
        }
        case EventType.MouseUp when _drag == slot:
          _drag = null;
          e.Use();
          break;
      }
    }

    private void DrawTiling(Rect screen, Vector2 root, Rect area)
    {
      WindowLayoutMath.ComputeTiling(TilingMode, TilingCount,
        WindowLayoutMath.Inset(area, Library.TilingPadding), Library.TilingSpacing, _tiles);

      var label = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
      label.normal.textColor = Color.white;
      for (int i = 0; i < _tiles.Count; i++)
      {
        Rect gui = ToGui(screen, root, _tiles[i]);
        Color color = Color.HSVToRGB(i / (float)Mathf.Max(1, _tiles.Count) * 0.8f, 0.55f, 0.9f);
        Rounded(gui, new Color(color.r, color.g, color.b, 0.75f), 0f, 4f);
        Rounded(gui, new Color(0f, 0f, 0f, 0.5f), 1f, 4f);
        // Title bar to make the tiles read as windows.
        Rounded(new Rect(gui.x, gui.y, gui.width, Mathf.Min(10f, gui.height)), new Color(0f, 0f, 0f, 0.3f), 0f, 4f);
        GUI.Label(gui, (i + 1).ToString(), label);
      }
    }

    private static Rect ToGui(Rect screen, Vector2 root, Rect rect)
    {
      float x = screen.x + rect.x / root.x * screen.width;
      float y = screen.yMax - rect.yMax / root.y * screen.height;
      return new Rect(x, y, rect.width / root.x * screen.width, rect.height / root.y * screen.height);
    }

    private static bool Near(Rect rect, Vector2 point) =>
      new Rect(rect.x - Grab, rect.y - Grab, rect.width + Grab * 2f, rect.height + Grab * 2f).Contains(point);

    private static Vector2 Clamp01(Vector2 min, Vector2 size) =>
      new(Mathf.Clamp(min.x, 0f, 1f - size.x), Mathf.Clamp(min.y, 0f, 1f - size.y));

    private static Vector2 Round(Vector2 v) => new(Mathf.Round(v.x * 100f) / 100f, Mathf.Round(v.y * 100f) / 100f);

    private static void DrawDashed(Rect rect, Color color)
    {
      const float dash = 4f;
      for (float x = rect.x; x < rect.xMax; x += dash * 2f)
      {
        EditorGUI.DrawRect(new Rect(x, rect.y, Mathf.Min(dash, rect.xMax - x), 1f), color);
        EditorGUI.DrawRect(new Rect(x, rect.yMax - 1f, Mathf.Min(dash, rect.xMax - x), 1f), color);
      }

      for (float y = rect.y; y < rect.yMax; y += dash * 2f)
      {
        EditorGUI.DrawRect(new Rect(rect.x, y, 1f, Mathf.Min(dash, rect.yMax - y)), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - 1f, y, 1f, Mathf.Min(dash, rect.yMax - y)), color);
      }
    }

    private static void Rounded(Rect rect, Color color, float border, float radius) =>
      GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, border, radius);
  }
}
