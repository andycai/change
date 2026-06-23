using System;
using UnityEngine;

namespace Change.Runtime.Asset
{
    /// <summary>
    /// 泛型资源句柄，自动管理资源引用计数
    /// </summary>
    public sealed class AssetLease<T> : IDisposable where T : UnityEngine.Object
    {
        private readonly T _asset;
        private Action _release;

        /// <summary>
        /// 构造资源句柄
        /// </summary>
        /// <param name="asset">加载的资源</param>
        /// <param name="release">释放回调</param>
        public AssetLease(T asset, Action release)
        {
            _asset = asset ?? throw new ArgumentNullException(nameof(asset));
            _release = release ?? throw new ArgumentNullException(nameof(release));
        }

        /// <summary>
        /// 获取加载的资源
        /// </summary>
        public T Asset => _asset;

        /// <summary>
        /// 释放资源句柄
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
