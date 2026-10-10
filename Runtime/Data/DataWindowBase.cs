using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WindowsSystem
{
  /// <summary>
  /// A window that displays a data payload. One instance can be shown many times with different data,
  /// including several queued requests with their own data.
  /// </summary>
  public abstract class DataWindowBase<TData> : WindowBase
  {
    [Tooltip("Rebind DefaultData once the window is hidden.")]
    [SerializeField] private bool resetToDefaultWhenHidden;

    /// <summary>Data currently bound to the window.</summary>
    public TData Data { get; private set; }

    /// <summary>Data bound by <see cref="ResetToDefault"/>.</summary>
    protected virtual TData DefaultData => default;

    /// <summary>Applies data to the window visuals.</summary>
    protected abstract void Bind(TData data);

    public void SetData(TData data)
    {
      Data = data;
      Bind(data);
    }

    public void ResetToDefault() => SetData(DefaultData);

    public UniTask ShowAsync(TData data, bool instant = false, CancellationToken cancellationToken = default)
    {
      SetData(data);
      return ShowAsync(instant, cancellationToken);
    }

    /// <summary>Queues the window with its own data. The data is bound right before the window is shown.</summary>
    public bool Enqueue(TData data, int? priority = null)
    {
      if (Service == null)
      {
        WindowsLog.Error($"'{name}' is not attached to a WindowsService and cannot be queued.", this);
        return false;
      }

      return Service.Queue.Enqueue(this, data, priority);
    }

    protected override void OnHidden()
    {
      base.OnHidden();
      if (resetToDefaultWhenHidden)
        ResetToDefault();
    }
  }
}
