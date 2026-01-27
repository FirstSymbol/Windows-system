using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WindowsSystem.Scripts.Base.AnimAction
{
  public abstract class HideAction
  {
    public GameObject gameObject;
    public abstract UniTask Hide(bool isForce = false);
  }
}