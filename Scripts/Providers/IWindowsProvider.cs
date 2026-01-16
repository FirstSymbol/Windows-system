using UnityEngine;
using WindowsSystem;

namespace Windows_system.Scripts.Providers
{
  public interface IWindowsProvider
  {
    public T GetWindowPrefab<T>() where T : MonoBehaviour, IWindowBase;
  }
}