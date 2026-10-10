using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using WindowsSystem.Layouts;

namespace WindowsSystem.Editor
{
  [CustomEditor(typeof(WindowLayoutLibrary))]
  internal sealed class WindowLayoutLibraryEditor : UnityEditor.Editor
  {
    private const string AspectKey = "WindowsSystem.LayoutPreview.Aspect";
    private const string SafeAreaKey = "WindowsSystem.LayoutPreview.SafeArea";

    private WindowLayoutPreview _preview;
    private ListView _list;

    private WindowLayoutLibrary Library => (WindowLayoutLibrary)target;

    public override VisualElement CreateInspectorGUI()
    {
      var root = new VisualElement();
      WindowsEditorUtility.ApplyStyles(root);
      root.AddToClassList("ws-inspector");

      var card = new VisualElement();
      card.AddToClassList("ws-card");
      var title = new Label("Window Layout Library");
      title.AddToClassList("ws-card__title");
      var subtitle = new Label("Slots are places for windows inside a Window Layout Root. Select a slot and drag its area " +
                               "or edges in the preview; switch the screen to check other proportions.");
      subtitle.AddToClassList("ws-card__subtitle");
      card.Add(title);
      card.Add(subtitle);

      _preview = new WindowLayoutPreview
      {
        Library = Library,
        Editable = true,
        Aspect = SessionState.GetFloat(AspectKey, 16f / 9f),
        SafeArea = SessionState.GetBool(SafeAreaKey, false)
      };
      _preview.SlotClicked += slot =>
      {
        int index = Library.Slots.ToList().IndexOf(slot);
        if (index >= 0)
          _list?.SetSelection(index);
      };

      card.Add(CreatePreviewToolbar());
      card.Add(_preview);

      var actions = new VisualElement();
      actions.AddToClassList("ws-card__controls");
      actions.Add(Button("Generate C# constants", () =>
      {
        string path = WindowLayoutEditorUtility.GenerateConstants(Library);
        if (path != null)
          Debug.Log($"[Windows] Slot constants written to {path}", AssetDatabase.LoadAssetAtPath<Object>(path));
      }));
      actions.Add(Button("Reset to defaults", () =>
      {
        if (!EditorUtility.DisplayDialog("Reset slots", "Replace all slots with the default set?", "Reset", "Cancel"))
          return;
        Undo.RecordObject(Library, "Reset Window Slots");
        Library.ResetToDefaults();
        EditorUtility.SetDirty(Library);
        serializedObject.Update();
      }));
      card.Add(actions);
      root.Add(card);

      Foldout tiling = WindowsEditorUtility.CreateSection("Tiling", "LibraryTiling", expandedByDefault: false);
      tiling.Add(new PropertyField(serializedObject.FindProperty("tilingSpacing")));
      tiling.Add(new PropertyField(serializedObject.FindProperty("tilingPadding")));
      root.Add(tiling);

      Foldout slots = WindowsEditorUtility.CreateSection("Slots", "LibrarySlots");
      _list = new ListView
      {
        bindingPath = "slots",
        reorderable = true,
        showAddRemoveFooter = true,
        showBorder = true,
        showBoundCollectionSize = false,
        showFoldoutHeader = false,
        virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
        selectionType = SelectionType.Single
      };
      _list.selectedIndicesChanged += indices =>
      {
        int index = indices.DefaultIfEmpty(-1).First();
        _preview.SelectedId = index >= 0 && index < Library.Slots.Count ? Library.Slots[index]?.Id : null;
        _preview.MarkDirtyRepaint();
      };
      slots.Add(_list);
      root.Add(slots);

      Library.Changed += OnLibraryChanged;
      root.RegisterCallback<DetachFromPanelEvent>(_ => Library.Changed -= OnLibraryChanged);
      root.TrackSerializedObjectValue(serializedObject, _ =>
      {
        if (Library.EnsureUniqueIds())
          EditorUtility.SetDirty(Library);
        _preview.MarkDirtyRepaint();
      });
      return root;
    }

    private void OnLibraryChanged()
    {
      serializedObject.Update();
      _preview?.MarkDirtyRepaint();
    }

    private VisualElement CreatePreviewToolbar()
    {
      var bar = new VisualElement();
      bar.AddToClassList("ws-card__controls");

      var mode = new EnumField(WindowLayoutPreview.PreviewMode.Slots) { style = { width = 80 } };
      var tilingMode = new EnumField(WindowTilingMode.Grid) { style = { width = 110 } };
      var count = new SliderInt(1, 8) { value = 4, showInputField = true, style = { width = 120 } };
      tilingMode.style.display = DisplayStyle.None;
      count.style.display = DisplayStyle.None;

      mode.RegisterValueChangedCallback(evt =>
      {
        _preview.Mode = (WindowLayoutPreview.PreviewMode)evt.newValue;
        bool tiling = _preview.Mode == WindowLayoutPreview.PreviewMode.Tiling;
        tilingMode.style.display = tiling ? DisplayStyle.Flex : DisplayStyle.None;
        count.style.display = tiling ? DisplayStyle.Flex : DisplayStyle.None;
        _preview.MarkDirtyRepaint();
      });
      tilingMode.RegisterValueChangedCallback(evt =>
      {
        _preview.TilingMode = (WindowTilingMode)evt.newValue;
        _preview.MarkDirtyRepaint();
      });
      count.RegisterValueChangedCallback(evt =>
      {
        _preview.TilingCount = evt.newValue;
        _preview.MarkDirtyRepaint();
      });

      bar.Add(mode);
      foreach ((string label, float aspect) in WindowLayoutEditorUtility.AspectPresets)
      {
        float captured = aspect;
        Button preset = Button(label, null);
        preset.AddToClassList("ws-button--small");
        preset.clicked += () =>
        {
          _preview.Aspect = captured;
          SessionState.SetFloat(AspectKey, captured);
          bar.Query<Button>().ForEach(b => b.EnableInClassList("ws-button--active", b == preset));
          _preview.MarkDirtyRepaint();
        };
        preset.EnableInClassList("ws-button--active", Mathf.Approximately(captured, _preview.Aspect));
        bar.Add(preset);
      }

      var safe = new Toggle("Safe area") { value = _preview.SafeArea };
      safe.AddToClassList("ws-card__instant");
      safe.RegisterValueChangedCallback(evt =>
      {
        _preview.SafeArea = evt.newValue;
        SessionState.SetBool(SafeAreaKey, evt.newValue);
        _preview.MarkDirtyRepaint();
      });
      bar.Add(safe);
      bar.Add(tilingMode);
      bar.Add(count);
      return bar;
    }

    private static Button Button(string text, System.Action action)
    {
      var button = action != null ? new Button(action) { text = text } : new Button { text = text };
      button.AddToClassList("ws-button");
      return button;
    }
  }

  [CustomEditor(typeof(WindowLayoutRoot))]
  internal sealed class WindowLayoutRootEditor : UnityEditor.Editor
  {
    private VisualElement _slotButtons;
    private Label _selectionHint;
    private VisualElement _revertRow;

    private WindowLayoutRoot Root => (WindowLayoutRoot)target;

    public override VisualElement CreateInspectorGUI()
    {
      var root = new VisualElement();
      WindowsEditorUtility.ApplyStyles(root);
      root.AddToClassList("ws-inspector");

      var card = new VisualElement();
      card.AddToClassList("ws-card");
      var title = new Label("Window Layout Root");
      title.AddToClassList("ws-card__title");
      var subtitle = new Label("Windows below this object (or using it as the main root) are placed into slots and tiled here. " +
                               "Slots are drawn in the Scene View while this object is selected.");
      subtitle.AddToClassList("ws-card__subtitle");
      card.Add(title);
      card.Add(subtitle);
      card.Add(new PropertyField(serializedObject.FindProperty("library")));
      card.Add(new PropertyField(serializedObject.FindProperty("respectSafeArea")));
      card.Add(new PropertyField(serializedObject.FindProperty("moveDuration")));
      root.Add(card);

      Foldout preview = WindowsEditorUtility.CreateSection("Preview", "RootPreview");
      _selectionHint = new Label();
      _selectionHint.AddToClassList("ws-card__hint");
      preview.Add(_selectionHint);
      preview.Add(new Label("Place into slot") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 4 } });
      _slotButtons = new VisualElement();
      _slotButtons.AddToClassList("ws-card__controls");
      preview.Add(_slotButtons);

      preview.Add(new Label("Tile") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 4 } });
      var tiles = new VisualElement();
      tiles.AddToClassList("ws-card__controls");
      foreach (WindowTilingMode mode in System.Enum.GetValues(typeof(WindowTilingMode)))
      {
        WindowTilingMode captured = mode;
        var button = new Button(() => Tile(captured)) { text = ObjectNames.NicifyVariableName(mode.ToString()) };
        button.AddToClassList("ws-button");
        button.AddToClassList("ws-button--small");
        tiles.Add(button);
      }

      preview.Add(tiles);

      _revertRow = new VisualElement();
      _revertRow.AddToClassList("ws-card__controls");
      var revert = new Button(() => { WindowLayoutEditorPreview.Revert(); Refresh(); }) { text = "Revert preview" };
      revert.AddToClassList("ws-button");
      var keep = new Button(() => { WindowLayoutEditorPreview.Keep(); Refresh(); }) { text = "Keep layout" };
      keep.AddToClassList("ws-button");
      _revertRow.Add(revert);
      _revertRow.Add(keep);
      preview.Add(_revertRow);
      root.Add(preview);

      root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
      root.schedule.Execute(UpdateSelectionHint).Every(300);
      Refresh();
      return root;
    }

    private List<WindowBase> SelectedOrAllWindows()
    {
      List<WindowBase> selected = Selection.gameObjects
        .Select(go => go.GetComponent<WindowBase>())
        .Where(w => w != null)
        .ToList();
      if (selected.Count > 0)
        return selected;

      return Root.GetComponentsInChildren<WindowBase>(true).ToList();
    }

    private void UpdateSelectionHint()
    {
      if (_selectionHint == null || target == null)
        return;

      int selected = Selection.gameObjects.Count(go => go.GetComponent<WindowBase>() != null);
      _selectionHint.text = Application.isPlaying
        ? "Play Mode: buttons move windows at runtime (animated)."
        : selected > 0
          ? $"Applies to {selected} selected window(s). Revert restores their previous layout."
          : "Applies to every window under this root (select windows to limit it). Revert restores the previous layout.";
      _revertRow.style.display = WindowLayoutEditorPreview.HasPreview ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void Refresh()
    {
      if (_slotButtons == null)
        return;

      _slotButtons.Clear();
      WindowLayoutLibrary library = Root.Library;
      if (library == null)
      {
        _slotButtons.Add(new HelpBox("Assign a Window Layout Library.", HelpBoxMessageType.Info));
        return;
      }

      foreach (WindowSlot slot in library.Slots.Where(s => s != null))
      {
        WindowSlot captured = slot;
        var button = new Button(() => Place(captured)) { text = slot.name };
        button.AddToClassList("ws-button");
        button.AddToClassList("ws-button--small");
        button.style.borderLeftWidth = 4;
        button.style.borderLeftColor = slot.color;
        _slotButtons.Add(button);
      }

      UpdateSelectionHint();
    }

    private void Place(WindowSlot slot)
    {
      List<WindowBase> windows = SelectedOrAllWindows();
      if (Application.isPlaying)
      {
        foreach (WindowBase window in windows)
          Root.Place(window, new WindowSlotRef(slot.Id, slot.name), animate: true);
        return;
      }

      WindowLayoutEditorPreview.PreviewSlot(Root, windows, slot);
      UpdateSelectionHint();
    }

    private void Tile(WindowTilingMode mode)
    {
      List<WindowBase> windows = SelectedOrAllWindows();
      if (Application.isPlaying)
      {
        Root.Tile(mode, windows.Where(w => w.IsVisible).ToList());
        return;
      }

      WindowLayoutEditorPreview.PreviewTiling(Root, windows, mode);
      UpdateSelectionHint();
    }

    private void OnSceneGUI()
    {
      WindowLayoutLibrary library = Root.Library;
      if (library == null || Event.current.type != EventType.Repaint)
        return;

      RectTransform rect = Root.RectTransform;
      foreach (WindowSlot slot in library.Slots.Where(s => s != null))
      {
        Rect local = Root.GetSlotRect(slot);
        Vector3[] corners =
        {
          rect.TransformPoint(new Vector3(local.xMin, local.yMin)),
          rect.TransformPoint(new Vector3(local.xMin, local.yMax)),
          rect.TransformPoint(new Vector3(local.xMax, local.yMax)),
          rect.TransformPoint(new Vector3(local.xMax, local.yMin))
        };
        Color color = slot.color;
        Handles.DrawSolidRectangleWithOutline(corners, new Color(color.r, color.g, color.b, 0.08f), color);
        Handles.Label(corners[1], " " + slot.name, EditorStyles.whiteMiniLabel);
      }
    }
  }
}
