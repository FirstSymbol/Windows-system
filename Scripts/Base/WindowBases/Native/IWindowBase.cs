using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WindowsSystem
{
    public interface IWindowBase
    {
        GameObject gameObject { get; }
        public Action<Type> OnBeforeShow { get; set; }
        public Action<Type> OnBeforeHide { get; set; }
        public Action<Type> OnAfterShow { get; set; }
        public Action<Type> OnAfterHide { get; set; }
        public Action<Type> OnAfterClose { get; set; }
        public bool IsSpawned { get; set; }
        public bool ForceHideOnInit { get; set; }
        public UniTask Show(bool isForce = false, bool showIfAlreadyShowed = false);
        public UniTask Hide(bool isForce = false, bool hideIfAlreadyHidden = false);
        public UniTask Close(bool isForce = false);
        public void AddInQueue(bool hideIfFirstEntry = false, bool hideForce = false);
        public UniTask Toggle(bool isForce = false);
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