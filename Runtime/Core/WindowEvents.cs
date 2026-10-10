using System;
using UnityEngine.Events;

namespace WindowsSystem
{
  /// <summary>Designer-facing callbacks of a window.</summary>
  [Serializable]
  public sealed class WindowEvents
  {
    public UnityEvent onShowing = new();
    public UnityEvent onShown = new();
    public UnityEvent onHiding = new();
    public UnityEvent onHidden = new();
  }
}
