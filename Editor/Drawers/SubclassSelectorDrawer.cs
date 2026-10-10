using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using WindowsSystem.Animations;
using Object = UnityEngine.Object;

namespace WindowsSystem.Editor
{
  [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
  internal sealed class SubclassSelectorDrawer : PropertyDrawer
  {
    private static readonly Dictionary<Type, Type[]> TypeCacheByBase = new();

    #region UI Toolkit

    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
      if (property.propertyType != SerializedPropertyType.ManagedReference)
        return new HelpBox($"{property.displayName}: [SubclassSelector] requires [SerializeReference].", HelpBoxMessageType.Error);

      var root = new VisualElement();
      root.AddToClassList("ws-subclass");
      WindowsEditorUtility.ApplyStyles(root);

      var foldout = new Foldout { text = property.displayName, value = property.isExpanded };
      foldout.AddToClassList("ws-subclass__foldout");
      foldout.RegisterValueChangedCallback(evt =>
      {
        if (evt.target == foldout)
        {
          property.isExpanded = evt.newValue;
        }
      });

      var typeButton = new Button { text = GetTypeLabel(property) };
      typeButton.AddToClassList("ws-subclass__button");
      typeButton.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
      typeButton.clicked += () => ShowTypeMenu(property, () => Rebuild(property, foldout, typeButton));

      Toggle toggle = foldout.Q<Toggle>();
      toggle.Add(typeButton);

      root.Add(foldout);
      BuildChildren(property, foldout.contentContainer);

      string lastType = property.managedReferenceFullTypename;
      root.TrackPropertyValue(property, changed =>
      {
        if (changed.managedReferenceFullTypename == lastType)
          return;
        lastType = changed.managedReferenceFullTypename;
        Rebuild(changed, foldout, typeButton);
      });

      return root;
    }

    private static void Rebuild(SerializedProperty property, Foldout foldout, Button typeButton)
    {
      typeButton.text = GetTypeLabel(property);
      BuildChildren(property, foldout.contentContainer);
    }

    private static void BuildChildren(SerializedProperty property, VisualElement container)
    {
      container.Clear();
      SerializedProperty iterator = property.Copy();
      SerializedProperty end = iterator.GetEndProperty();
      bool hasChildren = false;

      if (iterator.NextVisible(true))
      {
        do
        {
          if (SerializedProperty.EqualContents(iterator, end))
            break;

          var field = new PropertyField(iterator.Copy());
          field.Bind(property.serializedObject);
          BindCustomCurveVisibility(property, iterator, field);
          container.Add(field);
          hasChildren = true;
        } while (iterator.NextVisible(false));
      }

      if (!hasChildren)
      {
        var empty = new Label(property.managedReferenceValue == null ? "Not assigned" : "No settings");
        empty.AddToClassList("ws-subclass__empty");
        container.Add(empty);
      }
    }

    /// <summary>Shows the custom curve of tween animations only when Ease is Custom.</summary>
    private static void BindCustomCurveVisibility(SerializedProperty owner, SerializedProperty child, VisualElement field)
    {
      if (child.name != nameof(TweenWindowAnimation.customCurve))
        return;

      SerializedProperty ease = owner.FindPropertyRelative(nameof(TweenWindowAnimation.ease));
      if (ease == null || ease.propertyType != SerializedPropertyType.Enum)
        return;

      void Update(SerializedProperty property) =>
        field.style.display = property.enumValueIndex == (int)Ease.Custom ? DisplayStyle.Flex : DisplayStyle.None;

      Update(ease);
      field.TrackPropertyValue(ease, Update);
    }

    #endregion

    #region IMGUI

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
      float height = EditorGUIUtility.singleLineHeight;
      if (property.propertyType != SerializedPropertyType.ManagedReference || !property.isExpanded)
        return height;

      foreach (SerializedProperty child in GetChildren(property))
        height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
      return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
      if (property.propertyType != SerializedPropertyType.ManagedReference)
      {
        EditorGUI.LabelField(position, label.text, "[SubclassSelector] requires [SerializeReference].");
        return;
      }

      EditorGUI.BeginProperty(position, label, property);
      var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
      var foldoutRect = new Rect(line.x, line.y, EditorGUIUtility.labelWidth, line.height);
      var buttonRect = new Rect(line.x + EditorGUIUtility.labelWidth, line.y,
        line.width - EditorGUIUtility.labelWidth, line.height);

      property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
      if (EditorGUI.DropdownButton(buttonRect, new GUIContent(GetTypeLabel(property)), FocusType.Keyboard))
        ShowTypeMenu(property, null);

      if (property.isExpanded)
      {
        EditorGUI.indentLevel++;
        float y = line.yMax + EditorGUIUtility.standardVerticalSpacing;
        foreach (SerializedProperty child in GetChildren(property))
        {
          float height = EditorGUI.GetPropertyHeight(child, true);
          EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), child, true);
          y += height + EditorGUIUtility.standardVerticalSpacing;
        }

        EditorGUI.indentLevel--;
      }

      EditorGUI.EndProperty();
    }

    private static IEnumerable<SerializedProperty> GetChildren(SerializedProperty property)
    {
      SerializedProperty iterator = property.Copy();
      SerializedProperty end = iterator.GetEndProperty();
      if (!iterator.NextVisible(true))
        yield break;

      do
      {
        if (SerializedProperty.EqualContents(iterator, end))
          yield break;
        yield return iterator.Copy();
      } while (iterator.NextVisible(false));
    }

    #endregion

    private void ShowTypeMenu(SerializedProperty property, Action onChanged)
    {
      Type baseType = GetBaseType();
      Type currentType = property.managedReferenceValue?.GetType();
      var menu = new GenericMenu();

      menu.AddItem(new GUIContent("None"), currentType == null, () => Assign(property, null, onChanged));
      menu.AddSeparator(string.Empty);

      foreach (Type type in GetAssignableTypes(baseType))
      {
        Type captured = type;
        menu.AddItem(new GUIContent(WindowsEditorUtility.GetDisplayName(type)), type == currentType,
          () => Assign(property, captured, onChanged));
      }

      menu.ShowAsContext();
    }

    private static void Assign(SerializedProperty property, Type type, Action onChanged)
    {
      SerializedObject serializedObject = property.serializedObject;
      serializedObject.Update();

      object previous = property.managedReferenceValue;
      object value = type == null ? null : Activator.CreateInstance(type);

      // Keep compatible settings (duration, ease...) when switching between similar types.
      if (previous != null && value != null)
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(previous), value);

      foreach (Object target in serializedObject.targetObjects)
      {
        using var single = new SerializedObject(target);
        SerializedProperty targetProperty = single.FindProperty(property.propertyPath);
        targetProperty.managedReferenceValue = value == null ? null : CloneOrSame(value, target, serializedObject);
        single.ApplyModifiedProperties();
      }

      serializedObject.Update();
      onChanged?.Invoke();
    }

    /// <summary>Every target needs its own instance, otherwise multi-selection would share one object.</summary>
    private static object CloneOrSame(object value, Object target, SerializedObject serializedObject)
    {
      if (serializedObject.targetObjects.Length == 1 || target == serializedObject.targetObjects[0])
        return value;

      object clone = Activator.CreateInstance(value.GetType());
      JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(value), clone);
      return clone;
    }

    private Type GetBaseType()
    {
      Type type = fieldInfo.FieldType;
      if (type.IsArray)
        return type.GetElementType();
      if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        return type.GetGenericArguments()[0];
      return type;
    }

    private static IEnumerable<Type> GetAssignableTypes(Type baseType)
    {
      if (TypeCacheByBase.TryGetValue(baseType, out Type[] cached))
        return cached;

      cached = TypeCache.GetTypesDerivedFrom(baseType)
        .Append(baseType)
        .Where(type => !type.IsAbstract && !type.IsGenericTypeDefinition && !type.IsInterface &&
                       !typeof(Object).IsAssignableFrom(type) &&
                       type.IsDefined(typeof(SerializableAttribute), false) &&
                       type.GetConstructor(Type.EmptyTypes) != null)
        .Distinct()
        .OrderBy(WindowsEditorUtility.GetDisplayName)
        .ToArray();

      TypeCacheByBase[baseType] = cached;
      return cached;
    }

    private static string GetTypeLabel(SerializedProperty property)
    {
      object value = property.managedReferenceValue;
      return value == null ? "None" : WindowsEditorUtility.GetDisplayName(value.GetType());
    }
  }
}
