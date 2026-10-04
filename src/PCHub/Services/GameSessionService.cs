using System.Diagnostics;
using System.IO;
using PCHub.Helpers;
using PCHub.Models;

namespace PCHub.Services;

public enum SessionState
{
    Idle,
    Preparing,      // กำลังเตรียมเครื่อง (ปิดแอพ สลับโหมดพลังงาน)
    WaitingForGame, // สั่งเปิดเกมแล้ว รอเกมขึ้น
    Playing,
    Ended,
}

/// <summary>ผลการเล่น 1 รอบ (Session = null ถ้าไม่เห็นเกมเปิดขึ้นมา)</summary>
public record SessionResult(Game Game, PlaySession? Session, int ReopenedApps);

/// <summary>
/// เซสชันเล่นเกม: เตรียมเครื่อง → เปิดเกม → จับเวลาจนเลิกเล่น → คืนค่าเครื่อง
/// มีได้ทีละ 1 เซสชัน ใช้ผ่าน GameSessionService.Instance
/// </summary>
public class GameSessionService : ObservableObject
{
    private static readonly TimeSpan WaitForGameTimeout = TimeSpan.FromMinutes(5); // เผื่อ launcher อัปเดตเกมก่อน
    private static readonly TimeSpan MinimumSession = TimeSpan.FromMinutes(1);

    // ประกาศหลังค่าคงที่ด้านบน (C# ตั้งค่าตัวแปร static ตามลำดับบรรทัด)
    public static GameSessionService Instance { get; } = new();

    private CancellationTokenSource? _stop;

    private SessionState _state;
    public SessionState State
    {
        get => _state;
        private set
        {
            if (SetField(ref _state, value)) OnPropertyChanged(nameof(IsActive));
        }
    }

    public bool IsActive => State is SessionState.Preparing or SessionState.WaitingForGame or SessionState.Playing;

    private Game? _game;
    public Game? CurrentGame
    {
        get => _game;
        private set => SetField(ref _game, value);
    }

    private DateTime? _startedAt;
    /// <summary>เวลาที่เห็นเกมเปิดขึ้นมา</summary>
    public DateTime? StartedAt
    {
        get => _startedAt;
        private set => SetField(ref _startedAt, value);
    }

    /// <summary>true = หาเกมเองไม่ได้ ต้องกด "จบเซสชัน" เอง</summary>
    public bool ManualStopOnly { get; private set; }

    public event Action<SessionResult>? SessionEnded;

    /// <summary>เริ่มเล่นเกม คืนค่า false ถ้าเปิดเกมไม่ได้ (เซสชันจบไปแล้วเมื่อ Task นี้เสร็จ)</summary>
    public async Task<bool> PlayAsync(Game game)
    {
        if (IsActive) return false;

        _stop = new CancellationTokenSource();
        var token = _stop.Token;
        CurrentGame = game;
        StartedAt = null;
        ManualStopOnly = false;
        State = SessionState.Preparing;

        var boost = SettingsService.Boost;
        List<ClosedApp> closed = [];
        PowerState? power = null;
        if (boost.Enabled)
        {
            var toClose = boost.CloseApps.ToList();
            closed = await Task.Run(() => BoostService.CloseApps(toClose));
            if (boost.HighPerformance) power = await Task.Run(PowerPlanService.Boost);
            foreach (var app in boost.CompanionApps.Where(a => !IsRunning(a))) AppLauncher.Launch(app);
        }

        // กดยกเลิกระหว่างเตรียมเครื่อง: ไม่ต้องเปิดเกม
        if (token.IsCancellationRequested)
        {
            var restored = await RestoreAsync(power, closed, boost);
            State = SessionState.Ended;
            SessionEnded?.Invoke(new SessionResult(game, null, restored));
            return true;
        }

        if (!AppLauncher.Launch(game.ToAppEntry()))
        {
            await RestoreAsync(power, closed, boost);
            State = SessionState.Idle;
            return false;
        }

        State = SessionState.WaitingForGame;
        DateTime? start = null;
        DateTime? lastSeen = null; // ครั้งสุดท้ายที่ยังเห็นเกมเปิดอยู่ (ใช้เป็นเวลาเลิกเล่น)
        var folder = game.InstallFolder;

        if (folder != null && Directory.Exists(folder))
        {
            var deadline = DateTime.Now + WaitForGameTimeout;
            while (DateTime.Now < deadline && !token.IsCancellationRequested)
            {
                if (await Task.Run(() => ProcessHelper.AnyRunningIn(folder)))
                {
                    start = lastSeen = DateTime.Now;
                    break;
                }
                await Delay(TimeSpan.FromSeconds(3), token);
            }

            if (start != null)
            {
                StartedAt = start;
                State = SessionState.Playing;
                // เช็คทุก 5 วินาที ไม่เจอเกม 2 ครั้งติดกัน = เลิกเล่นแล้ว (กันพลาดตอนเกมรีสตาร์ทตัวเอง)
                var misses = 0;
                while (misses < 2 && !token.IsCancellationRequested)
                {
                    await Delay(TimeSpan.FromSeconds(5), token);
                    if (token.IsCancellationRequested) break;
                    if (await Task.Run(() => ProcessHelper.AnyRunningIn(folder)))
                    {
                        misses = 0;
                        lastSeen = DateTime.Now;
                    }
                    else
                    {
                        misses++;
                    }
                }
                // กดจบเอง = นับถึงตอนกด, เกมปิดเอง = นับถึงครั้งสุดท้ายที่เห็นเกม
                if (token.IsCancellationRequested) lastSeen = DateTime.Now;
            }
        }
        else
        {
            // ไม่รู้ว่าเกมลงไว้ที่ไหน จับเวลาจนกว่าจะกด "จบเซสชัน" เอง
            ManualStopOnly = true;
            start = DateTime.Now;
            StartedAt = start;
            State = SessionState.Playing;
            await Delay(Timeout.InfiniteTimeSpan, token);
            lastSeen = DateTime.Now;
        }

        PlaySession? session = null;
        if (start != null)
        {
            session = new PlaySession(game.Id, game.Name, start.Value, lastSeen ?? DateTime.Now);
            if (session.Duration >= MinimumSession) PlayTimeService.Record(session);
        }

        var reopened = await RestoreAsync(power, closed, boost);
        State = SessionState.Ended;
        SessionEnded?.Invoke(new SessionResult(game, session, reopened));
        return true;
    }

    /// <summary>จบเซสชันเอง (กดปุ่ม "จบเซสชัน")</summary>
    public void Stop() => _stop?.Cancel();

    /// <summary>ปิดการ์ดสรุปผล กลับไปสถานะว่าง</summary>
    public void Dismiss()
    {
        if (State == SessionState.Ended) State = SessionState.Idle;
    }

    private static async Task<int> RestoreAsync(PowerState? power, List<ClosedApp> closed, BoostSettings boost)
    {
        if (power != null) await Task.Run(() => PowerPlanService.Restore(power));
        if (!boost.RestoreApps || closed.Count == 0) return 0;
        await Task.Run(() => BoostService.Reopen(closed));
        return closed.Count;
    }

    /// <summary>แอพเปิดอยู่แล้วไหม (เดาจากชื่อ เช่น "Discord" → โปรเซส Discord)</summary>
    private static bool IsRunning(AppEntry app)
    {
        var processes = Process.GetProcessesByName(app.Name.Replace(" ", ""));
        foreach (var p in processes) p.Dispose();
        return processes.Length > 0;
    }

    private static async Task Delay(TimeSpan time, CancellationToken token)
    {
        try
        {
            await Task.Delay(time, token);
        }
        catch (TaskCanceledException)
        {
            // กดจบเซสชัน
        }
    }
}
