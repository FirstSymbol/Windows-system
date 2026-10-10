using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace WindowsSystem.Editor
{
  /// <summary>Live overview of windows, their states and the queue.</summary>
  internal sealed class WindowsDebuggerWindow : EditorWindow
  {
    private readonly List<WindowBase> _rows = new();
    private readonly List<WindowsQueue.Request> _pending = new();

    private MultiColumnListView _table;
    private ListView _pendingList;
    private Label _status;
    private Label _empty;
    private Label _queueState;
    private Label _current;
    private VisualElement _queueButtons;
    private string _search = string.Empty;
    private bool _visibleOnly;
    private bool _instant;

    [MenuItem("Window/Windows System/Debugger", priority = 2000)]
    public static void Open()
    {
      var window = GetWindow<WindowsDebuggerWindow>();
      window.titleContent = new GUIContent("Windows Debugger", EditorGUIUtility.IconContent("d_UnityEditor.SceneHierarchyWindow").image);
      window.minSize = new Vector2(560, 300);
    }

    private void OnEnable()
    {
      EditorApplication.hierarchyChanged += RefreshRows;
      EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
      EditorApplication.hierarchyChanged -= RefreshRows;
      EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    private void OnPlayModeChanged(PlayModeStateChange _) => RefreshRows();

    public void CreateGUI()
    {
      VisualElement root = rootVisualElement;
      WindowsEditorUtility.ApplyStyles(root);
      root.AddToClassList("ws-debugger");

      root.Add(CreateToolbar());

      var split = new TwoPaneSplitView(1, 170, TwoPaneSplitViewOrientation.Vertical);
      split.AddToClassList("ws-debugger__split");

      var top = new VisualElement { style = { flexGrow = 1 } };
      _table = CreateTable();
      _empty = new Label("No windows in the open scenes.");
      _empty.AddToClassList("ws-debugger__empty");
      top.Add(_table);
      top.Add(_empty);
      split.Add(top);
      split.Add(CreateQueuePanel());
      root.Add(split);

      root.schedule.Execute(Tick).Every(200);
      RefreshRows();
    }

    private Toolbar CreateToolbar()
    {
      var toolbar = new Toolbar();

      var search = new ToolbarSearchField();
      search.AddToClassList("ws-debugger__search");
      search.RegisterValueChangedCallback(evt =>
      {
        _search = evt.newValue ?? string.Empty;
        RefreshRows();
      });
      toolbar.Add(search);

      var visibleOnly = new ToolbarToggle { text = "Visible only" };
      visibleOnly.RegisterValueChangedCallback(evt =>
      {
        _visibleOnly = evt.newValue;
        RefreshRows();
      });
      toolbar.Add(visibleOnly);

      var instant = new ToolbarToggle { text = "Instant" };
      instant.tooltip = "Skip animations when using the action buttons.";
      instant.RegisterValueChangedCallback(evt => _instant = evt.newValue);
      toolbar.Add(instant);

      toolbar.Add(new ToolbarSpacer { flex = true });

      _status = new Label();
      _status.AddToClassList("ws-debugger__status");
      toolbar.Add(_status);

      toolbar.Add(new ToolbarButton(() => GetService()?.HideAllAsync(_instant).Forget()) { text = "Hide all" });
      toolbar.Add(new ToolbarButton(RefreshRows) { text = "Refresh" });
      return toolbar;
    }

    private MultiColumnListView CreateTable()
    {
      var table = new MultiColumnListView
      {
        itemsSource = _rows,
        fixedItemHeight = 26,
        selectionType = SelectionType.Single,
        showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
        style = { flexGrow = 1 }
      };
      table.AddToClassList("ws-debugger__table");

      table.columns.Add(new Column
      {
        name = "state", title = "State", width = 96, resizable = false,
        makeCell = WindowsEditorUtility.CreateBadge,
        bindCell = (element, index) => BindState((Label)element, _rows[index])
      });
      table.columns.Add(new Column
      {
        name = "window", title = "Window", width = 160, minWidth = 110, stretchable = true,
        makeCell = () => CellLabel("ws-debugger__type"),
        bindCell = (element, index) => ((Label)element).text = WindowsEditorUtility.GetTypeName(_rows[index].GetType())
      });
      table.columns.Add(new Column
      {
        name = "object", title = "GameObject", width = 130, minWidth = 80, stretchable = true,
        makeCell = () => CellLabel(),
        bindCell = (element, index) => ((Label)element).text = _rows[index] != null ? _rows[index].name : "(destroyed)"
      });
      table.columns.Add(new Column
      {
        name = "scene", title = "Scene", width = 100, minWidth = 60,
        makeCell = () => CellLabel("ws-debugger__muted"),
        bindCell = (element, index) => ((Label)element).text = _rows[index] != null ? _rows[index].gameObject.scene.name : string.Empty
      });
      table.columns.Add(new Column
      {
        name = "flags", title = "Info", width = 130, minWidth = 60,
        makeCell = () => new VisualElement { name = "chips", style = { flexDirection = FlexDirection.Row } },
        bindCell = (element, index) => BindFlags(element, _rows[index])
      });
      table.columns.Add(new Column
      {
        name = "actions", title = "Actions", width = 214, resizable = false,
        makeCell = MakeActions,
        bindCell = (element, index) => element.userData = _rows[index]
      });

      table.selectionChanged += selection =>
      {
        if (selection.FirstOrDefault() is WindowBase window && window != null)
        {
          Selection.activeObject = window.gameObject;
          EditorGUIUtility.PingObject(window.gameObject);
        }
      };

      return table;
    }

    private static Label CellLabel(string modifier = null)
    {
      var label = new Label();
      label.AddToClassList("ws-debugger__cell");
      if (modifier != null)
        label.AddToClassList(modifier);
      return label;
    }

    private VisualElement MakeActions()
    {
      var container = new VisualElement();
      container.AddToClassList("ws-debugger__actions");

      void Add(string text, Action<WindowBase> action, bool playOnly = true)
      {
        var button = new Button(() =>
        {
          if (container.userData is WindowBase window && window != null)
            action(window);
        }) { text = text };
        button.AddToClassList("ws-button");
        button.AddToClassList("ws-button--small");
        if (playOnly)
          button.AddToClassList("ws-play-only");
        container.Add(button);
      }

      Add("Show", w => w.ShowAsync(_instant).Forget());
      Add("Hide", w => w.HideAsync(_instant).Forget());
      Add("Toggle", w => w.ToggleAsync(_instant).Forget());
      Add("Queue", w => w.Enqueue());
      Add("Ping", w => EditorGUIUtility.PingObject(w.gameObject), playOnly: false);
      return container;
    }

    private static void BindState(Label badge, WindowBase window)
    {
      if (window == null)
        WindowsEditorUtility.SetBadge(badge, "Destroyed", "error");
      else if (Application.isPlaying && window.IsInitialized)
        WindowsEditorUtility.SetBadge(badge, window.State);
      else
        WindowsEditorUtility.SetBadge(badge, window.InitialState == InitialWindowState.Shown ? "Starts shown" : "Starts hidden", "muted");
    }

    private static void BindFlags(VisualElement container, WindowBase window)
    {
      container.Clear();
      if (window == null)
        return;

      if (Application.isPlaying)
      {
        if (window.Service == null)
          container.Add(WindowsEditorUtility.CreateChip("No service", "warning"));
        else if (window.Service.Get(window.GetType()) != window)
          container.Add(WindowsEditorUtility.CreateChip("Extra instance", "warning"));
        if (window.InQueue)
          container.Add(WindowsEditorUtility.CreateChip("Queue", "info"));
        if (window.IsSpawned)
          container.Add(WindowsEditorUtility.CreateChip("Spawned"));
      }

      container.Add(WindowsEditorUtility.CreateChip($"P{window.QueuePriority}"));
    }

    private VisualElement CreateQueuePanel()
    {
      var panel = new VisualElement();
      panel.AddToClassList("ws-queue");

      var header = new VisualElement();
      header.AddToClassList("ws-queue__header");
      var title = new Label("Queue");
      title.AddToClassList("ws-queue__title");
      _queueState = WindowsEditorUtility.CreateBadge();
      header.Add(title);
      header.Add(_queueState);
      header.Add(new VisualElement { style = { flexGrow = 1 } });

      _queueButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
      _queueButtons.Add(QueueButton("Skip", queue => queue.Skip()));
      _queueButtons.Add(QueueButton("Clear", queue => queue.Clear()));
      _queueButtons.Add(QueueButton("Stop", queue => queue.Stop()));
      header.Add(_queueButtons);
      panel.Add(header);

      _current = new Label();
      _current.AddToClassList("ws-queue__current");
      panel.Add(_current);

      _pendingList = new ListView
      {
        itemsSource = _pending,
        fixedItemHeight = 20,
        selectionType = SelectionType.None,
        makeItem = () => CellLabel(),
        bindItem = (element, index) =>
        {
          WindowsQueue.Request request = _pending[index];
          string data = request.HasData ? $"   data: {FormatData(request.Data)}" : string.Empty;
          ((Label)element).text = $"{index + 1}. {request.Window.GetType().Name}   priority {request.Priority}{data}";
        },
        style = { flexGrow = 1 }
      };
      panel.Add(_pendingList);
      return panel;
    }

    private Button QueueButton(string text, Action<WindowsQueue> action)
    {
      var button = new Button(() =>
      {
        WindowsQueue queue = GetService()?.Queue;
        if (queue != null)
          action(queue);
      }) { text = text };
      button.AddToClassList("ws-button");
      button.AddToClassList("ws-button--small");
      return button;
    }

    private static WindowsService GetService() =>
      WindowsService.Active.Count > 0 ? WindowsService.Active[WindowsService.Active.Count - 1] : null;

    private void Tick()
    {
      if (_table == null)
        return;

      bool playing = Application.isPlaying;
      rootVisualElement.EnableInClassList("ws-edit-mode", !playing);
      rootVisualElement.Query(className: "ws-play-only").ForEach(element => element.SetEnabled(playing));

      if (playing && _rows.Count != CountCandidates())
        RefreshRows();
      else
        _table.RefreshItems();

      WindowsService service = playing ? GetService() : null;
      _status.text = !playing
        ? "Edit Mode: showing windows in open scenes"
        : service == null
          ? "Play Mode: no WindowsService"
          : $"Play Mode: {service.Windows.Count} registered";

      UpdateQueue(service);
    }

    private void UpdateQueue(WindowsService service)
    {
      _pending.Clear();
      WindowsQueue queue = service?.Queue;
      _queueButtons.SetEnabled(queue != null);

      if (queue == null)
      {
        WindowsEditorUtility.SetBadge(_queueState, Application.isPlaying ? "No service" : "Play Mode only", "muted");
        _current.text = string.Empty;
      }
      else
      {
        WindowsEditorUtility.SetBadge(_queueState, queue.IsRunning ? "Running" : "Idle", queue.IsRunning ? "showing" : "muted");
        WindowsQueue.Request current = queue.Current;
        _current.text = current == null
          ? "Nothing is shown by the queue."
          : $"Current: {current.Window.GetType().Name}   priority {current.Priority}" +
            (current.HasData ? $"   data: {FormatData(current.Data)}" : string.Empty);
        _pending.AddRange(queue.Pending);
      }

      _pendingList.RefreshItems();
    }

    private static int CountCandidates() => WindowTracker.Alive.Count;

    /// <summary>Shows serializable payloads as JSON instead of their type name.</summary>
    private static string FormatData(object data)
    {
      if (data == null)
        return "null";

      Type type = data.GetType();
      if (type.IsPrimitive || data is string || type.IsEnum)
        return data.ToString();

      string text = data.ToString();
      if (text != type.ToString())
        return text;

      try
      {
        string json = JsonUtility.ToJson(data);
        return string.IsNullOrEmpty(json) || json == "{}" ? type.Name : json;
      }
      catch (ArgumentException)
      {
        return type.Name;
      }
    }

    private static WindowBase[] FindSceneWindows()
    {
#if UNITY_6000_4_OR_NEWER
      return Object.FindObjectsByType<WindowBase>(FindObjectsInactive.Include);
#else
      return Object.FindObjectsByType<WindowBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#endif
    }

    private void RefreshRows()
    {
      if (_table == null)
        return;

      IEnumerable<WindowBase> source = Application.isPlaying
        ? WindowTracker.Alive.Where(w => w != null)
        : FindSceneWindows().Where(w => !EditorUtility.IsPersistent(w));

      if (_visibleOnly)
        source = source.Where(w => w.IsVisible || (!Application.isPlaying && w.gameObject.activeInHierarchy));

      if (!string.IsNullOrWhiteSpace(_search))
        source = source.Where(w =>
          w.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
          w.GetType().Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);

      _rows.Clear();
      _rows.AddRange(source.OrderBy(w => w.GetType().Name).ThenBy(w => w.name));
      _table.RefreshItems();
      _empty.style.display = _rows.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }
  }
}
