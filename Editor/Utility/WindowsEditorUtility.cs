using System;
using System.ComponentModel;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace WindowsSystem.Editor
{
  internal static class WindowsEditorUtility
  {
    private const string StyleSheetName = "WindowsSystemEditor";
    private static StyleSheet _styleSheet;

    /// <summary>Adds the shared style sheet and theme class to the element.</summary>
    public static void ApplyStyles(VisualElement element)
    {
      if (_styleSheet == null)
      {
        string path = AssetDatabase.FindAssets($"{StyleSheetName} t:StyleSheet")
          .Select(AssetDatabase.GUIDToAssetPath)
          .FirstOrDefault(p => p.EndsWith($"/{StyleSheetName}.uss", StringComparison.Ordinal));
        if (path != null)
          _styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
      }

      if (_styleSheet != null && !element.styleSheets.Contains(_styleSheet))
        element.styleSheets.Add(_styleSheet);

      element.AddToClassList("ws-root");
      element.EnableInClassList("ws-root--light", !EditorGUIUtility.isProSkin);
    }

    public static Label CreateBadge()
    {
      var badge = new Label();
      badge.AddToClassList("ws-badge");
      return badge;
    }

    public static void SetBadge(Label badge, string text, string modifier)
    {
      badge.text = text;
      foreach (string state in new[] { "hidden", "showing", "shown", "hiding", "muted", "warning", "error", "info" })
        badge.EnableInClassList($"ws-badge--{state}", state == modifier);
    }

    public static void SetBadge(Label badge, WindowState state) =>
      SetBadge(badge, state.ToString(), state.ToString().ToLowerInvariant());

    public static Label CreateChip(string text, string modifier = null)
    {
      var chip = new Label(text);
      chip.AddToClassList("ws-chip");
      if (modifier != null)
        chip.AddToClassList($"ws-chip--{modifier}");
      return chip;
    }

    /// <summary>Foldout styled as an inspector section that remembers its expanded state.</summary>
    public static Foldout CreateSection(string title, string persistenceKey, bool expandedByDefault = true)
    {
      string key = $"WindowsSystem.Section.{persistenceKey}";
      var foldout = new Foldout { text = title, value = SessionState.GetBool(key, expandedByDefault) };
      foldout.AddToClassList("ws-section");
      foldout.RegisterValueChangedCallback(evt =>
      {
        if (evt.target == foldout)
          SessionState.SetBool(key, evt.newValue);
      });
      return foldout;
    }

    public static string GetDisplayName(Type type)
    {
      if (type == null)
        return "None";

      var attribute = (DisplayNameAttribute)Attribute.GetCustomAttribute(type, typeof(DisplayNameAttribute));
      return attribute != null ? attribute.DisplayName : ObjectNames.NicifyVariableName(type.Name);
    }

    /// <summary>Readable type name, including generic arguments.</summary>
    public static string GetTypeName(Type type)
    {
      if (type == null)
        return string.Empty;
      if (!type.IsGenericType)
        return type.Name;

      string name = type.Name;
      int tick = name.IndexOf('`');
      if (tick >= 0)
        name = name.Substring(0, tick);
      return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(GetTypeName))}>";
    }
  }
}
