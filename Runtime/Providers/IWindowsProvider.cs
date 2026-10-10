using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace WindowsSystem.Providers
{
  /// <summary>
  /// Supplies window prefabs to <see cref="IWindowsService"/>. Asynchronous so it can be backed
  /// by Addressables or any other loading system.
  /// </summary>
  public interface IWindowsProvider
  {
    /// <returns>The prefab of the window type, or null if it is unknown.</returns>
    UniTask<WindowBase> GetPrefabAsync(Type windowType, CancellationToken cancellationToken = default);
  }
}
