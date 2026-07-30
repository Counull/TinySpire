using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

/// <summary>
/// 读取内容团队维护的 i18n.xlsx，不依赖运行时或第三方 Excel 库。
/// 只支持本项目约定的简单文本单元格，不承担通用 Excel 解析职责。
/// </summary>
internal static class I18nExcelReader
{
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static readonly XNamespace OfficeDocumentRelationshipNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static readonly XNamespace PackageRelationshipNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>
    /// 从指定工作簿的 i18n 工作表读取翻译行，并验证表头、语言列和重复 key。
    /// </summary>
    public static IReadOnlyList<I18nExcelEntry> Read(
        string workbookPath,
        string sheetName,
        IReadOnlyList<string> localeCodes)
    {
        if (string.IsNullOrWhiteSpace(workbookPath))
            throw new ArgumentException("Workbook path cannot be empty.", nameof(workbookPath));
        if (!File.Exists(workbookPath))
            throw new InvalidOperationException($"i18n workbook does not exist: {workbookPath}");
        if (string.IsNullOrWhiteSpace(sheetName))
            throw new ArgumentException("Sheet name cannot be empty.", nameof(sheetName));
        if (localeCodes == null || localeCodes.Count == 0)
            throw new ArgumentException("At least one locale is required.", nameof(localeCodes));

        using var archive = ZipFile.OpenRead(workbookPath);
        IReadOnlyList<string> sharedStrings = ReadSharedStrings(archive);
        XDocument worksheet = ReadXml(archive, FindWorksheetPath(archive, sheetName));
        List<XElement> rows = worksheet
            .Descendants(SpreadsheetNamespace + "row")
            .OrderBy(row => ParseRowIndex(row.Attribute("r")?.Value))
            .ToList();
        if (rows.Count == 0)
            throw new InvalidOperationException($"i18n workbook sheet '{sheetName}' does not contain any rows.");

        Dictionary<int, string> headers = ReadRow(rows[0], sharedStrings);
        int keyColumn = RequireColumn(headers, "key");
        int smartColumn = RequireColumn(headers, "smart");
        var localeColumns = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string localeCode in localeCodes)
            localeColumns.Add(localeCode, RequireColumn(headers, localeCode));

        var entries = new List<I18nExcelEntry>();
        var entryKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            Dictionary<int, string> values = ReadRow(rows[rowIndex], sharedStrings);
            if (values.Values.All(string.IsNullOrWhiteSpace))
                continue;

            int excelRowNumber = ParseRowIndex(rows[rowIndex].Attribute("r")?.Value);
            string key = RequireCell(values, keyColumn, "key", excelRowNumber).Trim();
            if (!entryKeys.Add(key))
                throw new InvalidOperationException($"i18n workbook contains duplicate key '{key}' at row {excelRowNumber}.");

            string smartText = RequireCell(values, smartColumn, "smart", excelRowNumber);
            if (!bool.TryParse(smartText, out bool isSmart))
            {
                throw new InvalidOperationException(
                    $"i18n workbook row {excelRowNumber} column 'smart' must be true or false.");
            }

            var translations = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> localeColumn in localeColumns)
            {
                translations.Add(
                    localeColumn.Key,
                    RequireCell(values, localeColumn.Value, localeColumn.Key, excelRowNumber));
            }

            entries.Add(new I18nExcelEntry(key, translations, isSmart));
        }

        if (entries.Count == 0)
            throw new InvalidOperationException($"i18n workbook sheet '{sheetName}' does not contain any translation entries.");

        return entries;
    }

    /// <summary>
    /// 从 sharedStrings.xml 读取共享字符串；使用 inlineStr 的工作簿会得到空列表。
    /// </summary>
    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry == null)
            return Array.Empty<string>();

        XDocument document = ReadXml(entry);
        return document
            .Descendants(SpreadsheetNamespace + "si")
            .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)))
            .ToArray();
    }

    /// <summary>
    /// 通过 workbook 与关系文件定位具名工作表的实际 XML 路径。
    /// </summary>
    private static string FindWorksheetPath(ZipArchive archive, string sheetName)
    {
        XDocument workbook = ReadXml(archive, "xl/workbook.xml");
        XElement sheet = workbook
            .Descendants(SpreadsheetNamespace + "sheet")
            .SingleOrDefault(candidate => string.Equals(
                candidate.Attribute("name")?.Value,
                sheetName,
                StringComparison.Ordinal));
        if (sheet == null)
            throw new InvalidOperationException($"i18n workbook does not contain sheet '{sheetName}'.");

        string relationshipId = sheet.Attribute(OfficeDocumentRelationshipNamespace + "id")?.Value;
        if (string.IsNullOrWhiteSpace(relationshipId))
            throw new InvalidOperationException($"i18n workbook sheet '{sheetName}' has no relationship id.");

        XDocument relationships = ReadXml(archive, "xl/_rels/workbook.xml.rels");
        XElement relationship = relationships
            .Descendants(PackageRelationshipNamespace + "Relationship")
            .SingleOrDefault(candidate => candidate.Attribute("Id")?.Value == relationshipId);
        string target = relationship?.Attribute("Target")?.Value;
        if (string.IsNullOrWhiteSpace(target))
            throw new InvalidOperationException($"i18n workbook sheet '{sheetName}' has no worksheet target.");

        return target.StartsWith("/", StringComparison.Ordinal)
            ? target.TrimStart('/')
            : "xl/" + target;
    }

    /// <summary>
    /// 读取一行的所有文本单元格，并以零基列索引作为键。
    /// </summary>
    private static Dictionary<int, string> ReadRow(XElement row, IReadOnlyList<string> sharedStrings)
    {
        var values = new Dictionary<int, string>();
        foreach (XElement cell in row.Elements(SpreadsheetNamespace + "c"))
        {
            int column = ParseColumnIndex(cell.Attribute("r")?.Value);
            values[column] = ReadCellValue(cell, sharedStrings);
        }

        return values;
    }

    /// <summary>
    /// 读取共享字符串、内联字符串或普通文本值。
    /// </summary>
    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        string cellType = cell.Attribute("t")?.Value;
        if (cellType == "s")
        {
            string sharedStringIndex = cell.Element(SpreadsheetNamespace + "v")?.Value;
            if (!int.TryParse(sharedStringIndex, out int index) || index < 0 || index >= sharedStrings.Count)
                throw new InvalidOperationException($"Invalid shared string index '{sharedStringIndex}'.");
            return sharedStrings[index];
        }

        if (cellType == "inlineStr")
            return string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value));

        return cell.Element(SpreadsheetNamespace + "v")?.Value ?? string.Empty;
    }

    /// <summary>
    /// 读取 XML 压缩包条目。
    /// </summary>
    private static XDocument ReadXml(ZipArchive archive, string entryPath)
    {
        ZipArchiveEntry entry = archive.GetEntry(entryPath)
            ?? throw new InvalidOperationException($"i18n workbook is missing '{entryPath}'.");
        return ReadXml(entry);
    }

    /// <summary>
    /// 从一个 ZIP 条目加载 XML 文档。
    /// </summary>
    private static XDocument ReadXml(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        return XDocument.Load(stream);
    }

    /// <summary>
    /// 从表头中查找要求列，不区分首尾空白。
    /// </summary>
    private static int RequireColumn(IReadOnlyDictionary<int, string> headers, string headerName)
    {
        foreach (KeyValuePair<int, string> header in headers)
        {
            if (string.Equals(header.Value?.Trim(), headerName, StringComparison.Ordinal))
                return header.Key;
        }

        throw new InvalidOperationException($"i18n workbook is missing required column '{headerName}'.");
    }

    /// <summary>
    /// 读取一个必须非空的单元格。
    /// </summary>
    private static string RequireCell(
        IReadOnlyDictionary<int, string> values,
        int column,
        string headerName,
        int rowNumber)
    {
        if (!values.TryGetValue(column, out string value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"i18n workbook row {rowNumber} column '{headerName}' cannot be empty.");
        }

        return value;
    }

    /// <summary>
    /// 将 A1 引用中的字母部分转换为零基列索引。
    /// </summary>
    private static int ParseColumnIndex(string cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
            throw new InvalidOperationException("i18n workbook contains a cell without a reference.");

        int result = 0;
        int characterIndex = 0;
        while (characterIndex < cellReference.Length && char.IsLetter(cellReference[characterIndex]))
        {
            result = result * 26 + char.ToUpperInvariant(cellReference[characterIndex]) - 'A' + 1;
            characterIndex++;
        }

        if (characterIndex == 0)
            throw new InvalidOperationException($"Invalid cell reference '{cellReference}'.");

        return result - 1;
    }

    /// <summary>
    /// 读取行引用；缺少引用时返回零以保持异常信息稳定。
    /// </summary>
    private static int ParseRowIndex(string rowReference)
    {
        return int.TryParse(rowReference, out int rowIndex) ? rowIndex : 0;
    }
}

/// <summary>
/// Excel 中一行 i18n 文本及其 Smart String 标记。
/// </summary>
internal sealed class I18nExcelEntry
{
    /// <summary>稳定的 Unity Localization key。</summary>
    public string Key { get; }

    /// <summary>按语言代码保存的原始翻译正文。</summary>
    public IReadOnlyDictionary<string, string> Translations { get; }

    /// <summary>该条目是否应作为 Unity Smart String 写入。</summary>
    public bool IsSmart { get; }

    /// <summary>构造一条已验证的 Excel 翻译记录。</summary>
    public I18nExcelEntry(
        string key,
        IReadOnlyDictionary<string, string> translations,
        bool isSmart)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Translations = translations ?? throw new ArgumentNullException(nameof(translations));
        IsSmart = isSmart;
    }
}
