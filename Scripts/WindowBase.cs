using System;
using Cysharp.Threading.Tasks;
using TriInspector;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace WindowsSystem
{
    public interface IWindowBase
    {
        public Action<Type> OnBeforeShow { get; set; }
        public Action<Type> OnBeforeHide { get; set; }
        public Action<Type> OnAfterShow { get; set; }
        public Action<Type> OnAfterHide { get; set; }
        public UniTask Show();
        public UniTask Hide();
        public void Toggle();
        public bool IsShowing { get; }
        public bool InQueue { get; set; }
        public IWindowsService WindowService { get; }
        public int QueuePriority { get; }
        public bool IsInteractable { get; }
        public void EnableInteract();
        public void DisableInteract();
        public void ToggleInteract();
    }

    [DeclareBoxGroup("Buttons")]
    public abstract class WindowBase<T> : MonoBehaviour, IWindowBase where T : IWindowBase
    {
        public Action<Type> OnBeforeShow { get; set; }
        public Action<Type> OnBeforeHide { get; set; }
        public Action<Type> OnAfterShow { get; set; }
        public Action<Type> OnAfterHide { get; set; }


        [field: SerializeField] public GraphicRaycaster interactionsParents;
        
        [ShowInInspector, ReadOnly] public bool IsShowing { get; protected set; } = false;
        [ShowInInspector, ReadOnly] public bool IsInteractable => !interactionsParents || interactionsParents.enabled;
        [ShowInInspector, ReadOnly] public bool InQueue { get; set; } = false;
        [ShowInInspector, ReadOnly] public int QueuePriority { get; protected set; } = 0;

        public IWindowsService WindowService { get; private set; }

        [Inject]
        protected virtual void Inject(IWindowsService windowsService)
        {
            WindowService = windowsService;
        }

        protected virtual void Awake()
        {
            AwakeAction();
        }
        
        protected virtual void Start()
        {
            WindowService.RegisterWindow(this);
            StartAction();
        }

        private void OnDestroy()
        {
            WindowService.UnregisterWindow(this);
            DestroyAction();
        }

        [Group("Buttons")]
        [Button("Show window")]
        public async UniTask Show()
        {
            OnBeforeShow?.Invoke(GetType());
            await ShowAction();
            IsShowing = true;
            OnAfterShow?.Invoke(GetType());
        }

        [Group("Buttons")]
        [Button("Hide window")]
        public async UniTask Hide()
        {
            OnBeforeHide?.Invoke(GetType());
            await HideAction();
            IsShowing = false;
            OnAfterHide?.Invoke(GetType());
        }

        public async void Toggle()
        {
            if (IsShowing)
                await Hide();
            else
                await Show();
        }
        
        protected virtual void AwakeAction(){}
        
        protected virtual async void StartAction()
        {
            if (IsShowing || gameObject.activeSelf)
            {
                gameObject.SetActive(false);
                await Hide();
            }
            else
                IsShowing = false;
        }

        protected virtual void DestroyAction()
        {
        }

        protected virtual UniTask ShowAction()
        {
            gameObject.SetActive(true);
            return default;
        }

        protected virtual UniTask HideAction()
        {
            gameObject.SetActive(false);
            return default;
        }

        /// <summary>
        /// Enable interaction GraphicRaycaster component.
        /// </summary>
        [Group("Buttons")]
        [Button("Enable Interaction")]
        public void EnableInteract()
        {
            if (interactionsParents) interactionsParents.enabled = true;
        }

        /// <summary>
        /// Disable interaction GraphicRaycaster component.
        /// </summary>
        [Group("Buttons")]
        [Button("Disable Interaction")]
        public void DisableInteract()
        {
            if (interactionsParents) interactionsParents.enabled = false;
        }

        /// <summary>
        /// Toggle interaction GraphicRaycaster component.
        /// </summary>
        [Group("Buttons")]
        [Button("Toggle Interaction")]
        public void ToggleInteract()
        {
            if (interactionsParents) interactionsParents.enabled = !interactionsParents.enabled;
        }
    }
}