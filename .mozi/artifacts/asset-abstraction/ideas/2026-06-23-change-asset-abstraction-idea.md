# 封装资源管理接口

在 @UnityProject/Assets/Change/Runtime/ 下的 Asset 实现一套资源管理的接口。

当前工程使用的使用开源资源管理库 YooAsset，但是我们后续开发的项目都是大型手机游戏，需要 10 多人前端开发，所以我不希望其他前端工程师知道我们使用的是 YooAsset，不希望他们直接使用 YooAsset 的接口，而且直接使用我们封装好的一套资源管理接口，后续我们要更换掉 YooAsset 成本也会很低
