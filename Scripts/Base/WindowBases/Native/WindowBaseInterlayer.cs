using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WindowsSystem
{
    public abstract class WindowBaseInterlayer : MonoBehaviour
    {
        public bool IsInitialized => _isInitialized;
        protected bool _isInitialized; 
        public virtual void Init(){}
    }
}