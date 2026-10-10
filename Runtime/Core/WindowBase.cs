using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using WindowsSystem.Animations;
using WindowsSystem.Layouts;

namespace WindowsSystem
{
  /// <summary>
  /// Base class for every window. Registers itself automatically and is controlled either directly
  /// or through <see cref="IWindowsService"/>.
  /// </summary>
  [DisallowMultipleComponent]
  public abstract class WindowBase : MonoBehaviour, IWindow
  {
    [Tooltip("State applied instantly when the window is initialized.")]
    [SerializeField] private InitialWindowState initialState = InitialWindowState.Hidden;

    [Tooltip("Requests with a higher priority are shown first and preempt lower ones.")]
    [SerializeField] private int queuePriority;

    [Tooltip("Optional. Used for interactivity, input blocking and fade animations.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Ignore input while the window is animating.")]
    [SerializeField] private bool blockInputDuringTransition = true;

    [Tooltip("Deactivate the GameObject once the window is hidden.")]
    [SerializeField] private bool deactivateWhenHidden = true;

    [SerializeReference, SubclassSelector] private WindowAnimation showAnimation = new InstantAnimation();
    [SerializeReference, SubclassSelector] private WindowAnimation hideAnimation = new InstantAnimation();

    [Tooltip("Slot of the Window Layout Root the window is placed into when initialized. Empty = keep the prefab layout.")]
    [SerializeField] private WindowSlotRef defaultSlot;

    [SerializeField] private WindowEvents events = new();

    private CancellationTokenSource _transitionCts;
    private UniTask _transition = UniTask.CompletedTask;
    private int _transitionId;
    private bool _initialized;
    private bool _interactable = true;
    private bool _restPoseCaptured;
    private WindowRestPose _restPose;
    private CancellationTokenSource _layoutMoveCts;

    public WindowState State { get; private set; } = WindowState.Hidden;
    public bool IsVisible => State.IsVisible();
    public bool IsInitialized => _initialized;
    public bool IsAlive => this != null;

    /// <summary>True if the window was instantiated by <see cref="IWindowsService"/>.</summary>
    public bool IsSpawned { get; internal set; }

    /// <summary>Service the window is attached to. Null until a service is created.</summary>
    public IWindowsService Service { get; internal set; }

    public bool InQueue => Service != null && Service.Queue.Contains(this);

    public int QueuePriority
    {
      get => queuePriority;
      set => queuePriority = value;
    }

    public InitialWindowState InitialState
    {
      get => initialState;
      set => initialState = value;
    }

    public bool DeactivateWhenHidden
    {
      get => deactivateWhenHidden;
      set => deactivateWhenHidden = value;
    }

    public bool BlockInputDuringTransition
    {
      get => blockInputDuringTransition;
      set
      {
        blockInputDuringTransition = value;
        ApplyInputState();
      }
    }

    public WindowAnimation ShowAnimation
    {
      get => showAnimation;
      set => showAnimation = value;
    }

    public WindowAnimation HideAnimation
    {
      get => hideAnimation;
      set => hideAnimation = value;
    }

    /// <summary>Slot applied on initialization (see <see cref="WindowLayoutRoot"/>).</summary>
    public WindowSlotRef DefaultSlot
    {
      get => defaultSlot;
      set => defaultSlot = value;
    }

    /// <summary>Layout root used by this window (nearest in parents, otherwise the main one).</summary>
    public WindowLayoutRoot LayoutRoot => WindowLayoutRoot.FindFor(this);

    /// <summary>Id of the slot the window is placed in, or null.</summary>
    public string SlotId => LayoutRoot != null ? LayoutRoot.GetSlotId(this) : null;

    public CanvasGroup CanvasGroup => canvasGroup;
    public RectTransform RectTransform => transform as RectTransform;
    public WindowEvents Events => events;

    /// <summary>
    /// Whether the window accepts input while shown. Requires a <see cref="CanvasGroup"/>.
    /// </summary>
    public bool Interactable
    {
      get => _interactable;
      set
      {
        _interactable = value;
        ApplyInputState();
      }
    }

    /// <summary>Pose of the fully shown window used by animations.</summary>
    public WindowRestPose RestPose
    {
      get
      {
        if (!_restPoseCaptured)
          CaptureRestPose();
        return _restPose;
      }
    }

    public event Action<IWindow> StateChanged;
    public event Action<WindowBase> Closed;

    protected virtual void Reset()
    {
      canvasGroup = GetComponent<CanvasGroup>();
    }

    protected virtual void Awake()
    {
      Initialize();
    }

    protected virtual void OnDestroy()
    {
      CancelTransition();
      CancelLayoutMove();
      WindowLayoutRoot.ReleaseEverywhere(this);
      if (State != WindowState.Hidden)
        SetState(WindowState.Hidden, invokeCallbacks: false);
      WindowTracker.Remove(this);
    }

    /// <summary>
    /// Registers the window and applies its initial state. Runs automatically in Awake;
    /// the service also calls it for windows that start inactive.
    /// </summary>
    public void Initialize()
    {
      if (_initialized || !IsAlive)
        return;

      _initialized = true;
      if (canvasGroup == null)
        canvasGroup = GetComponent<CanvasGroup>();

      if (!defaultSlot.IsEmpty)
        WindowLayoutRoot.PlaceDefault(this);

      CaptureRestPose();
      WindowTracker.Add(this);
      StartTransition(initialState == InitialWindowState.Shown, true, CancellationToken.None, silent: true).Forget();
      OnInitialized();
    }

    /// <summary>Stores the current transform as the shown pose used by animations.</summary>
    public void CaptureRestPose()
    {
      _restPose = WindowRestPose.Capture(transform);
      _restPoseCaptured = true;
    }

    /// <summary>Places the window into a slot of its layout root (by reference, id or name).</summary>
    public bool MoveToSlot(WindowSlotRef slot, bool animate = false)
    {
      WindowLayoutRoot root = LayoutRoot;
      if (root == null)
      {
        WindowsLog.Warning($"'{name}' cannot move to slot '{slot}': no WindowLayoutRoot found.", this);
        return false;
      }

      return root.Place(this, slot, animate);
    }

    internal CancellationToken BeginLayoutMove()
    {
      CancelLayoutMove();
      _layoutMoveCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
      return _layoutMoveCts.Token;
    }

    internal void CancelLayoutMove()
    {
      if (_layoutMoveCts == null)
        return;
      _layoutMoveCts.Cancel();
      _layoutMoveCts.Dispose();
      _layoutMoveCts = null;
    }

    /// <summary>Moves the window and makes the new position its rest position.</summary>
    public void MoveTo(Vector2 anchoredPosition)
    {
      if (RectTransform != null)
        RectTransform.anchoredPosition = anchoredPosition;
      else
        transform.localPosition = new Vector3(anchoredPosition.x, anchoredPosition.y, transform.localPosition.z);

      WindowRestPose pose = RestPose;
      pose.anchoredPosition = anchoredPosition;
      pose.localPosition = transform.localPosition;
      _restPose = pose;
    }

    public UniTask ShowAsync(bool instant = false, CancellationToken cancellationToken = default)
    {
      if (!IsAlive)
        return UniTask.CompletedTask;

      Initialize();
      if (State == WindowState.Shown)
        return UniTask.CompletedTask;
      if (State == WindowState.Showing && !instant)
        return _transition;

      return StartTransition(true, instant, cancellationToken);
    }

    public UniTask HideAsync(bool instant = false, CancellationToken cancellationToken = default)
    {
      if (!IsAlive)
        return UniTask.CompletedTask;

      Initialize();
      if (State == WindowState.Hidden)
        return UniTask.CompletedTask;
      if (State == WindowState.Hiding && !instant)
        return _transition;

      return StartTransition(false, instant, cancellationToken);
    }

    public UniTask ToggleAsync(bool instant = false, CancellationToken cancellationToken = default) =>
      IsVisible ? HideAsync(instant, cancellationToken) : ShowAsync(instant, cancellationToken);

    /// <summary>Hides the window and destroys its GameObject.</summary>
    public async UniTask CloseAsync(bool instant = false, CancellationToken cancellationToken = default)
    {
      if (!IsAlive)
        return;

      await HideAsync(instant, cancellationToken);
      if (!IsAlive || cancellationToken.IsCancellationRequested)
        return;

      OnClosed();
      Closed?.Invoke(this);
      Destroy(gameObject);
    }

    /// <summary>Adds the window to the service queue.</summary>
    public bool Enqueue(int? priority = null)
    {
      if (Service == null)
      {
        WindowsLog.Error($"'{name}' is not attached to a WindowsService and cannot be queued.", this);
        return false;
      }

      return Service.Queue.Enqueue(this, priority);
    }

    protected virtual void OnInitialized() { }
    protected virtual void OnShowing() { }
    protected virtual void OnShown() { }
    protected virtual void OnHiding() { }
    protected virtual void OnHidden() { }
    protected virtual void OnClosed() { }

    private UniTask StartTransition(bool show, bool instant, CancellationToken cancellationToken, bool silent = false)
    {
      // Bump the id first so the cancelled transition recognizes it was superseded.
      int id = ++_transitionId;
      CancelTransition();

      _transitionCts = cancellationToken.CanBeCanceled
        ? CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, cancellationToken)
        : CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

      _transition = RunTransition(id, show, instant, silent, _transitionCts.Token).Preserve();
      return _transition;
    }

    private async UniTask RunTransition(int id, bool show, bool instant, bool silent, CancellationToken token)
    {
      WindowState previous = State;
      WindowAnimation animation = show ? showAnimation : hideAnimation;

      if (show && !gameObject.activeSelf)
        gameObject.SetActive(true);

      SetState(show ? WindowState.Showing : WindowState.Hiding, !silent);
      if (id != _transitionId)
        return;

      try
      {
        if (animation != null)
          await animation.Play(new WindowAnimationContext(this, show, instant, previous), token);
      }
      catch (OperationCanceledException)
      {
        // Handled below through the token state.
      }
      catch (Exception exception)
      {
        WindowsLog.Error($"'{name}' animation failed: {exception}", this);
      }

      if (id != _transitionId || !IsAlive)
        return; // Superseded by another transition or destroyed.

      if (token.IsCancellationRequested)
      {
        // Cancelled by the caller: finish instantly so the window is never left half-animated.
        animation?.Play(new WindowAnimationContext(this, show, true, previous), CancellationToken.None).Forget();
        Complete(show, silent);
        throw new OperationCanceledException(token);
      }

      Complete(show, silent);
    }

    private void Complete(bool show, bool silent)
    {
      if (show)
      {
        SetState(WindowState.Shown, !silent);
        return;
      }

      SetState(WindowState.Hidden, !silent);
      if (deactivateWhenHidden && IsAlive && State == WindowState.Hidden)
        gameObject.SetActive(false);
    }

    private void CancelTransition()
    {
      if (_transitionCts == null)
        return;

      _transitionCts.Cancel();
      _transitionCts.Dispose();
      _transitionCts = null;
    }

    private void SetState(WindowState state, bool invokeCallbacks = true)
    {
      if (State == state)
        return;

      State = state;
      ApplyInputState();

      if (invokeCallbacks)
      {
        switch (state)
        {
          case WindowState.Showing:
            OnShowing();
            events?.onShowing?.Invoke();
            break;
          case WindowState.Shown:
            OnShown();
            events?.onShown?.Invoke();
            break;
          case WindowState.Hiding:
            OnHiding();
            events?.onHiding?.Invoke();
            break;
          case WindowState.Hidden:
            OnHidden();
            events?.onHidden?.Invoke();
            break;
        }
      }

      StateChanged?.Invoke(this);
    }

    private void ApplyInputState()
    {
      if (canvasGroup == null)
        return;

      bool acceptsInput = State == WindowState.Shown ||
                          (State.IsTransitioning() && !blockInputDuringTransition);

      canvasGroup.blocksRaycasts = acceptsInput;
      canvasGroup.interactable = acceptsInput && _interactable;
    }
  }
}
