using System;
using Cysharp.Threading.Tasks;
using TriInspector;
using UnityEngine;
using UnityEngine.UI;
using WindowsSystem.Scripts.Base.AnimAction;
using WindowsSystem.Scripts.DefaultPresets.AnimActions;
using Zenject;

namespace WindowsSystem
{
  [DeclareBoxGroup("Base Buttons")]
  public abstract class WindowBase<T> : MonoBehaviour, IWindowBase where T : IWindowBase
  {
    [field: SerializeField] public GraphicRaycaster interactionsParents;
    
    [field: SerializeReference] protected HideAction hideAction = new InstantHide();
    [field: SerializeReference] protected ShowAction showAction = new InstantShow();
    
    public Action<Type> OnBeforeShow { get; set; }
    public Action<Type> OnBeforeHide { get; set; }
    public Action<Type> OnAfterShow { get; set; }
    public Action<Type> OnAfterHide { get; set; }
    public Action<Type> OnAfterClose { get; set; }
    
    /// <summary>
    /// True if window is spawned from service.
    /// </summary>
    public bool IsSpawned { get; set; } = false;
    
    /// <summary>
    /// True if window is spawned from service.
    /// </summary>
    public bool DisableShowHideActionsOnStart { get; set; } = false;

    [ShowInInspector] [ReadOnly] public bool IsShowing { get; protected set; }
    [ShowInInspector] [ReadOnly] public bool IsInteractable => !interactionsParents || interactionsParents.enabled;
    [ShowInInspector] [ReadOnly] public bool InQueue { get; set; } = false;
    [ShowInInspector] [ReadOnly] public int QueuePriority { get; protected set; } = 0;

    public IWindowsService WindowService { get; private set; }

    [Inject]
    protected virtual void Inject(IWindowsService windowsService)
    {
      WindowService = windowsService;
    }
    
    protected virtual void Awake()
    {
      AnimActionInit();
      AwakeAction();
    }

    private void AnimActionInit()
    {
      hideAction ??= new InstantHide();
      showAction ??= new InstantShow();
      showAction.gameObject ??= gameObject;
      hideAction.gameObject ??= gameObject;
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
    
    [Group("Base Buttons")]
    [Button("Show window")]
    public async UniTask Show(bool isForce = false)
    {
      OnBeforeShow?.Invoke(GetType());
      await showAction.Show(isForce);
      IsShowing = true;
      OnAfterShow?.Invoke(GetType());
    }

    [Group("Base Buttons")]
    [Button("Hide window")]
    public async UniTask Hide(bool isForce = false)
    {
      OnBeforeHide?.Invoke(GetType());
      await hideAction.Hide(isForce);
      IsShowing = false;
      OnAfterHide?.Invoke(GetType());
    }

    [Group("Base Buttons")]
    [Button("Close window")]
    public async UniTask Close(bool isForce = false)
    {
      await hideAction.Hide(isForce);
      OnAfterClose?.Invoke(GetType());
      Destroy(gameObject);
    }
    public async void Toggle(bool isForce = false)
    {
      if (IsShowing)
        await Hide(isForce);
      else
        await Show(isForce);
    }

    /// <summary>
    ///   Enable interaction GraphicRaycaster component.
    /// </summary>
    [Group("Buttons")]
    [Button("Enable Interaction")]
    public void EnableInteract()
    {
      if (interactionsParents) interactionsParents.enabled = true;
    }

    /// <summary>
    ///   Disable interaction GraphicRaycaster component.
    /// </summary>
    [Group("Buttons")]
    [Button("Disable Interaction")]
    public void DisableInteract()
    {
      if (interactionsParents) interactionsParents.enabled = false;
    }

    /// <summary>
    ///   Toggle interaction GraphicRaycaster component.
    /// </summary>
    [Group("Buttons")]
    [Button("Toggle Interaction")]
    public void ToggleInteract()
    {
      if (interactionsParents) interactionsParents.enabled = !interactionsParents.enabled;
    }
    protected virtual void AwakeAction()
    {
    }

    protected virtual async void StartAction()
    {
      if (!DisableShowHideActionsOnStart) await Hide(true);
    }

    protected virtual void DestroyAction() { }
  }
}