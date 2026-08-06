using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// TinySpire 配置与本地资源内容的一键同步、校验和构建入口。
/// </summary>
public static class TinySpireBuildTools
{
    private const string MenuPath = "TinySpire/Build/Sync and Build All";

    /// <summary>
    /// 生成 Luban 配置，导入并校验本地化 Excel，随后重建本地 Addressables 内容。
    /// </summary>
    [MenuItem(MenuPath)]
    public static void SyncAndBuildAll()
    {
        try
        {
            EditorUtility.DisplayProgressBar("TinySpire Build", "Generating Luban configuration...", 0.1f);
            GenerateLubanConfiguration();

            EditorUtility.DisplayProgressBar("TinySpire Build", "Refreshing generated assets...", 0.35f);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            EditorUtility.DisplayProgressBar("TinySpire Build", "Validating configuration table manifest...", 0.45f);
            ConfigTableManifestValidator.ValidateCurrentProject();

            EditorUtility.DisplayProgressBar("TinySpire Build", "Validating battle card catalog...", 0.5f);
            BattleCardCatalogBuildValidator.ValidateCurrentProject();

            EditorUtility.DisplayProgressBar("TinySpire Build", "Importing and validating localization...", 0.6f);
            LocalizationBuildTools.ImportBattleCardTextFromExcel();

            EditorUtility.DisplayProgressBar("TinySpire Build", "Building local Addressables content...", 0.75f);
            AddressablesBuildTools.BuildLocalContent();

            Debug.Log("TinySpire sync and local content build completed successfully.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>
    /// 以与 DataTables/gen.bat 相同的参数生成配置代码和 GameData JSON。
    /// </summary>
    private static void GenerateLubanConfiguration()
    {
        string unityProjectDirectory = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unable to determine Unity project directory.");
        string workspaceDirectory = Directory.GetParent(unityProjectDirectory)?.FullName
            ?? throw new InvalidOperationException("Unable to determine TinySpire workspace directory.");
        string dataTablesDirectory = Path.Combine(workspaceDirectory, "DataTables");
        string lubanDllPath = Path.Combine(workspaceDirectory, "Tools", "Luban", "Luban.dll");
        string generatedCodeDirectory = Path.Combine(
            unityProjectDirectory,
            "Assets",
            "Scripts",
            "Core",
            "Generated",
            "Config");
        string gameDataDirectory = Path.Combine(unityProjectDirectory, "Assets", "GameData");
        string gameConfigSourcePath = Path.Combine(dataTablesDirectory, "game-config.json");
        string gameConfigOutputPath = Path.Combine(gameDataDirectory, "game-config.json");

        if (!File.Exists(lubanDllPath))
            throw new FileNotFoundException("Luban executable was not found.", lubanDllPath);
        if (!File.Exists(gameConfigSourcePath))
            throw new FileNotFoundException("Hand-authored game config was not found.", gameConfigSourcePath);

        string arguments = string.Join(
            " ",
            QuoteArgument(lubanDllPath),
            "-t all",
            "-d json2",
            "-c cs-newtonsoft-json",
            "--conf .\\luban.conf",
            $"-x outputCodeDir={QuoteArgument(generatedCodeDirectory)}",
            $"-x outputDataDir={QuoteArgument(gameDataDirectory)}");
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = dataTablesDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start Luban generation process.");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Luban generation failed with exit code {process.ExitCode}.\n{standardOutput}\n{standardError}");
        }

        Directory.CreateDirectory(gameDataDirectory);
        File.Copy(gameConfigSourcePath, gameConfigOutputPath, overwrite: true);
    }

    /// <summary>
    /// 将路径包装成可由 Windows 进程参数安全解析的单个参数。
    /// </summary>
    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
