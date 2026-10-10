using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using WindowsSystem.Providers;
using Object = UnityEngine.Object;

namespace WindowsSystem.Editor
{
  [CustomEditor(typeof(WindowsProvider))]
  internal sealed class WindowsProviderEditor : UnityEditor.Editor
  {
    private SerializedProperty _windows;
    private ListView _list;
    private Label _count;
    private HelpBox _summary;
    private readonly List<int> _indices = new();

    public override VisualElement CreateInspectorGUI()
    {
      _windows = serializedObject.FindProperty("windows");

      var root = new VisualElement();
      WindowsEditorUtility.ApplyStyles(root);
      root.AddToClassList("ws-inspector");

      var card = new VisualElement();
      card.AddToClassList("ws-card");
      var row = new VisualElement();
      row.AddToClassList("ws-card__row");
      var titles = new VisualElement();
      titles.AddToClassList("ws-card__titles");
      var title = new Label("Windows Provider");
      title.AddToClassList("ws-card__title");
      var subtitle = new Label("Prefabs available to WindowsService.SpawnAsync / OpenAsync. Drop prefabs here.");
      subtitle.AddToClassList("ws-card__subtitle");
      titles.Add(title);
      titles.Add(subtitle);
      _count = WindowsEditorUtility.CreateBadge();
      row.Add(titles);
      row.Add(_count);
      card.Add(row);

      var toolbar = new VisualElement();
      toolbar.AddToClassList("ws-card__controls");
      toolbar.Add(CreateButton("Find window prefabs", FindAllPrefabs));
      toolbar.Add(CreateButton("Remove empty & duplicates", RemoveEmptyAndDuplicates));
      card.Add(toolbar);
      root.Add(card);

      _summary = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
      root.Add(_summary);

      _list = new ListView
      {
        itemsSource = _indices,
        fixedItemHeight = 24,
        reorderable = true,
        reorderMode = ListViewReorderMode.Animated,
        showAddRemoveFooter = true,
        showBorder = true,
        selectionType = SelectionType.Multiple,
        makeItem = MakeRow,
        bindItem = BindRow
      };
      _list.AddToClassList("ws-provider__list");
      _list.onAdd = _ =>
      {
        serializedObject.Update();
        _windows.arraySize++;
        _windows.GetArrayElementAtIndex(_windows.arraySize - 1).objectReferenceValue = null;
        serializedObject.ApplyModifiedProperties();
        Rebuild();
      };
      _list.onRemove = _ =>
      {
        serializedObject.Update();
        int[] selected = _list.selectedIndices.OrderByDescending(i => i).ToArray();
        if (selected.Length == 0 && _windows.arraySize > 0)
          selected = new[] { _windows.arraySize - 1 };
        foreach (int index in selected)
          RemoveAt(index);
        serializedObject.ApplyModifiedProperties();
        _list.ClearSelection();
        Rebuild();
      };
      _list.itemIndexChanged += (from, to) =>
      {
        serializedObject.Update();
        _windows.MoveArrayElement(from, to);
        serializedObject.ApplyModifiedProperties();
        Rebuild();
      };
      root.Add(_list);

      root.RegisterCallback<DragUpdatedEvent>(_ =>
      {
        if (GetDraggedWindows().Any())
          DragAndDrop.visualMode = DragAndDropVisualMode.Link;
      });
      root.RegisterCallback<DragPerformEvent>(_ =>
      {
        WindowBase[] dragged = GetDraggedWindows().ToArray();
        if (dragged.Length == 0)
          return;
        DragAndDrop.AcceptDrag();
        Add(dragged);
      });

      root.TrackSerializedObjectValue(serializedObject, _ => Rebuild());
      Rebuild();
      return root;
    }

    private static Button CreateButton(string text, Action action)
    {
      var button = new Button(action) { text = text };
      button.AddToClassList("ws-button");
      return button;
    }

    private VisualElement MakeRow()
    {
      var row = new VisualElement();
      row.AddToClassList("ws-provider__row");
      var field = new ObjectField { objectType = typeof(WindowBase), allowSceneObjects = false, label = string.Empty };
      field.AddToClassList("ws-provider__field");
      var type = new Label();
      type.AddToClassList("ws-provider__type");
      Label status = WindowsEditorUtility.CreateBadge();
      row.Add(field);
      row.Add(type);
      row.Add(status);
      return row;
    }

    private void BindRow(VisualElement row, int index)
    {
      if (index >= _windows.arraySize)
        return;

      SerializedProperty element = _windows.GetArrayElementAtIndex(index);
      var field = row.Q<ObjectField>();
      field.BindProperty(element);

      var prefab = element.objectReferenceValue as WindowBase;
      row.Q<Label>(className: "ws-provider__type").text =
        prefab != null ? WindowsEditorUtility.GetTypeName(prefab.GetType()) : string.Empty;

      Label status = row.Q<Label>(className: "ws-badge");
      if (prefab == null)
        WindowsEditorUtility.SetBadge(status, "Empty", "warning");
      else if (IsDuplicate(index, prefab.GetType()))
        WindowsEditorUtility.SetBadge(status, "Duplicate", "error");
      else
        WindowsEditorUtility.SetBadge(status, "OK", "shown");
    }

    private bool IsDuplicate(int index, Type type)
    {
      for (int i = 0; i < index; i++)
      {
        var other = _windows.GetArrayElementAtIndex(i).objectReferenceValue as WindowBase;
        if (other != null && other.GetType() == type)
          return true;
      }

      return false;
    }

    private void Rebuild()
    {
      if (_list == null || _windows == null)
        return;

      serializedObject.Update();
      _indices.Clear();
      for (int i = 0; i < _windows.arraySize; i++)
        _indices.Add(i);
      _list.RefreshItems();

      int empty = 0;
      int duplicates = 0;
      var seen = new HashSet<Type>();
      for (int i = 0; i < _windows.arraySize; i++)
      {
        var prefab = _windows.GetArrayElementAtIndex(i).objectReferenceValue as WindowBase;
        if (prefab == null)
          empty++;
        else if (!seen.Add(prefab.GetType()))
          duplicates++;
      }

      WindowsEditorUtility.SetBadge(_count, seen.Count == 1 ? "1 window" : $"{seen.Count} windows", "info");
      var problems = new List<string>();
      if (empty > 0)
        problems.Add($"{empty} empty slot(s)");
      if (duplicates > 0)
        problems.Add($"{duplicates} duplicate window type(s), only the first one is used");
      _summary.text = string.Join(", ", problems) + ".";
      _summary.style.display = problems.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void FindAllPrefabs()
    {
      string[] guids = AssetDatabase.FindAssets("t:Prefab");
      var found = new List<WindowBase>();
      try
      {
        for (int i = 0; i < guids.Length; i++)
        {
          string path = AssetDatabase.GUIDToAssetPath(guids[i]);
          if (EditorUtility.DisplayCancelableProgressBar("Windows Provider", path, (float)i / guids.Length))
            break;

          var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
          if (prefab != null && prefab.TryGetComponent(out WindowBase window))
            found.Add(window);
        }
      }
      finally
      {
        EditorUtility.ClearProgressBar();
      }

      int added = Add(found);
      Debug.Log($"[Windows] Found {found.Count} window prefab(s), added {added}.", target);
    }

    private int Add(IEnumerable<WindowBase> windows)
    {
      serializedObject.Update();
      var existing = new HashSet<Type>(Enumerable.Range(0, _windows.arraySize)
        .Select(i => _windows.GetArrayElementAtIndex(i).objectReferenceValue as WindowBase)
        .Where(w => w != null)
        .Select(w => w.GetType()));

      int added = 0;
      foreach (WindowBase window in windows)
      {
        if (window == null || !existing.Add(window.GetType()))
          continue;

        _windows.arraySize++;
        _windows.GetArrayElementAtIndex(_windows.arraySize - 1).objectReferenceValue = window;
        added++;
      }

      serializedObject.ApplyModifiedProperties();
      Rebuild();
      return added;
    }

    private void RemoveEmptyAndDuplicates()
    {
      serializedObject.Update();
      var seen = new HashSet<Type>();
      for (int i = _windows.arraySize - 1; i >= 0; i--)
      {
        var prefab = _windows.GetArrayElementAtIndex(i).objectReferenceValue as WindowBase;
        if (prefab == null)
          RemoveAt(i);
      }

      for (int i = 0; i < _windows.arraySize; i++)
      {
        var prefab = (WindowBase)_windows.GetArrayElementAtIndex(i).objectReferenceValue;
        if (!seen.Add(prefab.GetType()))
        {
          RemoveAt(i);
          i--;
        }
      }

      serializedObject.ApplyModifiedProperties();
      Rebuild();
    }

    private void RemoveAt(int index)
    {
      SerializedProperty element = _windows.GetArrayElementAtIndex(index);
      element.objectReferenceValue = null;
      _windows.DeleteArrayElementAtIndex(index);
    }

    private static IEnumerable<WindowBase> GetDraggedWindows()
    {
      foreach (Object dragged in DragAndDrop.objectReferences)
      {
        WindowBase window = dragged switch
        {
          GameObject go => go.GetComponent<WindowBase>(),
          WindowBase component => component,
          _ => null
        };

        if (window != null && EditorUtility.IsPersistent(window))
          yield return window;
      }
    }
  }
}
