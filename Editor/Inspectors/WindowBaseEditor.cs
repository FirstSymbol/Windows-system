using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using WindowsSystem.Animations;
using WindowsSystem.Layouts;

namespace WindowsSystem.Editor
{
  [CustomEditor(typeof(WindowBase), true), CanEditMultipleObjects]
  internal sealed class WindowBaseEditor : UnityEditor.Editor
  {
    private static readonly string[] BehaviourFields =
    {
      "initialState", "queuePriority", "canvasGroup", "blockInputDuringTransition", "deactivateWhenHidden",
      "resetToDefaultWhenHidden"
    };

    private static readonly string[] AnimationFields = { "showAnimation", "hideAnimation" };
    private static readonly string[] EventFields = { "onShowing", "onShown", "onHiding", "onHidden" };

    private static readonly HashSet<string> BaseFields =
      new(BehaviourFields.Concat(AnimationFields).Append("events").Append("m_Script").Append("defaultSlot"));

    private Label _badge;
    private VisualElement _chips;
    private VisualElement _controls;
    private Label _controlsHint;
    private VisualElement _warnings;
    private bool _instant;

    private IEnumerable<WindowBase> Windows => targets.OfType<WindowBase>();

    public override VisualElement CreateInspectorGUI()
    {
      var root = new VisualElement();
      WindowsEditorUtility.ApplyStyles(root);
      root.AddToClassList("ws-inspector");

      root.Add(CreateHeader());

      _warnings = new VisualElement();
      _warnings.AddToClassList("ws-warnings");
      root.Add(_warnings);

      Foldout behaviour = WindowsEditorUtility.CreateSection("Behaviour", "Behaviour");
      AddFields(behaviour, BehaviourFields);
      root.Add(behaviour);

      root.Add(CreateLayoutSection());

      Foldout animations = WindowsEditorUtility.CreateSection("Animations", "Animations");
      AddFields(animations, AnimationFields);
      root.Add(animations);

      List<SerializedProperty> custom = GetCustomFields().ToList();
      if (custom.Count > 0)
      {
        Foldout window = WindowsEditorUtility.CreateSection(
          ObjectNames.NicifyVariableName(target.GetType().Name), "Custom");
        foreach (SerializedProperty property in custom)
          window.Add(new PropertyField(property));
        root.Add(window);
      }

      Foldout events = WindowsEditorUtility.CreateSection("Events", "Events", expandedByDefault: false);
      SerializedProperty eventsProperty = serializedObject.FindProperty("events");
      foreach (string field in EventFields)
      {
        SerializedProperty property = eventsProperty?.FindPropertyRelative(field);
        if (property != null)
          events.Add(new PropertyField(property));
      }
      root.Add(events);

      root.TrackSerializedObjectValue(serializedObject, _ => UpdateWarnings());
      root.schedule.Execute(Refresh).Every(100);
      UpdateWarnings();
      Refresh();
      return root;
    }

    private VisualElement CreateHeader()
    {
      var card = new VisualElement();
      card.AddToClassList("ws-card");

      var top = new VisualElement();
      top.AddToClassList("ws-card__row");

      var titles = new VisualElement();
      titles.AddToClassList("ws-card__titles");
      var title = new Label(targets.Length > 1
        ? $"{targets.Length} windows"
        : ObjectNames.NicifyVariableName(target.GetType().Name));
      title.AddToClassList("ws-card__title");
      var subtitle = new Label(WindowsEditorUtility.GetTypeName(target.GetType().BaseType));
      subtitle.AddToClassList("ws-card__subtitle");
      titles.Add(title);
      titles.Add(subtitle);

      _badge = WindowsEditorUtility.CreateBadge();
      top.Add(titles);
      top.Add(_badge);
      card.Add(top);

      _chips = new VisualElement();
      _chips.AddToClassList("ws-card__chips");
      card.Add(_chips);

      _controls = new VisualElement();
      _controls.AddToClassList("ws-card__controls");
      _controls.Add(CreateButton("Show", w => w.ShowAsync(_instant).Forget()));
      _controls.Add(CreateButton("Hide", w => w.HideAsync(_instant).Forget()));
      _controls.Add(CreateButton("Toggle", w => w.ToggleAsync(_instant).Forget()));
      _controls.Add(CreateButton("Enqueue", w => w.Enqueue()));
      _controls.Add(CreateButton("Close", w => w.CloseAsync(_instant).Forget(), "ws-button--danger"));
      var instant = new Toggle("Instant") { value = _instant };
      instant.AddToClassList("ws-card__instant");
      instant.RegisterValueChangedCallback(evt => _instant = evt.newValue);
      _controls.Add(instant);
      card.Add(_controls);

      _controlsHint = new Label("Show / Hide controls are available in Play Mode.");
      _controlsHint.AddToClassList("ws-card__hint");
      card.Add(_controlsHint);
      return card;
    }

    private Button CreateButton(string text, System.Action<WindowBase> action, string modifier = null)
    {
      var button = new Button(() =>
      {
        foreach (WindowBase window in Windows.ToArray())
          if (window != null)
            action(window);
      }) { text = text };
      button.AddToClassList("ws-button");
      if (modifier != null)
        button.AddToClassList(modifier);
      return button;
    }

    private void Refresh()
    {
      if (target == null || _badge == null)
        return;

      bool playing = Application.isPlaying;
      _controls.SetEnabled(playing);
      _controlsHint.style.display = playing ? DisplayStyle.None : DisplayStyle.Flex;

      var window = (WindowBase)target;
      if (targets.Length > 1)
        WindowsEditorUtility.SetBadge(_badge, "Multiple", "muted");
      else if (playing && window.IsInitialized)
        WindowsEditorUtility.SetBadge(_badge, window.State);
      else
        WindowsEditorUtility.SetBadge(_badge, $"Starts {window.InitialState}", "muted");

      _chips.Clear();
      if (targets.Length > 1)
        return;

      if (playing)
      {
        bool registered = window.Service != null && window.Service.Get(window.GetType()) == window;
        _chips.Add(registered
          ? WindowsEditorUtility.CreateChip("Registered", "good")
          : WindowsEditorUtility.CreateChip(window.Service != null ? "Attached" : "No service", "warning"));
        if (window.InQueue)
          _chips.Add(WindowsEditorUtility.CreateChip("In queue", "info"));
        if (window.IsSpawned)
          _chips.Add(WindowsEditorUtility.CreateChip("Spawned"));
        if (!window.Interactable)
          _chips.Add(WindowsEditorUtility.CreateChip("Not interactable", "warning"));
      }

      _chips.Add(WindowsEditorUtility.CreateChip($"Priority {window.QueuePriority}"));
    }

    private void UpdateWarnings()
    {
      if (_warnings == null)
        return;

      _warnings.Clear();
      List<WindowAnimation> animations = Windows
        .SelectMany(w => new[] { w.ShowAnimation, w.HideAnimation })
        .SelectMany(Flatten)
        .ToList();

      bool needsCanvasGroup = animations.Any(a => a is FadeAnimation);
      WindowBase[] missingGroup = Windows.Where(w => w.CanvasGroup == null && w.GetComponent<CanvasGroup>() == null).ToArray();
      if (missingGroup.Length > 0)
      {
        string message = needsCanvasGroup
          ? "Fade animation requires a CanvasGroup."
          : "Add a CanvasGroup to control interactivity and block input while the window is hidden or animating.";
        var box = new HelpBox(message, needsCanvasGroup ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info);
        box.Add(new Button(() => AddCanvasGroup(missingGroup)) { text = "Add CanvasGroup" });
        _warnings.Add(box);
      }

      if (animations.Any(a => a is SlideAnimation) && Windows.Any(w => w.RectTransform == null))
        _warnings.Add(new HelpBox("Slide animation works best with a RectTransform.", HelpBoxMessageType.Info));

      if (Windows.Any(w => w.ShowAnimation == null || w.HideAnimation == null))
        _warnings.Add(new HelpBox("Animation is not assigned: the window switches instantly.", HelpBoxMessageType.None));
    }

    private static IEnumerable<WindowAnimation> Flatten(WindowAnimation animation)
    {
      if (animation == null)
        yield break;

      yield return animation;
      if (animation is ParallelAnimation parallel && parallel.animations != null)
        foreach (WindowAnimation child in parallel.animations.SelectMany(Flatten))
          yield return child;
    }

    private void AddCanvasGroup(IEnumerable<WindowBase> windows)
    {
      foreach (WindowBase window in windows)
      {
        CanvasGroup group = window.GetComponent<CanvasGroup>();
        if (group == null)
          group = Undo.AddComponent<CanvasGroup>(window.gameObject);

        var windowObject = new SerializedObject(window);
        windowObject.FindProperty("canvasGroup").objectReferenceValue = group;
        windowObject.ApplyModifiedProperties();
      }

      serializedObject.Update();
      UpdateWarnings();
    }

    private VisualElement CreateLayoutSection()
    {
      Foldout layout = WindowsEditorUtility.CreateSection("Layout", "Layout");
      layout.Add(new PropertyField(serializedObject.FindProperty("defaultSlot")));

      var window = (WindowBase)target;
      WindowLayoutRoot layoutRoot = WindowLayoutEditorUtility.FindRoot(window);
      WindowLayoutLibrary library = layoutRoot != null ? layoutRoot.Library : null;
      if (layoutRoot == null || library == null)
      {
        var hint = new Label(layoutRoot == null
          ? "Add a Window Layout Root to a parent (e.g. the Canvas) to place windows into slots."
          : "The Window Layout Root has no library.");
        hint.AddToClassList("ws-card__hint");
        layout.Add(hint);
        return layout;
      }

      var label = new Label("Preview in slot");
      label.AddToClassList("ws-card__hint");
      layout.Add(label);
      var buttons = new VisualElement();
      buttons.AddToClassList("ws-card__controls");
      foreach (WindowSlot slot in library.Slots.Where(s => s != null))
      {
        WindowSlot captured = slot;
        var button = new Button(() =>
        {
          if (Application.isPlaying)
            foreach (WindowBase w in Windows)
              w.MoveToSlot(new WindowSlotRef(captured.Id, captured.name), animate: true);
          else
            WindowLayoutEditorPreview.PreviewSlot(layoutRoot, Windows.ToList(), captured);
        }) { text = slot.name };
        button.AddToClassList("ws-button");
        button.AddToClassList("ws-button--small");
        button.style.borderLeftWidth = 4;
        button.style.borderLeftColor = slot.color;
        buttons.Add(button);
      }

      layout.Add(buttons);
      var revert = new Button(WindowLayoutEditorPreview.Revert) { text = "Revert preview" };
      revert.AddToClassList("ws-button");
      layout.Add(revert);
      return layout;
    }

    private void AddFields(VisualElement container, IEnumerable<string> names)
    {
      foreach (string name in names)
      {
        SerializedProperty property = serializedObject.FindProperty(name);
        if (property != null)
          container.Add(new PropertyField(property));
      }
    }

    private IEnumerable<SerializedProperty> GetCustomFields()
    {
      SerializedProperty iterator = serializedObject.GetIterator();
      if (!iterator.NextVisible(true))
        yield break;

      do
      {
        if (!BaseFields.Contains(iterator.name))
          yield return iterator.Copy();
      } while (iterator.NextVisible(false));
    }
  }
}
