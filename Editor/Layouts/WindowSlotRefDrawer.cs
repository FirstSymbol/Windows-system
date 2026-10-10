using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using WindowsSystem.Layouts;

namespace WindowsSystem.Editor
{
  /// <summary>Dropdown of the slots of the library used by the inspected object.</summary>
  [CustomPropertyDrawer(typeof(WindowSlotRef))]
  internal sealed class WindowSlotRefDrawer : PropertyDrawer
  {
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
      SerializedProperty id = property.FindPropertyRelative(nameof(WindowSlotRef.id));
      SerializedProperty slotName = property.FindPropertyRelative(nameof(WindowSlotRef.name));

      var field = new DropdownField(property.displayName);
      field.AddToClassList(BaseField<string>.alignedFieldUssClassName);

      void Refresh()
      {
        WindowLayoutLibrary library = WindowLayoutEditorUtility.FindLibrary(property.serializedObject.targetObject);
        var choices = new System.Collections.Generic.List<string> { "None" };
        if (library != null)
          choices.AddRange(library.Slots.Where(s => s != null).Select(s => s.name));
        field.choices = choices;

        WindowSlot current = library != null ? library.Find(new WindowSlotRef(id.stringValue, slotName.stringValue)) : null;
        if (current != null)
          field.SetValueWithoutNotify(current.name);
        else if (string.IsNullOrEmpty(id.stringValue) && string.IsNullOrEmpty(slotName.stringValue))
          field.SetValueWithoutNotify("None");
        else
          field.SetValueWithoutNotify($"{slotName.stringValue} (missing)");

        field.tooltip = library != null
          ? $"Slots of '{library.name}'"
          : "No Window Layout Library found (add a Window Layout Root or create a library).";
      }

      field.RegisterValueChangedCallback(evt =>
      {
        WindowLayoutLibrary library = WindowLayoutEditorUtility.FindLibrary(property.serializedObject.targetObject);
        WindowSlot slot = evt.newValue == "None" || library == null ? null : library.Find(evt.newValue);
        id.stringValue = slot?.Id ?? string.Empty;
        slotName.stringValue = slot?.name ?? string.Empty;
        property.serializedObject.ApplyModifiedProperties();
      });

      field.RegisterCallback<FocusInEvent>(_ => Refresh());
      field.TrackPropertyValue(property, _ => Refresh());
      Refresh();
      return field;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
      SerializedProperty id = property.FindPropertyRelative(nameof(WindowSlotRef.id));
      SerializedProperty slotName = property.FindPropertyRelative(nameof(WindowSlotRef.name));
      WindowLayoutLibrary library = WindowLayoutEditorUtility.FindLibrary(property.serializedObject.targetObject);
      string[] names = new[] { "None" }.Concat(library != null ? library.Slots.Where(s => s != null).Select(s => s.name) : new string[0]).ToArray();
      WindowSlot current = library != null ? library.Find(new WindowSlotRef(id.stringValue, slotName.stringValue)) : null;
      int index = current != null ? System.Array.IndexOf(names, current.name) : 0;
      int selected = EditorGUI.Popup(position, label.text, Mathf.Max(0, index), names);
      if (selected == index)
        return;

      WindowSlot slot = selected <= 0 || library == null ? null : library.Find(names[selected]);
      id.stringValue = slot?.Id ?? string.Empty;
      slotName.stringValue = slot?.name ?? string.Empty;
    }
  }
}
