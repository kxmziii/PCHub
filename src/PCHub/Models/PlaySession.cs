using System.Text.Json.Serialization;

namespace PCHub.Models;

/// <summary>การเล่นเกม 1 ครั้ง (บันทึกลง playtime.json ใช้ทำสถิติและ Gaming Wrapped)</summary>
public record PlaySession(string GameId, string GameName, DateTime Start, DateTime End)
{
    [JsonIgnore]
    public TimeSpan Duration => End - Start;
}
