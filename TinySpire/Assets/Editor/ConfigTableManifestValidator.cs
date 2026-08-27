using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEngine;

/// <summary>
/// 配置表清单在生成、资源输出与运行时加载之间发生漂移时抛出的构建期异常。
/// </summary>
internal sealed class ConfigTableManifestDriftException : Exception
{
    /// <summary>创建包含各来源差异的稳定构建期错误。</summary>
    public ConfigTableManifestDriftException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// 比较 Luban 表定义、生成代码、生成 JSON 与运行时必需表清单，阻止配置漂移进入构建产物。
/// </summary>
internal static class ConfigTableManifestValidator
{
    private static readonly Regex GeneratedLoaderPattern = new(
        @"loader\(\""(?<table>[^\"" ]+)\""\)",
        RegexOptions.CultureInvariant);

    private static readonly Regex GeneratedJsonTableNamePattern = new(
        @"^[a-z][a-z0-9_]*_tb[a-z0-9_]+$",
        RegexOptions.CultureInvariant);

    /// <summary>使用当前 Unity 项目中的真实生成物与表定义执行完整清单校验。</summary>
    internal static void ValidateCurrentProject()
    {
        string unityProjectDirectory = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unable to determine Unity project directory.");
        string workspaceDirectory = Directory.GetParent(unityProjectDirectory)?.FullName
            ?? throw new InvalidOperationException("Unable to determine TinySpire workspace directory.");
        string generatedTablesPath = Path.Combine(
            unityProjectDirectory,
            "Assets",
            "Scripts",
            "Core",
            "Generated",
            "Config",
            "Tables.cs");
        string gameDataDirectory = Path.Combine(unityProjectDirectory, "Assets", "GameData");
        string tableDefinitionsPath = Path.Combine(
            workspaceDirectory,
            "DataTables",
            "Datas",
            "__tables__.xlsx");

        ValidateSets(
            ConfigService.RequiredTableNames,
            ReadGeneratedTableNames(generatedTablesPath),
            ReadGeneratedJsonTableNames(gameDataDirectory),
            ReadLubanTableNames(tableDefinitionsPath));
    }

    /// <summary>比较四份表名集合，并在任一来源遗漏或多出表时阻止构建。</summary>
    internal static void ValidateSets(
        IEnumerable<string> runtimeTableNames,
        IEnumerable<string> generatedTableNames,
        IEnumerable<string> generatedJsonTableNames,
        IEnumerable<string> lubanDefinitionTableNames)
    {
        var definitions = NormalizeTableNames(lubanDefinitionTableNames, "Luban table definitions");
        var differences = new List<string>();
        CollectDifferences(
            differences,
            "runtime TableNames",
            NormalizeTableNames(runtimeTableNames, "runtime TableNames"),
            definitions);
        CollectDifferences(
            differences,
            "generated Tables.cs",
            NormalizeTableNames(generatedTableNames, "generated Tables.cs"),
            definitions);
        CollectDifferences(
            differences,
            "generated JSON",
            NormalizeTableNames(generatedJsonTableNames, "generated JSON"),
            definitions);

        if (differences.Count > 0)
        {
            throw new ConfigTableManifestDriftException(
                $"Configuration table manifest drift detected. {string.Join(" ", differences)}");
        }
    }

    /// <summary>从生成 Tables 构造函数中读取实际调用 loader 的表名。</summary>
    private static IEnumerable<string> ReadGeneratedTableNames(string generatedTablesPath)
    {
        if (!File.Exists(generatedTablesPath))
            throw new FileNotFoundException("Generated Tables.cs was not found.", generatedTablesPath);

        return GeneratedLoaderPattern.Matches(File.ReadAllText(generatedTablesPath))
            .Select(match => match.Groups["table"].Value)
            .ToArray();
    }

    /// <summary>读取 GameData 中由 Luban 输出的全部客户端表 JSON 文件名。</summary>
    private static IEnumerable<string> ReadGeneratedJsonTableNames(string gameDataDirectory)
    {
        if (!Directory.Exists(gameDataDirectory))
            throw new DirectoryNotFoundException($"Generated GameData directory was not found: {gameDataDirectory}");

        return Directory.EnumerateFiles(gameDataDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => GeneratedJsonTableNamePattern.IsMatch(name))
            .ToArray();
    }

    /// <summary>从 Luban 的 __tables__.xlsx 读取参与客户端生成的全部表定义。</summary>
    private static IEnumerable<string> ReadLubanTableNames(string tableDefinitionsPath)
    {
        if (!File.Exists(tableDefinitionsPath))
            throw new FileNotFoundException("Luban table definition workbook was not found.", tableDefinitionsPath);

        using FileStream fileStream = File.OpenRead(tableDefinitionsPath);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);
        IReadOnlyList<string> sharedStrings = ReadSharedStrings(archive);
        ZipArchiveEntry sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")
            ?? throw new InvalidOperationException("Luban table definition workbook does not contain sheet1.xml.");
        using Stream sheetStream = sheetEntry.Open();
        XDocument document = XDocument.Load(sheetStream);
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        var tableNames = new List<string>();
        foreach (XElement row in document.Descendants(spreadsheet + "row"))
        {
            IReadOnlyDictionary<string, string> cells = ReadCells(row, spreadsheet, sharedStrings);
            if (!cells.TryGetValue("B", out string fullName) ||
                fullName.IndexOf('.') <= 0 ||
                !IsClientTable(cells))
            {
                continue;
            }

            tableNames.Add(fullName.Replace('.', '_').ToLowerInvariant());
        }

        return tableNames;
    }

    /// <summary>读取工作簿共享字符串表，供表格单元格索引解析使用。</summary>
    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry == null)
            return Array.Empty<string>();

        using Stream stream = entry.Open();
        XDocument document = XDocument.Load(stream);
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return document
            .Descendants(spreadsheet + "si")
            .Select(item => string.Concat(item.Descendants(spreadsheet + "t").Select(text => text.Value)))
            .ToArray();
    }

    /// <summary>将一行 XML 单元格解析为以列字母为键的文本字典。</summary>
    private static IReadOnlyDictionary<string, string> ReadCells(
        XElement row,
        XNamespace spreadsheet,
        IReadOnlyList<string> sharedStrings)
    {
        var cells = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (XElement cell in row.Elements(spreadsheet + "c"))
        {
            string cellReference = (string)cell.Attribute("r");
            string columnName = GetColumnName(cellReference);
            if (string.IsNullOrEmpty(columnName))
                continue;

            cells[columnName] = ReadCellValue(cell, spreadsheet, sharedStrings);
        }

        return cells;
    }

    /// <summary>解析共享字符串、普通字符串或布尔单元格中的文本值。</summary>
    private static string ReadCellValue(
        XElement cell,
        XNamespace spreadsheet,
        IReadOnlyList<string> sharedStrings)
    {
        string value = cell.Element(spreadsheet + "v")?.Value ?? string.Empty;
        if ((string)cell.Attribute("t") != "s")
            return value;

        if (!int.TryParse(value, out int index) || index < 0 || index >= sharedStrings.Count)
            throw new InvalidOperationException($"Invalid shared string index '{value}' in Luban table definition workbook.");

        return sharedStrings[index];
    }

    /// <summary>从 Excel 单元格引用中提取列字母，例如 B4 的 B。</summary>
    private static string GetColumnName(string cellReference)
    {
        if (string.IsNullOrEmpty(cellReference))
            return string.Empty;

        int index = 0;
        while (index < cellReference.Length && char.IsLetter(cellReference[index]))
            index++;

        return cellReference.Substring(0, index);
    }

    /// <summary>判断 H 列表定义是否包含客户端分组；空分组按 Luban 约定属于全部分组。</summary>
    private static bool IsClientTable(IReadOnlyDictionary<string, string> cells)
    {
        if (!cells.TryGetValue("H", out string groups) || string.IsNullOrWhiteSpace(groups))
            return true;

        return groups.Split(',').Any(group => string.Equals(group.Trim(), "c", StringComparison.Ordinal));
    }

    /// <summary>标准化表名，同时拒绝空项、重复项和空来源以保持失败可诊断。</summary>
    private static HashSet<string> NormalizeTableNames(IEnumerable<string> tableNames, string sourceName)
    {
        if (tableNames == null)
            throw new ArgumentNullException(nameof(tableNames));

        string[] values = tableNames.Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();
        string[] duplicates = values
            .GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new ConfigTableManifestDriftException(
                $"{sourceName} has duplicate [{string.Join(", ", duplicates)}].");
        }

        var normalized = new HashSet<string>(values, StringComparer.Ordinal);
        if (normalized.Count == 0)
            throw new ConfigTableManifestDriftException($"{sourceName} contains no client table names.");

        return normalized;
    }

    /// <summary>记录单一来源相对 Luban 表定义的遗漏与额外项。</summary>
    private static void CollectDifferences(
        ICollection<string> differences,
        string sourceName,
        IReadOnlyCollection<string> actual,
        IReadOnlyCollection<string> expected)
    {
        string[] missing = expected.Except(actual).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        string[] unexpected = actual.Except(expected).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (missing.Length > 0)
            differences.Add($"{sourceName} missing [{string.Join(", ", missing)}].");
        if (unexpected.Length > 0)
            differences.Add($"{sourceName} has unexpected [{string.Join(", ", unexpected)}].");
    }
}
