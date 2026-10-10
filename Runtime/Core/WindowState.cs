namespace WindowsSystem
{
  public enum WindowState
  {
    Hidden,
    Showing,
    Shown,
    Hiding
  }

  public enum InitialWindowState
  {
    Hidden,
    Shown
  }

  public static class WindowStateExtensions
  {
    /// <summary>True for <see cref="WindowState.Showing"/> and <see cref="WindowState.Shown"/>.</summary>
    public static bool IsVisible(this WindowState state) =>
      state == WindowState.Showing || state == WindowState.Shown;

    public static bool IsTransitioning(this WindowState state) =>
      state == WindowState.Showing || state == WindowState.Hiding;
  }
}
