using System.Windows.Threading;
using PCHub.Models;

namespace PCHub.Services;

/// <summary>
/// จับเวลาเล่นเองแม้เปิดเกมจาก Steam/Epic โดยตรง (ไม่ได้กดเล่นผ่าน PC Hub)
/// ทุก 15 วินาทีดูว่ามีเกมในคลังเปิดอยู่ไหม (ดูแค่ที่อยู่ไฟล์ .exe เหมือนเซสชันปกติ)
/// </summary>
public sealed class GameWatcher
{
    // ต้องประกาศก่อน Instance: C# ตั้งค่าตัวแปร static ตามลำดับบรรทัด
    // ถ้า Instance มาก่อน ตอนสร้าง timer ค่า Interval จะยังเป็น 0 แล้ว timer จะทำงานรัวไม่หยุด (CPU พุ่ง)
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MinimumSession = TimeSpan.FromMinutes(1);

    public static GameWatcher Instance { get; } = new();

    private sealed class Tracked(Game game, DateTime start)
    {
        public Game Game { get; } = game;
        public DateTime Start { get; } = start;
        public DateTime LastSeen { get; set; } = start;
        public int Misses { get; set; }
    }

    private readonly DispatcherTimer _timer = new() { Interval = Interval };
    private readonly Dictionary<string, Tracked> _tracked = new();
    private bool _busy;

    /// <summary>บันทึกเวลาเล่นได้ 1 รอบ (หน้าคลังเกมใช้อัปเดตชั่วโมงบนการ์ด)</summary>
    public event Action<PlaySession>? SessionRecorded;

    private GameWatcher() => _timer.Tick += async (_, _) => await TickAsync();

    public void Start() => _timer.Start();

    /// <summary>ตอนปิดโปรแกรม: บันทึกเกมที่ยังเล่นอยู่ถึงครั้งสุดท้ายที่เห็น</summary>
    public void Stop()
    {
        _timer.Stop();
        foreach (var id in _tracked.Keys.ToList()) Finish(id);
    }

    private async Task TickAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            if (!SettingsService.General.AutoTrackGames)
            {
                foreach (var id in _tracked.Keys.ToList()) Finish(id);
                return;
            }

            // ไม่นับเกมที่ซ่อนไว้ (เช่น Wallpaper Engine ที่เปิดทั้งวัน) และ launcher ที่ไม่ใช่ตัวเกม (หมวดอื่นๆ)
            var hidden = SettingsService.Current.HiddenGames.ToHashSet();
            var games = (await GameLibraryService.GetAsync())
                .Where(g => g.Source != GameSource.Other && g.InstallFolder != null && !hidden.Contains(g.Id) &&
                            !StorageService.IsTooBroad(g.InstallFolder))
                .ToDictionary(g => g.Id);
            var targets = games.Values.Select(g => (g.Id, g.InstallFolder!)).ToList();
            var running = await Task.Run(() => ProcessHelper.FindRunning(targets));

            // เกมที่กำลังเล่นผ่านเซสชันของ PC Hub นับแยกอยู่แล้ว ไม่นับซ้ำ
            var session = GameSessionService.Instance;
            if (session.IsActive && session.CurrentGame != null) running.Remove(session.CurrentGame.Id);

            var now = DateTime.Now;
            foreach (var id in running)
            {
                if (_tracked.TryGetValue(id, out var tracked))
                {
                    tracked.LastSeen = now;
                    tracked.Misses = 0;
                }
                else
                {
                    _tracked[id] = new Tracked(games[id], now);
                }
            }

            // ไม่เห็นเกม 2 รอบติดกัน = เลิกเล่นแล้ว (กันพลาดตอนเกมรีสตาร์ทตัวเอง)
            foreach (var tracked in _tracked.Values.Where(t => !running.Contains(t.Game.Id)).ToList())
            {
                if (++tracked.Misses >= 2) Finish(tracked.Game.Id);
            }
        }
        finally
        {
            _busy = false;
        }
    }

    private void Finish(string id)
    {
        if (!_tracked.Remove(id, out var tracked)) return;
        var played = new PlaySession(tracked.Game.Id, tracked.Game.Name, tracked.Start, tracked.LastSeen);
        if (played.Duration < MinimumSession) return;
        PlayTimeService.Record(played);
        SessionRecorded?.Invoke(played);
    }
}
