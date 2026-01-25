using System;
using Cysharp.Threading.Tasks;

namespace WindowsSystem
{
  public interface IWindowBase
  {
    public Action<Type> OnBeforeShow { get; set; }
    public Action<Type> OnBeforeHide { get; set; }
    public Action<Type> OnAfterShow { get; set; }
    public Action<Type> OnAfterHide { get; set; }
    public Action<Type> OnAfterClose { get; set; }
    public bool IsSpawned { get; set; }
    public bool DisableShowHideActionsOnStart { get; set; }
    public UniTask Show(bool isForce = false);
    public UniTask Hide(bool isForce = false);
    public UniTask Close(bool isForce = false);
    public void Toggle(bool isForce = false);
    public bool IsShowing { get; }
    public bool InQueue { get; set; }
    public IWindowsService WindowService { get; }
    public int QueuePriority { get; }
    public bool IsInteractable { get; }
    public void EnableInteract();
    public void DisableInteract();
    public void ToggleInteract();
  }
}