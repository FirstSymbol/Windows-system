using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WindowsSystem.Scripts.Base.AnimAction
{
  public abstract class ShowAction
  {
    public GameObject gameObject;
    public abstract UniTask Show(bool isForce = false);
  }
}