namespace WindowsSystem
{
  public interface IPooledWindow : IWindowBase
  {
    void ExtractData(int index = -1, bool removeAfterExtract = false);
    void ReturnToDefault();
  }
}