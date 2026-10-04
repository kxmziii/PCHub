using System.Text;

namespace PCHub.Helpers;

/// <summary>
/// อ่านไฟล์ .vdf ของ Steam (รูปแบบ "key" "value" และ "key" { ... })
/// ได้ผลเป็น Dictionary ที่ค่าเป็น string หรือ Dictionary ซ้อนกัน (ชื่อ key ไม่สนตัวพิมพ์เล็ก/ใหญ่)
/// </summary>
public static class VdfParser
{
    public static Dictionary<string, object> Parse(string text)
    {
        var position = 0;
        return ParseObject(text, ref position);
    }

    /// <summary>เดินตาม key ทีละชั้น เช่น Find(root, "Software", "Valve") คืน null ถ้าไม่เจอ</summary>
    public static Dictionary<string, object>? Find(Dictionary<string, object> root, params string[] path)
    {
        var current = root;
        foreach (var key in path)
        {
            if (!current.TryGetValue(key, out var next) || next is not Dictionary<string, object> child) return null;
            current = child;
        }
        return current;
    }

    private static Dictionary<string, object> ParseObject(string text, ref int position)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var key = NextToken(text, ref position);
            if (key == null || key.Value is { IsBrace: true, Text: "}" }) return result;

            var value = NextToken(text, ref position);
            if (value == null) return result;
            result[key.Value.Text] = value.Value is { IsBrace: true, Text: "{" }
                ? ParseObject(text, ref position)
                : value.Value.Text;
        }
    }

    private static (string Text, bool IsBrace)? NextToken(string text, ref int position)
    {
        while (position < text.Length)
        {
            var c = text[position];
            if (char.IsWhiteSpace(c))
            {
                position++;
            }
            else if (c == '/' && position + 1 < text.Length && text[position + 1] == '/')
            {
                while (position < text.Length && text[position] != '\n') position++; // คอมเมนต์
            }
            else if (c is '{' or '}')
            {
                position++;
                return (c.ToString(), true);
            }
            else if (c == '"')
            {
                var builder = new StringBuilder();
                position++;
                while (position < text.Length && text[position] != '"')
                {
                    if (text[position] == '\\' && position + 1 < text.Length) position++;
                    builder.Append(text[position]);
                    position++;
                }
                position++;
                return (builder.ToString(), false);
            }
            else
            {
                var start = position;
                while (position < text.Length && !char.IsWhiteSpace(text[position]) && text[position] is not ('{' or '}' or '"'))
                    position++;
                return (text[start..position], false);
            }
        }
        return null;
    }
}
