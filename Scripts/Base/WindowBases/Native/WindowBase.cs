using System;
using Cysharp.Threading.Tasks;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using UnityEngine.UI;
using WindowsSystem.Scripts.Base.AnimAction;
using WindowsSystem.Scripts.DefaultPresets.AnimActions;
using Logger = ExtDebugLogger.Logger;

namespace WindowsSystem
{
  public abstract class WindowBase<T> : WindowBaseInterlayer, IWindowBase where T : IWindowBase
  {
    [field: SerializeField] public GraphicRaycaster interactionsParents;
    
    [field: SerializeReference] protected ShowAction showAction = new InstantShow();
    [field: SerializeReference] protected HideAction hideAction = new InstantHide();
    
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
    
    [field: SerializeField] public bool ForceHideOnInit { get; set; } = true;

#if ODIN_INSPECTOR
    [ShowInInspector] [ReadOnly] 
#else
    [field: SerializeField]    
#endif
    public bool IsShowing { get; protected set; }
#if ODIN_INSPECTOR
    [ShowInInspector] [ReadOnly] 
#else
    [field: SerializeField]    
#endif
    public bool IsInteractable => !interactionsParents || interactionsParents.enabled;
#if ODIN_INSPECTOR
    [ShowInInspector] [ReadOnly] 
#else
    [field: SerializeField]    
#endif
    public bool InQueue { get; set; } = false;
#if ODIN_INSPECTOR
    [ShowInInspector] [ReadOnly] 
#else
    [field: SerializeField]    
#endif
    public int QueuePriority { get; protected set; } = 0;

    public IWindowsService WindowService { get; private set; }
    
    protected virtual void Awake()
    {
      AnimActionInit();
      WindowService = WindowsService.Instance;
      if (WindowService == null)
        WindowsService.OnInitialize += WindowsServiceOnOnInitialize;
      else
        WindowsServiceOnOnInitialize();

      AwakeAction();
    }

    private void WindowsServiceOnOnInitialize()
    {
      Init();
      WindowsService.OnInitialize -= WindowsServiceOnOnInitialize;
    }

    public async override void Init()
    {
      if (_isInitialized || gameObject == null)
        return;
      WindowService = WindowsService.Instance;
      WindowService.RegisterWindow(this);
      if (ForceHideOnInit) await Hide(true, true);
      else await Show(true, true);
      _isInitialized = true;
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
      StartAction();
    }
    
    private void OnDestroy()
    {
      WindowService ??= WindowsService.Instance;
      WindowService.UnregisterWindow(this);
      DestroyAction();
    }
    
#if ODIN_INSPECTOR
    [FoldoutGroup("Base Buttons")]
    [Button("Show window")]
#endif
    public async UniTask Show(bool isForce = false, bool showIfAlreadyShowed = false)
    {
      if (!showIfAlreadyShowed && IsShowing)
        return;
      OnBeforeShow?.Invoke(GetType());
      await showAction.Show(isForce);
      IsShowing = true;
      OnAfterShow?.Invoke(GetType());
    }

#if ODIN_INSPECTOR
    [FoldoutGroup("Base Buttons")]
    [Button("Hide window")]
#endif
    public async UniTask Hide(bool isForce = false, bool hideIfAlreadyHidden = false)
    {
      if (!hideIfAlreadyHidden && !IsShowing)
        return;
      OnBeforeHide?.Invoke(GetType());
      await hideAction.Hide(isForce);
      IsShowing = false;
      OnAfterHide?.Invoke(GetType());
    }
    
#if ODIN_INSPECTOR
    [FoldoutGroup("Base Buttons")]
    [Button("Close window")]
#endif
    public async UniTask Close(bool isForce = false)
    {
      await hideAction.Hide(isForce);
      OnAfterClose?.Invoke(GetType());
      Destroy(gameObject);
    }
    
#if ODIN_INSPECTOR
    [FoldoutGroup("Base Buttons")]
    [Button]
#endif
    public void AddInQueue(bool hideIfFirstEntry = false, bool hideForce = false)
    {
      WindowService.QueueController.AddWindowInQueue(this, hideIfFirstEntry, hideForce);
    }
    
    public async UniTask Toggle(bool isForce = false)
    {
      if (IsShowing)
        await Hide(isForce);
      else
        await Show(isForce);
    }

    /// <summary>
    ///   Enable interaction GraphicRaycaster component.
    /// </summary>
#if ODIN_INSPECTOR
    [FoldoutGroup("Interaction Buttons")]
    [Button("Enable Interaction")]
#endif
    public void EnableInteract()
    {
      if (interactionsParents) interactionsParents.enabled = true;
    }

    /// <summary>
    ///   Disable interaction GraphicRaycaster component.
    /// </summary>
#if ODIN_INSPECTOR
    [FoldoutGroup("Interaction Buttons")]
    [Button("Disable Interaction")]
#endif
    public void DisableInteract()
    {
      if (interactionsParents) interactionsParents.enabled = false;
    }

    /// <summary>
    ///   Toggle interaction GraphicRaycaster component.
    /// </summary>
#if ODIN_INSPECTOR
    [FoldoutGroup("Interaction Buttons")]
    [Button("Toggle Interaction")]
#endif
    public void ToggleInteract()
    {
      if (interactionsParents) interactionsParents.enabled = !interactionsParents.enabled;
    }
    protected virtual async void AwakeAction()
    {
    }

    protected virtual async void StartAction()
    {
    }

    protected virtual void DestroyAction() { }
  }
}