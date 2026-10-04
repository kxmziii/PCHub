using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PCHub.Services;

/// <summary>สิ่งที่ต้องใช้ตอนเปลี่ยนโหมดพลังงานกลับเป็นแบบเดิม</summary>
public record PowerState(string? PreviousScheme, Guid? PreviousOverlay);

/// <summary>สลับโหมดพลังงานของ Windows เป็นแบบแรงสุดระหว่างเล่นเกม แล้วเปลี่ยนกลับตอนเลิกเล่น</summary>
public static class PowerPlanService
{
    private const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    // โหมด "ประสิทธิภาพดีที่สุด" ในหน้าตั้งค่าพลังงานของ Windows 10/11
    private static readonly Guid BestPerformanceOverlay = new("ded574b5-45a0-4f42-8737-46345c09c238");

    /// <summary>สลับเป็นโหมดแรงสุด คืนค่า null ถ้าไม่ได้เปลี่ยนอะไร (แรงสุดอยู่แล้ว หรือเครื่องไม่รองรับ)</summary>
    public static PowerState? Boost()
    {
        var list = RunPowerCfg("/list");
        if (list != null)
        {
            var schemes = Regex.Matches(list, @"([0-9a-fA-F]{8}-[0-9a-fA-F-]{27})\s+\((.*?)\)(\s*\*)?")
                .Select(m => (Guid: m.Groups[1].Value.ToLowerInvariant(), Name: m.Groups[2].Value, Active: m.Groups[3].Success))
                .ToList();
            var active = schemes.FirstOrDefault(s => s.Active).Guid;

            // ใช้ Ultimate Performance ถ้ามี ไม่งั้นใช้ High performance
            var target = schemes.FirstOrDefault(s => s.Name.Contains("Ultimate", StringComparison.OrdinalIgnoreCase)).Guid
                         ?? schemes.FirstOrDefault(s => s.Guid == HighPerformance).Guid;
            if (target != null)
            {
                if (target == active) return null;
                if (RunPowerCfg($"/setactive {target}") != null) return new PowerState(active, null);
            }
        }

        // โน้ตบุ๊กรุ่นใหม่มักไม่มี High performance ให้ใช้โหมดพลังงานของ Windows แทน
        if (PowerGetEffectiveOverlayScheme(out var overlay) == 0 && overlay != BestPerformanceOverlay &&
            PowerSetActiveOverlayScheme(BestPerformanceOverlay) == 0)
        {
            return new PowerState(null, overlay);
        }
        return null;
    }

    public static void Restore(PowerState state)
    {
        if (state.PreviousScheme != null) RunPowerCfg($"/setactive {state.PreviousScheme}");
        if (state.PreviousOverlay is { } overlay) PowerSetActiveOverlayScheme(overlay);
    }

    /// <summary>รัน powercfg คืนข้อความที่ได้ (null ถ้าไม่สำเร็จ)</summary>
    private static string? RunPowerCfg(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("powercfg.exe", arguments)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process == null) return null;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return process.ExitCode == 0 ? output : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetEffectiveOverlayScheme(out Guid overlay);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveOverlayScheme(Guid overlay);
}
