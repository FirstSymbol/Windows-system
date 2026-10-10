# Windows System

Async UI windows for Unity 6: every window is a small state machine with cancellable show/hide animations,
windows can be queued by priority (with their own data), spawned from prefabs through a provider and inspected
live in the editor.

- **One dependency** — [UniTask](https://github.com/Cysharp/UniTask).
- **DI-agnostic** — works with Zenject, VContainer or plain `new`.
- **Safe async** — `Show` during `Hide` (and vice versa) cancels the running animation and reverses it;
  repeated calls in the same direction await the running transition.
- **Editor tooling** — window inspector with live state and controls, animation type picker,
  provider inspector, `Window > Windows System > Debugger`.

## Installation

1. Install [UniTask](https://github.com/Cysharp/UniTask#upm-package) (UPM git URL or OpenUPM).
2. Add this package:
   - Package Manager → *Add package from git URL* → `https://github.com/FirstSymbol/Windows-system.git`
   - or copy the folder into `Assets/` / `Packages/`.

> UniTask is not listed in `package.json` dependencies because it is not published in the Unity registry.

## Quick start

### 1. Create a window

```csharp
using WindowsSystem;

public sealed class SettingsWindow : WindowBase
{
    // Optional hooks
    protected override void OnShown() { /* ... */ }
}
```

Add the component to a UI object (a `CanvasGroup` is recommended — it is used for input blocking and fades).
Pick show/hide animations in the inspector: *Instant, Fade, Scale, Slide, Parallel*.

### 2. Create the service

The service has no static instance: create one and dispose it with its scope.
Windows in the scene attach themselves automatically, including windows that start inactive.

```csharp
// Plain C#
var windows = new WindowsService(provider, defaultParent);

// Zenject
Container.BindInterfacesAndSelfTo<WindowsService>()
    .FromMethod(ctx => new WindowsService(ctx.Container.Resolve<IWindowsProvider>()))
    .AsSingle()
    .NonLazy();

// VContainer
builder.Register<WindowsService>(r => new WindowsService(r.Resolve<IWindowsProvider>()), Lifetime.Singleton)
    .AsImplementedInterfaces();
```

### 3. Use it

```csharp
await windows.ShowAsync<SettingsWindow>();
await windows.HideAsync<SettingsWindow>(instant: true);
await windows.ToggleAsync<SettingsWindow>();

SettingsWindow settings = windows.Get<SettingsWindow>();
settings.Interactable = false;

// Spawn from the provider
var popup = await windows.OpenAsync<RewardPopup>(parent: canvasRect);
await popup.CloseAsync(); // hide + destroy
```

## Windows

| Member | Description |
| --- | --- |
| `State` | `Hidden`, `Showing`, `Shown`, `Hiding` |
| `ShowAsync / HideAsync / ToggleAsync(instant, token)` | Transitions. Cancelling the token completes the transition instantly and throws `OperationCanceledException`. |
| `CloseAsync` | Hides and destroys the window. |
| `Interactable` | Input switch (requires `CanvasGroup`). |
| `Enqueue(priority?)` | Adds the window to the service queue. |
| `MoveTo(anchoredPosition)` | Moves the window and updates the pose used by animations. |
| `StateChanged`, `Closed` | C# events. `Events` holds UnityEvents for designers. |
| `OnInitialized / OnShowing / OnShown / OnHiding / OnHidden / OnClosed` | Overridable hooks. |

Inspector settings: initial state, queue priority, `CanvasGroup`, *Block input during transition*,
*Deactivate when hidden*, animations, events.

> Overriding `Awake` or `OnDestroy`? Call `base.Awake()` / `base.OnDestroy()`.

### Data windows

```csharp
public sealed class MessageWindow : DataWindowBase<string>
{
    [SerializeField] private TMP_Text text;
    protected override void Bind(string data) => text.text = data;
}

await message.ShowAsync("Hello");
message.Enqueue("Queued message", priority: 1); // each request keeps its own data
```

### Custom animations

```csharp
[Serializable, DisplayName("Rotate")]
public sealed class RotateAnimation : TweenWindowAnimation
{
    public float hiddenAngle = 90f;
    private float _from, _to;

    protected override void Begin(in WindowAnimationContext context)
    {
        _from = context.StartsHidden ? hiddenAngle : context.Window.transform.localEulerAngles.z;
        _to = context.IsShow ? 0f : hiddenAngle;
    }

    protected override void Evaluate(in WindowAnimationContext context, float t) =>
        context.Window.transform.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(_from, _to, t));
}
```

Any `[Serializable]` subclass of `WindowAnimation` appears in the inspector dropdown.
`[SubclassSelector]` can be used on your own `[SerializeReference]` fields as well.

## Queue

`service.Queue` shows windows one by one: the current window stays until it is hidden, then the pending request
with the highest priority is shown (FIFO within a priority). A request with a higher priority than the current one
hides it and puts it back at the front of its priority group.

```csharp
windows.Queue.Enqueue<DailyRewardWindow>();
windows.Queue.Enqueue(newsWindow, priority: 10);
windows.Queue.EnqueueRange(new[] { typeof(A), typeof(B) });

windows.Queue.Skip();   // hide the current window
windows.Queue.Clear();  // drop pending requests
windows.Queue.Stop();   // both

windows.Queue.Completed += () => Debug.Log("All popups shown");
```

## Providers

`IWindowsProvider` supplies prefabs for `SpawnAsync` / `OpenAsync`. It is asynchronous, so an
Addressables-backed provider is a few lines. The built-in `WindowsProvider` asset
(*Create → Windows System → Windows Provider*) keeps direct references; its inspector can find every window
prefab in the project and highlights empty or duplicate entries.

## Logging

```csharp
WindowsLog.Level = WindowsLogLevel.Info; // None, Error, Warning (default), Info

// Route to any logger, e.g. ExtDebugLogger:
WindowsLog.Handler = (type, message, context) => ExtDebugLogger.Logger.Log(message);
```

## Debugger

`Window > Windows System > Debugger` lists windows of the open scenes (Edit Mode) or all live windows
(Play Mode) with their state, scene, queue flags and Show / Hide / Toggle / Queue / Ping buttons,
plus the queue panel (current request, pending requests with priorities and data, Skip / Clear / Stop).

## Window layouts

Slots are named places for windows ("Center Popup", "Left Panel", "Toast"...) defined once in a
**Window Layout Library** (*Create → Windows System → Window Layout Library*). Windows are placed into slots
instead of being positioned in every prefab, and stay there when the screen is resized.

1. Add **Window Layout Root** to the object that contains your windows (e.g. a full-screen child of the Canvas)
   and assign the library. Windows use the nearest root in their parents, otherwise the most recently enabled one.
2. Pick a **Default Slot** in a window's *Layout* section — it is applied when the window initializes.
3. Or move windows from code:

```csharp
window.MoveToSlot("Center Popup");                       // id or name
window.MoveToSlot(MainLayoutSlots.LeftPanel, animate: true); // generated constants
await windows.ShowInSlotAsync<SettingsWindow>("Right Panel");
await windows.OpenInSlotAsync<RewardPopup>("Center Popup");
windows.TileVisible(WindowTilingMode.Grid);              // Horizontal, Vertical, Grid, MainAndStack, Cascade, Center, Maximize
```

A slot is an area of the root (normalized anchors + padding) and a size mode:
*Stretch* (fill the area), *Fixed* (size clamped to the area, placed by alignment) or *Fit Aspect*
(largest rect of an aspect, optionally limited). Every slot can have a separate **portrait placement** used when the
root is taller than wide. The root respects `Screen.safeArea` (optional) and re-applies placements on resize.
Windows are placed by anchors, so their own content keeps adapting normally.

**Editor:**
- *Library inspector* — a mock screen with presets (16:9, 21:9, 4:3, 1:1, 3:4, 9:16, 9:19.5) and a simulated safe
  area; click a slot and drag its area or edges; a *Tiling* view shows every tiling mode for 1–8 windows;
  *Generate C# constants* writes a class with the slot ids.
- *Layout Root inspector* — place the selected windows (or all windows under the root) into a slot or tile them in
  Edit Mode; *Revert preview* restores the previous layout exactly. Slots are drawn in the Scene View.
- *Window inspector* — default slot dropdown and slot preview buttons.
- *Debugger* — slot column and *Tile visible* menu.

## Upgrading from 0.x

See [MIGRATION.md](MIGRATION.md).
