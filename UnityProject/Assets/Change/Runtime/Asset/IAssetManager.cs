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
        /// <typeparam name="T">要加载的资源类型，必须是 UnityEngine.Object 的子类</typeparam>
        /// <param name="location">资源的 YooAsset 地址</param>
        /// <param name="progress">可选的进度报告器，范围 0.0 到 1.0</param>
        /// <param name="cancellationToken">用于取消加载操作的取消令牌</param>
        /// <returns>资源的租约句柄，调用方必须在使用完毕后调用 Dispose 释放资源</returns>
        UniTask<AssetLease<T>> LoadAsync<T>(
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;

        /// <summary>
        /// 从指定包异步加载资源
        /// </summary>
        /// <typeparam name="T">要加载的资源类型，必须是 UnityEngine.Object 的子类</typeparam>
        /// <param name="packageName">YooAsset 资源包的名称</param>
        /// <param name="location">资源的 YooAsset 地址</param>
        /// <param name="progress">可选的进度报告器，范围 0.0 到 1.0</param>
        /// <param name="cancellationToken">用于取消加载操作的取消令牌</param>
        /// <returns>资源的租约句柄，调用方必须在使用完毕后调用 Dispose 释放资源</returns>
        UniTask<AssetLease<T>> LoadAsync<T>(
            string packageName,
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;

        /// <summary>
        /// 从默认包加载并实例化 GameObject
        /// </summary>
        /// <param name="location">GameObject 预制体的 YooAsset 地址</param>
        /// <param name="progress">可选的进度报告器，范围 0.0 到 1.0</param>
        /// <param name="cancellationToken">用于取消加载操作的取消令牌</param>
        /// <returns>已实例化的 GameObject 租约句柄，调用方必须在使用完毕后调用 Dispose 销毁实例并释放资源</returns>
        UniTask<GameObjectLease> LoadAndInstantiateAsync(
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 从指定包加载并实例化 GameObject
        /// </summary>
        /// <param name="packageName">YooAsset 资源包的名称</param>
        /// <param name="location">GameObject 预制体的 YooAsset 地址</param>
        /// <param name="progress">可选的进度报告器，范围 0.0 到 1.0</param>
        /// <param name="cancellationToken">用于取消加载操作的取消令牌</param>
        /// <returns>已实例化的 GameObject 租约句柄，调用方必须在使用完毕后调用 Dispose 销毁实例并释放资源</returns>
        UniTask<GameObjectLease> LoadAndInstantiateAsync(
            string packageName,
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);
    }
}
