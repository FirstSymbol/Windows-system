namespace WindowsSystem
{
  public interface IPooledWindow
  {
    void ExtractData(int index = -1, bool removeAfterExtract = false);
  }
}