using Cysharp.Threading.Tasks;
using WindowsSystem.Scripts.Base.AnimAction;

namespace WindowsSystem.Scripts.DefaultPresets.AnimActions
{
  public class InstantHide : HideAction
  {
    public override UniTask Hide(bool isForce = false)
    {
      gameObject.SetActive(false);
      return UniTask.CompletedTask;
    }
  }
  
  public class InstantShow : ShowAction
  {
    public override UniTask Show(bool isForce = false)
    {
      gameObject.SetActive(true);
      return UniTask.CompletedTask;
    }
  }
}