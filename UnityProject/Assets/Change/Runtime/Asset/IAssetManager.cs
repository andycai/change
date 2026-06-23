using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Change.Runtime.Asset
{
    /// <summary>
    /// 资源管理器接口，提供统一的资源加载抽象层
    /// </summary>
    public interface IAssetManager
    {
        /// <summary>
        /// 从默认包异步加载资源
        /// </summary>
        UniTask<AssetLease<T>> LoadAsync<T>(
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;

        /// <summary>
        /// 从指定包异步加载资源
        /// </summary>
        UniTask<AssetLease<T>> LoadAsync<T>(
            string packageName,
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;

        /// <summary>
        /// 从默认包加载并实例化 GameObject
        /// </summary>
        UniTask<GameObjectLease> LoadAndInstantiateAsync(
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 从指定包加载并实例化 GameObject
        /// </summary>
        UniTask<GameObjectLease> LoadAndInstantiateAsync(
            string packageName,
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);
    }
}
