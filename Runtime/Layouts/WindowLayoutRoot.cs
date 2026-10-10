using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using WindowsSystem.Animations;

namespace WindowsSystem.Layouts
{
  /// <summary>
  /// Area in which windows are placed into slots of a <see cref="WindowLayoutLibrary"/> or tiled.
  /// Placements are recomputed when the area is resized or the safe area changes.
  /// </summary>
  [DisallowMultipleComponent]
  [RequireComponent(typeof(RectTransform))]
  [AddComponentMenu("Windows System/Window Layout Root")]
  public sealed class WindowLayoutRoot : MonoBehaviour
  {
    private struct Placement
    {
      public string SlotId;
      public bool Tiled;
      public WindowTilingMode Mode;
      public int Index;
      public int Count;
    }

    private static readonly List<WindowLayoutRoot> EnabledRoots = new();
    private static readonly List<WindowBase> PendingDefaults = new();

    [SerializeField] private WindowLayoutLibrary library;

    [Tooltip("Keep windows inside Screen.safeArea (notches, rounded corners).")]
    [SerializeField] private bool respectSafeArea = true;

    [Tooltip("Duration of animated moves between slots.")]
    [SerializeField, Min(0f)] private float moveDuration = 0.25f;

    private readonly Dictionary<WindowBase, Placement> _placements = new();
    private readonly List<Rect> _scratch = new();
    private Rect _lastSafeArea;
    private bool _dirty;

    /// <summary>The most recently enabled root.</summary>
    public static WindowLayoutRoot Main => EnabledRoots.Count > 0 ? EnabledRoots[EnabledRoots.Count - 1] : null;

    public WindowLayoutLibrary Library
    {
      get => library;
      set
      {
        library = value;
        _dirty = true;
      }
    }

    public bool RespectSafeArea
    {
      get => respectSafeArea;
      set
      {
        respectSafeArea = value;
        _dirty = true;
      }
    }

    public RectTransform RectTransform => (RectTransform)transform;

    /// <summary>Root used by a window: the nearest one in its parents, otherwise <see cref="Main"/>.</summary>
    public static WindowLayoutRoot FindFor(Component window)
    {
      WindowLayoutRoot parent = window != null ? window.GetComponentInParent<WindowLayoutRoot>() : null;
      return parent != null ? parent : Main;
    }

    private void OnEnable()
    {
      EnabledRoots.Add(this);
      _dirty = true;

      foreach (WindowBase window in PendingDefaults.ToArray())
      {
        if (window == null || FindFor(window) != this)
          continue;
        PendingDefaults.Remove(window);
        Place(window, window.DefaultSlot);
      }
    }

    private void OnDisable() => EnabledRoots.Remove(this);

    private void OnRectTransformDimensionsChange() => _dirty = true;

    private void LateUpdate()
    {
      if (respectSafeArea && Screen.safeArea != _lastSafeArea)
      {
        _lastSafeArea = Screen.safeArea;
        _dirty = true;
      }

      if (!_dirty)
        return;

      _dirty = false;
      Reapply();
    }

    /// <summary>Area available to windows, in local space of this RectTransform.</summary>
    public Rect GetArea()
    {
      Rect area = RectTransform.rect;
      if (!respectSafeArea || !Application.isPlaying)
        return area;

      Canvas canvas = GetComponentInParent<Canvas>();
      Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
      Rect safe = Screen.safeArea;
      if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, safe.min, cam, out Vector2 min) ||
          !RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, safe.max, cam, out Vector2 max))
        return area;

      return Rect.MinMaxRect(
        Mathf.Max(area.xMin, min.x), Mathf.Max(area.yMin, min.y),
        Mathf.Min(area.xMax, max.x), Mathf.Min(area.yMax, max.y));
    }

    /// <summary>Rect of the slot in local space of this root.</summary>
    public Rect GetSlotRect(WindowSlot slot)
    {
      Rect area = GetArea();
      return WindowLayoutMath.ComputeSlot(slot.Resolve(area.size), area);
    }

    /// <summary>Places the window into the slot and keeps it there when the root is resized.</summary>
    public bool Place(WindowBase window, WindowSlotRef slotRef, bool animate = false)
    {
      if (window == null || window.RectTransform == null)
        return false;

      WindowSlot slot = library != null ? library.Find(slotRef) : null;
      if (slot == null)
      {
        WindowsLog.Warning($"'{name}': slot '{slotRef}' is not defined in '{(library != null ? library.name : "no library")}'.", this);
        return false;
      }

      _placements[window] = new Placement { SlotId = slot.Id };
      Apply(window, GetSlotRect(slot), animate);
      return true;
    }

    /// <summary>Tiles the windows over the root area; the layout is kept on resize until windows are placed elsewhere.</summary>
    public void Tile(WindowTilingMode mode, IReadOnlyList<WindowBase> windows, bool animate = true)
    {
      var valid = new List<WindowBase>();
      foreach (WindowBase window in windows)
        if (window != null && window.RectTransform != null)
          valid.Add(window);

      ComputeTiling(mode, valid.Count, _scratch);
      for (int i = 0; i < valid.Count; i++)
      {
        _placements[valid[i]] = new Placement { Tiled = true, Mode = mode, Index = i, Count = valid.Count };
        Apply(valid[i], _scratch[i], animate);
      }
    }

    /// <summary>Rects of a tiling over this root (local space), e.g. for previews.</summary>
    public void ComputeTiling(WindowTilingMode mode, int count, List<Rect> results)
    {
      float padding = library != null ? library.TilingPadding : 24f;
      float spacing = library != null ? library.TilingSpacing : 16f;
      WindowLayoutMath.ComputeTiling(mode, count, WindowLayoutMath.Inset(GetArea(), padding), spacing, results);
    }

    /// <summary>Slot id of the window, or null when it is not placed in a slot.</summary>
    public string GetSlotId(WindowBase window) =>
      window != null && _placements.TryGetValue(window, out Placement placement) && !placement.Tiled ? placement.SlotId : null;

    /// <summary>Stops tracking the window (its current position is kept).</summary>
    public void Release(WindowBase window) => _placements.Remove(window);

    /// <summary>Recomputes every placement immediately.</summary>
    public void Reapply()
    {
      if (_placements.Count == 0)
        return;

      var windows = new List<WindowBase>(_placements.Keys);
      foreach (WindowBase window in windows)
      {
        if (window == null)
        {
          _placements.Remove(window);
          continue;
        }

        Placement placement = _placements[window];
        if (placement.Tiled)
        {
          ComputeTiling(placement.Mode, placement.Count, _scratch);
          if (placement.Index < _scratch.Count)
            Apply(window, _scratch[placement.Index], false);
        }
        else if (library != null && library.Find(placement.SlotId) is { } slot)
        {
          Apply(window, GetSlotRect(slot), false);
        }
      }
    }

    internal static void PlaceDefault(WindowBase window)
    {
      WindowLayoutRoot root = FindFor(window);
      if (root != null && root.isActiveAndEnabled)
      {
        root.Place(window, window.DefaultSlot);
        return;
      }

      // The root is not enabled yet (initialization order): place the window when it is.
      if (!PendingDefaults.Contains(window))
        PendingDefaults.Add(window);
    }

    internal static void ReleaseEverywhere(WindowBase window)
    {
      PendingDefaults.Remove(window);
      foreach (WindowLayoutRoot root in EnabledRoots)
        root.Release(window);
    }

    private void Apply(WindowBase window, Rect rect, bool animate)
    {
      WindowLayoutMath.GetAnchors(window.RectTransform, RectTransform, rect, out Vector2 min, out Vector2 max);
      if (animate && Application.isPlaying && moveDuration > 0f && window.isActiveAndEnabled)
      {
        MoveAsync(window, min, max, moveDuration).Forget();
        return;
      }

      window.CancelLayoutMove();
      WindowLayoutMath.SetAnchors(window.RectTransform, min, max);
      window.CaptureRestPose();
    }

    private static async UniTaskVoid MoveAsync(WindowBase window, Vector2 toMin, Vector2 toMax, float duration)
    {
      CancellationToken token = window.BeginLayoutMove();
      RectTransform rect = window.RectTransform;
      WindowLayoutMath.GetCurrentAnchors(rect, out Vector2 fromMin, out Vector2 fromMax);

      float elapsed = 0f;
      while (elapsed < duration)
      {
        await UniTask.Yield(PlayerLoopTiming.Update, token);
        if (token.IsCancellationRequested || rect == null)
          return;

        elapsed += Time.unscaledDeltaTime;
        float t = Easing.Evaluate(Ease.OutCubic, elapsed / duration);
        WindowLayoutMath.SetAnchors(rect, Vector2.LerpUnclamped(fromMin, toMin, t), Vector2.LerpUnclamped(fromMax, toMax, t));
      }

      WindowLayoutMath.SetAnchors(rect, toMin, toMax);
      window.CaptureRestPose();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
      EnabledRoots.Clear();
      PendingDefaults.Clear();
    }
  }
}
