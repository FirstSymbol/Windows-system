using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WindowsSystem.Providers
{
  /// <summary>Provider that keeps direct references to window prefabs.</summary>
  [CreateAssetMenu(fileName = "WindowsProvider", menuName = "Windows System/Windows Provider", order = 0)]
  public sealed class WindowsProvider : ScriptableObject, IWindowsProvider
  {
    [SerializeField] private List<WindowBase> windows = new();

    private Dictionary<Type, WindowBase> _lookup;

    public IReadOnlyList<WindowBase> Windows => windows;

    public UniTask<WindowBase> GetPrefabAsync(Type windowType, CancellationToken cancellationToken = default) =>
      UniTask.FromResult(GetPrefab(windowType));

    public T GetPrefab<T>() where T : WindowBase => GetPrefab(typeof(T)) as T;

    public WindowBase GetPrefab(Type windowType)
    {
      if (windowType == null)
        return null;

      EnsureLookup();
      return _lookup.TryGetValue(windowType, out WindowBase prefab) ? prefab : null;
    }

    public bool Contains(Type windowType) => GetPrefab(windowType) != null;

    private void OnEnable() => _lookup = null;

    private void OnValidate() => _lookup = null;

    private void EnsureLookup()
    {
      if (_lookup != null)
        return;

      _lookup = new Dictionary<Type, WindowBase>();
      if (windows == null)
        return;

      foreach (WindowBase prefab in windows)
      {
        if (prefab == null)
          continue;

        Type type = prefab.GetType();
        if (!_lookup.TryAdd(type, prefab))
          WindowsLog.Warning($"{name}: '{type.Name}' is listed more than once, '{prefab.name}' is ignored.", this);
      }
    }
  }
}
