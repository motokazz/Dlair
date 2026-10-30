using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CSV から StoryVariableDatabase を作る。
/// 既存アセットを渡したときは、その変数一覧を CSV の内容で置き換える。
/// </summary>
public static class StoryVariableDatabaseUtility
{
    public const string Summary =
        "CSV から StoryVariableDatabase を作ります。\n" +
        "1行目は見出しです。列の順番は自由で、key / 変数名、description / 説明、type / 型、default / 初期値 を読みます。\n" +
        "型は Int、Bool、Float、String です。初期値は型に合わせて読みます。同じ変数名が複数あるときは、後の行を採用します。\n" +
        "文字コードは UTF-8 です。\n" +
        "上書きするデータベースを指定したときは、その変数一覧を CSV の内容で置き換えます。指定が無いときは、選んだフォルダに新しいアセットを作ります。";

    /// <summary>
    /// CSV から読み取った変数一覧と、警告、エラー。
    /// </summary>
    public class ParseResult
    {
        public readonly List<StoryVariableDefinition> variables = new List<StoryVariableDefinition>();
        public readonly List<string> warnings = new List<string>();
        public string error;
    }

    private struct Columns
    {
        public int key;
        public int description;
        public int type;
        public int defaultValue;
        public int defaultInt;
        public int defaultBool;
        public int defaultFloat;
        public int defaultString;
    }

    /// <summary>
    /// CSV ファイルを読んで変数定義にする。path は Assets からの相対パスか、絶対パス。
    /// </summary>
    public static ParseResult ParseFile(string path)
    {
        ParseResult result = new ParseResult();
        string absolute = ToAbsolutePath(path);
        if (string.IsNullOrEmpty(absolute) || !File.Exists(absolute))
        {
            result.error = "CSV ファイルが見つかりません。";
            return result;
        }

        try
        {
            string text = Decode(File.ReadAllBytes(absolute));
            return Parse(text);
        }
        catch (Exception ex)
        {
            result.error = string.IsNullOrEmpty(ex.Message) ? "CSV を読めませんでした。" : ex.Message;
            return result;
        }
    }

    /// <summary>
    /// CSV 本文を変数定義にする。1行目は見出し。
    /// </summary>
    public static ParseResult Parse(string csvText)
    {
        ParseResult result = new ParseResult();
        if (string.IsNullOrWhiteSpace(csvText))
        {
            result.error = "CSV が空です。";
            return result;
        }

        char delimiter = DetectDelimiter(csvText);
        List<int> startLines = new List<int>();
        List<List<string>> records = ReadRecords(csvText, delimiter, startLines);
        int headerIndex = 0;
        if (records.Count > 0 && IsSeparatorHint(records[0])) headerIndex = 1;
        if (records.Count <= headerIndex)
        {
            result.error = "CSV に見出しがありません。";
            return result;
        }

        Columns columns = FindColumns(records[headerIndex]);
        if (columns.key < 0)
        {
            result.error = "見出しに key（変数名）列がありません。";
            return result;
        }

        if (columns.type < 0)
        {
            result.warnings.Add("型の列が無いので、Int として読みます。");
        }

        Dictionary<string, int> indexByKey = new Dictionary<string, int>();
        bool hasDataRow = false;
        for (int i = headerIndex + 1; i < records.Count; i++)
        {
            List<string> row = records[i];
            if (IsBlank(row)) continue;
            hasDataRow = true;
            int line = i < startLines.Count ? startLines[i] : i + 1;
            StoryVariableDefinition definition = ReadRow(row, columns, line, result);
            if (definition == null) continue;

            if (indexByKey.TryGetValue(definition.key, out int existing))
            {
                result.warnings.Add($"「{definition.key}」が重複しています。{line} 行目を採用します。");
                result.variables[existing] = definition;
            }
            else
            {
                indexByKey.Add(definition.key, result.variables.Count);
                result.variables.Add(definition);
            }
        }

        if (!hasDataRow)
        {
            result.error = "変数の行がありません。";
        }
        else if (result.variables.Count == 0 && string.IsNullOrEmpty(result.error))
        {
            result.error = "読み込めた変数がありません。";
        }

        return result;
    }

    /// <summary>
    /// 指定フォルダに新しい StoryVariableDatabase を作る。
    /// </summary>
    public static StoryVariableDatabase CreateAsset(string folderPath, string assetName, IList<StoryVariableDefinition> variables)
    {
        if (variables == null || variables.Count == 0)
        {
            throw new ArgumentException("書き込む変数がありません。");
        }

        folderPath = (folderPath ?? "").Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrEmpty(folderPath) || !folderPath.StartsWith("Assets") || !AssetDatabase.IsValidFolder(folderPath))
        {
            throw new ArgumentException("作成先フォルダは Assets 以下である必要があります。");
        }

        assetName = SanitizeFileName(assetName);
        string path = AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + assetName + ".asset");
        StoryVariableDatabase database = ScriptableObject.CreateInstance<StoryVariableDatabase>();
        database.variables = CloneList(variables);
        AssetDatabase.CreateAsset(database, path);
        Undo.RegisterCreatedObjectUndo(database, "CSVから変数データベースを作成");
        AssetDatabase.SaveAssets();
        return database;
    }

    /// <summary>
    /// 既存の StoryVariableDatabase の変数一覧を、渡された定義で置き換える。
    /// </summary>
    public static void ReplaceVariables(StoryVariableDatabase database, IList<StoryVariableDefinition> variables)
    {
        if (database == null)
        {
            throw new ArgumentException("StoryVariableDatabase が指定されていません。");
        }

        if (variables == null || variables.Count == 0)
        {
            throw new ArgumentException("書き込む変数がありません。");
        }

        Undo.RecordObject(database, "CSVから変数データベースを更新");
        database.variables = CloneList(variables);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
    }

    public static string FormatDefault(StoryVariableDefinition definition)
    {
        if (definition == null) return "";
        switch (definition.type)
        {
            case StoryVariableType.Int:
                return definition.defaultIntValue.ToString(CultureInfo.InvariantCulture);
            case StoryVariableType.Bool:
                return definition.defaultBoolValue ? "true" : "false";
            case StoryVariableType.Float:
                return definition.defaultFloatValue.ToString(CultureInfo.InvariantCulture);
            default:
                return definition.defaultStringValue ?? "";
        }
    }

    private static StoryVariableDefinition ReadRow(List<string> row, Columns columns, int line, ParseResult result)
    {
        string key = Cell(row, columns.key).Trim();
        if (string.IsNullOrEmpty(key))
        {
            result.warnings.Add($"{line} 行目: 変数名が空なので飛ばします。");
            return null;
        }

        StoryVariableType type = StoryVariableType.Int;
        if (columns.type >= 0)
        {
            string typeText = Cell(row, columns.type).Trim();
            if (string.IsNullOrEmpty(typeText))
            {
                result.warnings.Add($"{line} 行目: 型が空なので Int にします。");
            }
            else if (!TryParseType(typeText, out type))
            {
                result.warnings.Add($"{line} 行目: 型「{typeText}」を読めないので飛ばします。");
                return null;
            }
        }

        StoryVariableDefinition definition = new StoryVariableDefinition();
        definition.key = key;
        definition.description = Cell(row, columns.description).Trim();
        definition.type = type;
        ApplyDefault(definition, DefaultText(row, columns, type), line, result);
        return definition;
    }

    private static void ApplyDefault(StoryVariableDefinition definition, string raw, int line, ParseResult result)
    {
        raw = raw == null ? "" : raw.Trim();
        if (string.IsNullOrEmpty(raw)) return;

        switch (definition.type)
        {
            case StoryVariableType.Int:
                if (!TryParseInt(raw, out definition.defaultIntValue))
                {
                    result.warnings.Add($"{line} 行目: 初期値「{raw}」を Int として読めないので 0 にします。");
                }
                break;
            case StoryVariableType.Bool:
                if (!TryParseBool(raw, out definition.defaultBoolValue))
                {
                    result.warnings.Add($"{line} 行目: 初期値「{raw}」を Bool として読めないので false にします。");
                }
                break;
            case StoryVariableType.Float:
                if (!TryParseFloat(raw, out definition.defaultFloatValue))
                {
                    result.warnings.Add($"{line} 行目: 初期値「{raw}」を Float として読めないので 0 にします。");
                }
                break;
            default:
                definition.defaultStringValue = raw;
                break;
        }
    }

    private static string DefaultText(List<string> row, Columns columns, StoryVariableType type)
    {
        int typedColumn = -1;
        switch (type)
        {
            case StoryVariableType.Int:
                typedColumn = columns.defaultInt;
                break;
            case StoryVariableType.Bool:
                typedColumn = columns.defaultBool;
                break;
            case StoryVariableType.Float:
                typedColumn = columns.defaultFloat;
                break;
            default:
                typedColumn = columns.defaultString;
                break;
        }

        string typed = Cell(row, typedColumn).Trim();
        if (!string.IsNullOrEmpty(typed)) return typed;
        return Cell(row, columns.defaultValue);
    }

    private static Columns FindColumns(List<string> header)
    {
        Columns columns = new Columns();
        columns.key = FindColumn(header, "key", "name", "キー", "変数名", "変数キー");
        columns.description = FindColumn(header, "description", "desc", "説明", "概要", "メモ");
        columns.type = FindColumn(header, "type", "型", "種類");
        columns.defaultValue = FindColumn(header, "default", "defaultvalue", "value", "初期値", "デフォルト", "値");
        columns.defaultInt = FindColumn(header, "defaultint", "defaultintvalue", "初期値int");
        columns.defaultBool = FindColumn(header, "defaultbool", "defaultboolvalue", "初期値bool");
        columns.defaultFloat = FindColumn(header, "defaultfloat", "defaultfloatvalue", "初期値float");
        columns.defaultString = FindColumn(header, "defaultstring", "defaultstringvalue", "初期値string");
        return columns;
    }

    private static int FindColumn(List<string> header, params string[] names)
    {
        for (int i = 0; i < header.Count; i++)
        {
            string cell = NormalizeHeader(header[i]);
            for (int n = 0; n < names.Length; n++)
            {
                if (string.Equals(cell, names[n], StringComparison.OrdinalIgnoreCase)) return i;
            }
        }

        return -1;
    }

    private static string NormalizeHeader(string header)
    {
        if (string.IsNullOrEmpty(header)) return "";
        string cell = header.Trim().TrimStart('\uFEFF');
        return cell.Replace(" ", "").Replace("　", "").Replace("_", "");
    }

    private static bool TryParseType(string text, out StoryVariableType type)
    {
        type = StoryVariableType.Int;
        switch (text.Trim().ToLowerInvariant())
        {
            case "int":
            case "integer":
            case "整数":
                type = StoryVariableType.Int;
                return true;
            case "bool":
            case "boolean":
            case "真偽":
            case "真偽値":
                type = StoryVariableType.Bool;
                return true;
            case "float":
            case "single":
            case "実数":
            case "小数":
            case "浮動小数":
                type = StoryVariableType.Float;
                return true;
            case "string":
            case "text":
            case "文字列":
                type = StoryVariableType.String;
                return true;
        }

        return false;
    }

    private static bool TryParseBool(string text, out bool value)
    {
        value = false;
        switch (text.Trim().ToLowerInvariant())
        {
            case "true":
            case "1":
            case "yes":
            case "y":
            case "on":
            case "真":
            case "○":
                value = true;
                return true;
            case "false":
            case "0":
            case "no":
            case "n":
            case "off":
            case "偽":
            case "×":
                value = false;
                return true;
        }

        return false;
    }

    private static bool TryParseInt(string text, out int value)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return true;
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value);
    }

    private static bool TryParseFloat(string text, out float value)
    {
        const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;
        if (float.TryParse(text, styles, CultureInfo.InvariantCulture, out value)) return true;
        return float.TryParse(text, styles, CultureInfo.CurrentCulture, out value);
    }

    private static char DetectDelimiter(string text)
    {
        string first = FirstPhysicalLine(text).TrimStart('\uFEFF').Trim();
        if (first.StartsWith("sep=", StringComparison.OrdinalIgnoreCase) && first.Length >= 5)
        {
            char hinted = first[4];
            if (hinted == ',' || hinted == ';' || hinted == '\t') return hinted;
        }

        int comma = CountChar(first, ',');
        int tab = CountChar(first, '\t');
        int semicolon = CountChar(first, ';');
        if (tab > comma && tab >= semicolon) return '\t';
        if (semicolon > comma && semicolon > tab) return ';';
        return ',';
    }

    private static string FirstPhysicalLine(string text)
    {
        int end = text.IndexOfAny(new[] { '\r', '\n' });
        if (end < 0) return text;
        return text.Substring(0, end);
    }

    private static int CountChar(string text, char value)
    {
        int count = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == value) count++;
        }

        return count;
    }

    private static bool IsSeparatorHint(List<string> record)
    {
        if (record == null || record.Count == 0) return false;
        string first = record[0].Trim();
        return first.Equals("sep", StringComparison.OrdinalIgnoreCase)
            || first.StartsWith("sep=", StringComparison.OrdinalIgnoreCase);
    }

    private static List<List<string>> ReadRecords(string text, char delimiter, List<int> startLines)
    {
        List<List<string>> records = new List<List<string>>();
        List<string> record = new List<string>();
        StringBuilder field = new StringBuilder();
        bool inQuotes = false;
        int line = 1;
        int recordStart = 1;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                    if (c == '\n') line++;
                    else if (c == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')) line++;
                }

                continue;
            }

            if (c == '"' && field.Length == 0)
            {
                inQuotes = true;
                continue;
            }

            if (c == delimiter)
            {
                record.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (c == '\n' || c == '\r')
            {
                record.Add(field.ToString());
                field.Clear();
                if (!IsBlank(record))
                {
                    records.Add(record);
                    startLines.Add(recordStart);
                }

                record = new List<string>();
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                line++;
                recordStart = line;
                continue;
            }

            field.Append(c);
        }

        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            if (!IsBlank(record))
            {
                records.Add(record);
                startLines.Add(recordStart);
            }
        }

        return records;
    }

    private static bool IsBlank(List<string> record)
    {
        if (record == null || record.Count == 0) return true;
        for (int i = 0; i < record.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(record[i])) return false;
        }

        return true;
    }

    private static string Cell(List<string> row, int index)
    {
        if (row == null || index < 0 || index >= row.Count || row[index] == null) return "";
        return row[index];
    }

    private static string Decode(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return "";
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        try
        {
            UTF8Encoding utf8 = new UTF8Encoding(false, true);
            return utf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            try
            {
                return Encoding.GetEncoding(932).GetString(bytes);
            }
            catch (Exception)
            {
                throw new InvalidDataException("CSV は UTF-8 で保存してください。");
            }
        }
    }

    private static string ToAbsolutePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        if (Path.IsPathRooted(path)) return path;
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, path);
    }

    private static string SanitizeFileName(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName)) assetName = "StoryVariableDatabase";
        char[] invalid = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalid.Length; i++)
        {
            assetName = assetName.Replace(invalid[i].ToString(), "");
        }

        assetName = assetName.Trim();
        if (assetName.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
        {
            assetName = assetName.Substring(0, assetName.Length - ".asset".Length);
        }

        if (string.IsNullOrEmpty(assetName)) assetName = "StoryVariableDatabase";
        return assetName;
    }

    private static List<StoryVariableDefinition> CloneList(IList<StoryVariableDefinition> variables)
    {
        List<StoryVariableDefinition> copy = new List<StoryVariableDefinition>();
        for (int i = 0; i < variables.Count; i++)
        {
            StoryVariableDefinition source = variables[i];
            if (source == null || string.IsNullOrEmpty(source.key)) continue;
            copy.Add(new StoryVariableDefinition
            {
                key = source.key,
                description = source.description ?? "",
                type = source.type,
                defaultIntValue = source.defaultIntValue,
                defaultBoolValue = source.defaultBoolValue,
                defaultFloatValue = source.defaultFloatValue,
                defaultStringValue = source.defaultStringValue ?? ""
            });
        }

        return copy;
    }
}
