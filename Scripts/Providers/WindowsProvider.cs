using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WindowsSystem.Providers
{
  [CreateAssetMenu(fileName = "WindowsProvider Config", menuName = "Configs/Providers/WindowsSystem/WindowsProvider", order = 0)]
  public class WindowsProvider : ScriptableObject, IWindowsProvider, ISerializationCallbackReceiver
  {
    public List<GameObject> windowsObjects;
    private Dictionary<Type, IWindowBase>  _windows = new();
    
    public void OnBeforeSerialize()
    {
      windowsObjects = windowsObjects.Distinct().ToList();
      _windows ??= new Dictionary<Type, IWindowBase>();
      _windows.Clear();
      
      for (int i = 0; i < windowsObjects.Count; i++)
      {
        if (windowsObjects[i] == null)
          continue;
        if (windowsObjects[i].TryGetComponent(out IWindowBase window))
        {
          _windows.Add(window.GetType(), window);
        }
        else
          windowsObjects[i] = null;
      }
    }

    public void OnAfterDeserialize()
    {
      windowsObjects = _windows.Values.Select(t=> t.gameObject).ToList();
    }
    
    public T GetWindowPrefab<T>() where T : MonoBehaviour, IWindowBase
    {
      return _windows[typeof(T)] as T;
    }

    
  }
}