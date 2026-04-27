using System;
using UnityEngine;

namespace Change.Runtime.UI
{
    public sealed class UiAssetLease : IDisposable
    {
        private Action _release;

        public UiAssetLease(GameObject instance, Action release)
        {
            Instance = instance;
            _release = release;
        }

        public GameObject Instance { get; }

        public void Dispose()
        {
            var release = _release;
            if (release == null)
            {
                return;
            }

            _release = null;
            release();
        }
    }
}
