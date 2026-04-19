using UnityEngine;

namespace WindowsSystem.Providers
{
  public interface IWindowsProvider
  {
    public T GetWindowPrefab<T>() where T : MonoBehaviour, IWindowBase;
  }
}