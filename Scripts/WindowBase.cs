using System;
using Cysharp.Threading.Tasks;
using TriInspector;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace WindowsSystem
{
  [DeclareBoxGroup("Buttons")]
  public abstract class WindowBase<T> : MonoBehaviour, IWindowBase where T : IWindowBase
  {
    [field: SerializeField] public GraphicRaycaster interactionsParents;

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

    public Action<Type> OnBeforeShow { get; set; }
    public Action<Type> OnBeforeHide { get; set; }
    public Action<Type> OnAfterShow { get; set; }
    public Action<Type> OnAfterHide { get; set; }

    [ShowInInspector] [ReadOnly] public bool IsShowing { get; protected set; }
    [ShowInInspector] [ReadOnly] public bool IsInteractable => !interactionsParents || interactionsParents.enabled;
    [ShowInInspector] [ReadOnly] public bool InQueue { get; set; } = false;
    [ShowInInspector] [ReadOnly] public int QueuePriority { get; protected set; } = 0;

    public IWindowsService WindowService { get; private set; }

    [Group("Buttons")]
    [Button("Show window")]
    public async UniTask Show(bool isForce = false)
    {
      OnBeforeShow?.Invoke(GetType());
      await ShowAction(isForce);
      IsShowing = true;
      OnAfterShow?.Invoke(GetType());
    }

    [Group("Buttons")]
    [Button("Hide window")]
    public async UniTask Hide(bool isForce = false)
    {
      OnBeforeHide?.Invoke(GetType());
      await HideAction(isForce);
      IsShowing = false;
      OnAfterHide?.Invoke(GetType());
    }

    public async void Toggle()
    {
      await Hide(true);
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

    [Inject]
    protected virtual void Inject(IWindowsService windowsService)
    {
      WindowService = windowsService;
    }

    protected virtual void AwakeAction()
    {
    }

    protected virtual async void StartAction()
    {
      if (IsShowing || gameObject.activeSelf)
      {
        gameObject.SetActive(false);
        await Hide();
      }
      else
      {
        IsShowing = false;
      }
    }

    protected virtual void DestroyAction()
    {
    }

    protected virtual UniTask ShowAction(bool isForce)
    {
      gameObject.SetActive(true);
      return default;
    }

    protected virtual UniTask HideAction(bool isForce)
    {
      gameObject.SetActive(false);
      return default;
    }
  }
}