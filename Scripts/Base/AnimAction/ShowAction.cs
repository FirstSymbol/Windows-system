using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WindowsSystem.Scripts.Base.AnimAction
{
  [Serializable]
  public abstract class ShowAction
  {
    public GameObject gameObject;
    public abstract UniTask Show(bool isForce = false);
  }
}