using System;
using UnityEngine;

namespace Change.Runtime.Asset
{
    /// <summary>
    /// GameObject 实例句柄，管理实例和资源的生命周期
    /// </summary>
    public sealed class GameObjectLease : IDisposable
    {
        private readonly GameObject _instance;
        private Action _release;

        /// <summary>
        /// 构造 GameObject 句柄
        /// </summary>
        /// <param name="instance">实例化的 GameObject</param>
        /// <param name="release">释放回调（销毁实例 + 释放资源）</param>
        public GameObjectLease(GameObject instance, Action release)
        {
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));
            _release = release ?? throw new ArgumentNullException(nameof(release));
        }

        /// <summary>
        /// 获取实例化的 GameObject
        /// </summary>
        public GameObject Instance => _instance;

        /// <summary>
        /// 释放句柄，销毁 GameObject 实例并释放资源
        /// </summary>
        public void Dispose()
        {
            var release = _release;
            if (release == null)
            {
                return;
            }

            _release = null;
            try
            {
                release();
            }
            catch
            {
                // 吞掉释放异常，防止 Dispose 抛异常
            }
        }
    }
}
