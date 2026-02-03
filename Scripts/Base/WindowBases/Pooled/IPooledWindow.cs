namespace WindowsSystem
{
  public interface IPooledWindow : IWindowBase
  {
    void ExtractData(int index = -1, bool removeAfterExtract = false);
    void ReturnToDefault();

    public void AddInQueue(int index, bool removeAfterExtract, bool returnToDefault = true,
      bool hideIfFirstEntry = true, bool hideForce = false);
  }
}