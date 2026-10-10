# Changelog

## [1.0.0]

Rewrite. See [MIGRATION.md](MIGRATION.md).

### Added
- Window state machine (`Hidden`, `Showing`, `Shown`, `Hiding`) with cancellable transitions:
  opposite calls reverse the running animation, repeated calls await it.
- Animations: `Instant`, `Fade`, `Scale`, `Slide`, `Parallel`; `TweenWindowAnimation` base with easing,
  delay and unscaled time; inspector type picker (`[SubclassSelector]`).
- `DataWindowBase<TData>` — windows with data payloads; queued requests keep their own data.
- Priority queue with preemption, `Skip`, `Clear`, `Stop` and events.
- Asynchronous `IWindowsProvider` (Addressables-friendly), typed `WindowsProvider` asset.
- `CanvasGroup`-based interactivity and input blocking during transitions.
- `WindowsLog` with levels and a pluggable handler.
- Editor: window inspector with live state and Play Mode controls, provider inspector with validation
  and prefab search, `Window > Windows System > Debugger`.
- EditMode tests for the queue.

### Changed
- Package layout (`Runtime/`, `Editor/`, `Tests/`), assemblies renamed to `FirstSymbol.WindowsSystem*`.
- `WindowsService` is a disposable, non-static service; windows attach through an internal tracker.

### Fixed
- Windows registered under the generic argument instead of their actual type.
- Race conditions between Show/Hide, `Close` not resetting state, fire-and-forget `Toggle`/`Close`.
- Double registration and leaked static subscriptions during initialization.
- `NullReferenceException` in `OnDestroy` without a service and in `OpenWindow` when the prefab is missing.
- All windows (including `DontDestroyOnLoad`) being unregistered on any scene unload.
- Finalizer-based unsubscription that never ran.
- Spawn position treated as a world position.
- `TryGetWindow<T>` returning true on a failed cast.
- `WindowsProvider` crashing on null lists and duplicate types.
- Queue losing windows on preemption and never resetting `InQueue`.

### Removed
- TriInspector, Odin and ExtDebugLogger dependencies.
- `DependencyResolver`, static `WindowsService.Instance`, `WindowsQueueController`, `WindowsGroupBase`,
  `GraphicRaycaster` interaction.
