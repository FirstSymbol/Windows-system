# Migrating from 0.x to 1.0

1.0 is a rewrite of the public API. Dependencies on TriInspector, Odin and ExtDebugLogger were removed;
UniTask is the only dependency.

## Assemblies and namespaces

| 0.x | 1.0 |
| --- | --- |
| Assembly `WindowsSystem` | `FirstSymbol.WindowsSystem` (+ `FirstSymbol.WindowsSystem.Editor`) |
| `WindowsSystem.Scripts.Base.AnimAction` | `WindowsSystem.Animations` |
| `WindowsSystem.Resolver` | removed |
| `WindowsSystem.Log` | `WindowsLog` in `WindowsSystem` |

## Types

| 0.x | 1.0 |
| --- | --- |
| `WindowBase<T>` | `WindowBase` (no generic parameter, registered by its actual type) |
| `PooledWindowBase<TWindow, TData>` | `DataWindowBase<TData>` |
| `IPooledWindow`, `QueueWindowItem`, `WindowsQueueController`, `WindowsGroupBase`, `WindowBaseInterlayer` | removed |
| `ShowAction` / `HideAction` | `WindowAnimation` (one type for both directions) |
| `InstantShow` / `InstantHide` | `InstantAnimation` |
| `DependencyResolver`, `IDependencyResolver` | removed — pass `IWindowsProvider` to the constructor |
| `WSLogTag`, `WSLogColorsConfig` | `WindowsLog` |

## Service

| 0.x | 1.0 |
| --- | --- |
| `new WindowsService(new DependencyResolver(...))` | `new WindowsService(provider, defaultParent)` |
| `WindowsService.Instance`, `WindowsService.OnInitialize` | removed (windows attach automatically); use DI |
| `Windows` (`Dictionary<Type, IWindowBase>`) | `Windows` (`IReadOnlyCollection<WindowBase>`) |
| `ShownWindows` | `Windows.Where(w => w.IsVisible)` |
| `GetWindow<T>()`, `TryGetWindow`, `ExistWindow` | `Get<T>()`, `TryGet`, `IsRegistered` |
| `ShowWindow<T>()` / `HideWindow<T>()` / `ToggleWindow<T>()` | `ShowAsync<T>()` / `HideAsync<T>()` / `ToggleAsync<T>()` |
| `CloseWindow<T>()` | `CloseAsync<T>()` |
| `SpawnWindow<T>(pos, parent)` | `SpawnAsync<T>(parent, pos)` |
| `OpenWindow<T>(pos, parent, forceHideOnInit)` | `OpenAsync<T>(parent, pos, instant)` |
| `QueueController.AddWindowInQueue(window)` | `Queue.Enqueue(window)` or `window.Enqueue()` |
| `QueueController.AddWindowInQueue(pooled, index, ...)` | `dataWindow.Enqueue(data, priority)` |
| `QueueController.AddWindowsGroupInQueue(group)` | `Queue.EnqueueRange(types)` |
| `QueueController.Next()` | `Queue.Skip()` |

## Windows

| 0.x | 1.0 |
| --- | --- |
| `Show(isForce, showIfAlreadyShowed)` | `ShowAsync(instant)` |
| `Hide(isForce, hideIfAlreadyHidden)` | `HideAsync(instant)` |
| `Close(isForce)` | `CloseAsync(instant)` |
| `IsShowing` | `IsVisible` / `State` |
| `ForceHideOnInit` | `InitialState` (`Hidden` / `Shown`) |
| `interactionsParents` (`GraphicRaycaster`) + `EnableInteract/DisableInteract/ToggleInteract` | `CanvasGroup` + `Interactable` |
| `OnBeforeShow`, `OnAfterShow`, `OnBeforeHide`, `OnAfterHide` (`Action<Type>`) | `StateChanged` event, `OnShowing/OnShown/OnHiding/OnHidden` hooks, `Events` UnityEvents |
| `OnAfterClose` | `Closed` event / `OnClosed` hook |
| `AwakeAction`, `StartAction`, `DestroyAction` | override `Awake` / `Start` / `OnDestroy` (call base) or `OnInitialized` |

### Data windows

```csharp
// 0.x
public class TestWindow : PooledWindowBase<TestWindow, MyData>
{
    protected override IDictionary<int, MyData> WindowData => datas;
    protected override MyData DefaultData { get; set; }
    protected override void ExtractAction(in MyData data) { ... }
}

// 1.0
public class TestWindow : DataWindowBase<MyData>
{
    protected override MyData DefaultData => defaultData;
    protected override void Bind(MyData data) { ... }
}
```

The index-based data dictionary is gone: keep presets in your own window if you need them and call
`Enqueue(data)` / `ShowAsync(data)`.

## Serialized data

- Animations are stored in new fields (`showAnimation` / `hideAnimation`). Re-assign them on existing
  windows; custom `ShowAction`/`HideAction` classes must be ported to `WindowAnimation` (see README).
- `WindowsProvider` now stores `WindowBase` references in `windows` instead of GameObjects in
  `windowsObjects`. Re-assign the prefabs (the inspector button *Find window prefabs* does it in one click).
- Add a `CanvasGroup` to windows that used `GraphicRaycaster` for interaction control.
