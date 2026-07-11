using System;
using Cysharp.Threading.Tasks;
using YooAsset;

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
        if (Package != null)
            return;
        await UniTask.Yield();
        if (!YooAssets.Initialized)
            YooAssets.Initialize();

        ResourcePackage package = YooAssets.TryGetPackage(_options.PackageName);
        if (package == null)
            package = YooAssets.CreatePackage(_options.PackageName);

        var initializeParameters = new OfflinePlayModeParameters
        {
            BuildinFileSystemParameters =
                FileSystemParameters.CreateDefaultBuildinFileSystemParameters()
        };
        var initializeOperation = package.InitializeAsync(initializeParameters);
        await initializeOperation.Task;

        if (initializeOperation.Status != EOperationStatus.Succeed)
            throw new InvalidOperationException($"Unable to initialize YooAsset package '{_options.PackageName}': {initializeOperation.Error}");

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

        var downloader = Package.CreateResourceDownloader(downloadingMaxNumber: 4, failedTryAgain: 2);
        if (downloader.TotalDownloadCount == 0)
            return;

        downloader.BeginDownload();
        await downloader.Task;

        if (downloader.Status != EOperationStatus.Succeed)
            throw new InvalidOperationException($"Unable to download game content: {downloader.Error}");
    }
}
