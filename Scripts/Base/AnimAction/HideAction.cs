using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WindowsSystem.Scripts.Base.AnimAction
{
  [Serializable]
  public abstract class HideAction
  {
    public GameObject gameObject;
    public abstract UniTask Hide(bool isForce = false);
  }
}