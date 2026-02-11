using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WindowsSystem.Providers
{
  [CreateAssetMenu(fileName = "WindowsProvider Config", menuName = "Configs/Providers/WindowsSystem/WindowsProvider",
    order = 0)]
  public class WindowsProvider : ScriptableObject, IWindowsProvider
  {
    public List<GameObject> windowsObjects;
    private Dictionary<Type, IWindowBase> _windows = new();

    private void OnEnable()
    {
      BuildDictionary();
    }

    private void OnValidate()
    {
      BuildDictionary();
    }

    public T GetWindowPrefab<T>() where T : MonoBehaviour, IWindowBase
    {
      return _windows[typeof(T)] as T;
    }

    private void BuildDictionary()
    {
      windowsObjects = windowsObjects.Distinct().ToList();
      _windows ??= new Dictionary<Type, IWindowBase>();
      _windows.Clear();
      for (var i = 0; i < windowsObjects.Count; i++)
      {
        if (windowsObjects[i] == null) continue;
        if (windowsObjects[i].TryGetComponent(out IWindowBase window))
          _windows.Add(window.GetType(), window);
        else windowsObjects[i] = null;
      }
    }
  }
}