using System;
using Cysharp.Threading.Tasks;
using YooAsset;

/// <summary>
/// YooAsset Package 生命周期和下载服务。
/// 当前使用 OfflinePlayMode，资源从 StreamingAssets/yoo/Main 读取。
/// </summary>
public sealed class YooAssetPackageService
{
    private readonly GameStartupOptions _options;

    public ResourcePackage Package { get; private set; }

    public YooAssetPackageService(GameStartupOptions options)
    {
        _options = options;
    }

    public async UniTask InitializeAsync()
    {
        // 初始化具有幂等性，避免 GameLauncher 和 SceneFlowService 重复初始化。
        if (Package != null)
            return;

        // 让 YooAsset 的初始化操作至少在下一帧开始，避免阻塞当前启动回调。
        await UniTask.Yield();
        if (!YooAssets.Initialized)
            YooAssets.Initialize();

        ResourcePackage package = YooAssets.TryGetPackage(_options.PackageName);
        if (package == null)
            package = YooAssets.CreatePackage(_options.PackageName);

        // OfflinePlayMode 只使用内置文件系统，不会向 HTTP 服务器请求资源。
        var initializeParameters = new OfflinePlayModeParameters
        {
            BuildinFileSystemParameters =
                FileSystemParameters.CreateDefaultBuildinFileSystemParameters()
        };
        var initializeOperation = package.InitializeAsync(initializeParameters);
        await initializeOperation.Task;

        if (initializeOperation.Status != EOperationStatus.Succeed)
            throw new InvalidOperationException($"Unable to initialize YooAsset package '{_options.PackageName}': {initializeOperation.Error}");

        // 读取当前 Package 的版本文件，再加载对应的本地 Manifest。
        var requestVersionOperation = package.RequestPackageVersionAsync();
        await requestVersionOperation.Task;

        if (requestVersionOperation.Status != EOperationStatus.Succeed)
            throw new InvalidOperationException($"Unable to request YooAsset package version '{_options.PackageName}': {requestVersionOperation.Error}");

        var updateManifestOperation = package.UpdatePackageManifestAsync(requestVersionOperation.PackageVersion);
        await updateManifestOperation.Task;

        if (updateManifestOperation.Status != EOperationStatus.Succeed)
            throw new InvalidOperationException($"Unable to load YooAsset package manifest '{_options.PackageName}': {updateManifestOperation.Error}");

        YooAssets.SetDefaultPackage(package);
        Package = package;
    }

    public async UniTask EnsureAllContentAvailableAsync()
    {
        if (Package == null)
            throw new InvalidOperationException("YooAsset package has not been initialized.");

        // 当前方法创建的是整个 Package 的下载器。
        // OfflinePlayMode 下通常没有需要下载的内容，因此 TotalDownloadCount 为 0。
        var downloader = Package.CreateResourceDownloader(downloadingMaxNumber: 4, failedTryAgain: 2);
        if (downloader.TotalDownloadCount == 0)
            return;

        // BeginDownload 只负责把缺失 Bundle 下载到缓存，不会直接加载场景对象。
        downloader.BeginDownload();
        await downloader.Task;

        if (downloader.Status != EOperationStatus.Succeed)
            throw new InvalidOperationException($"Unable to download game content: {downloader.Error}");
    }
}
