# PC Hub: notes for Claude

Thai gamer helper app for Windows: "กดทีเดียว เครื่องพร้อมเล่น". WPF on .NET 10, C#.
Public repo: https://github.com/kxmziii/PCHub (auto-update via GitHub Releases + Velopack).

## The user
- Thai, beginner programmer, plays PUBG / Apex / The Isle / FiveM. Talk in Thai, short and concrete.
- Likes step-by-step progress, asks "ทำอะไรต่อดี" and expects a clear recommendation.
- Installed copy lives in `%LOCALAPPDATA%\PCHub` and auto-updates; user data in `%APPDATA%\PCHub`
  (settings.json, playtime.json, alerts.json, logs\). Never delete or overwrite user data in tests.

## Code conventions
- All code comments and UI text in Thai; identifiers in English. Match surrounding style.
- Views use code-behind (no MVVM framework). Small row view-models live next to their view
  (e.g. `FiveMRow`, `ToggleItem`, `GameStorageItem`) and derive from `Helpers/ObservableObject`.
- Colors, fonts, buttons, cards: `src/PCHub/Themes/Theme.xaml` (dark theme). Reuse existing styles
  (Card, PrimaryButton, SecondaryButton, IconButton, FilterChip, Switch, GameCard, SubtleButton...).
- Icons: Segoe Fluent Icons glyphs (`{StaticResource Icon}` TextBlock).
- Services are static classes or `Instance` singletons in `src/PCHub/Services`.
- Log notable events/failures with `AppLog.Info/Warn/Error` (never log passwords/tokens).
- Network failures are caught per-source so one store/launcher failing never breaks a page.

## Build, run, verify
- dotnet may not be on PATH in tool shells: use `"C:\Program Files\dotnet\dotnet.exe"`.
- Build: `dotnet build PCHub.slnx -v q`. Stop the dev exe first if running (only processes under `Projects\PCHub`,
  never the installed one in `AppData\Local\PCHub` unless the user asks).
- Visual check without disturbing the user: Debug builds accept
  `PCHub.exe --snapshot out.png [--page games|fivem|deals|wrapped|modes|cleaner|storage|discord|tools|settings|mode-editor|boost|add-watch|confirm-play|splash]`
  `[--delay ms] [--size WxH] [--background] [--demo-session] [--scan] [--crash-test] [--export-wrapped out.png] [--search term]`.
  The window opens off-screen without focus and without a tray icon. Save snapshots to the scratchpad, then Read them.
- Logic tests: a throwaway console/WinExe project in the scratchpad with a ProjectReference to
  `src/PCHub/PCHub.csproj` (call `VelopackApp` is NOT needed; UpdateService handles that).
  Use fake games (copy the test exe into a sandbox folder and run it with `--sleep ms`). Clean up anything created.
- After adding timers/background work, measure CPU of the dev build with `--background` (should be ~0-1%).

## Release
1. Bump `<Version>` in `src/PCHub/PCHub.csproj`.
2. Commit (author `kxmziii <337754657+kxmziii@users.noreply.github.com>`, end message with the Co-Authored-By line) and push.
3. `powershell -ExecutionPolicy Bypass -File tools\release.ps1 -Publish` (needs `C:\Program Files\GitHub CLI` on PATH; gh is logged in).
4. Verify: `https://github.com/kxmziii/PCHub/releases/latest/download/releases.win.json` lists the new version.

## Gotchas learned the hard way
- Static field order matters: declare `static readonly` fields BEFORE `public static X Instance { get; } = new();`
  (GameWatcher once got a 0s DispatcherTimer interval → 166% CPU and unclickable window).
- PowerShell 5.1 scripts with Thai text need UTF-8 **with BOM** (tools/*.ps1). Variable names are case-insensitive
  (`$publish` clobbered `[switch]$Publish`).
- WPF: `WindowStartupLocation` cannot be set in a Style; custom TextBox templates must not re-apply Padding;
  `RenderTargetBitmap` of an element with Margin is offset (snapshot renders the window's root child).
- App.xaml is a Page; `Program.Main` runs `VelopackApp.Build()...Run()` first, then single-instance mutex + show signal.
- In this harness, `Remove-Item` must be its own command with `-LiteralPath` (mixed paths get blocked).
- Rewriting git history is blocked for Claude; the user must run such commands themselves.
- FiveM: server list = protobuf stream at `frontend.cfx-services.net/api/servers/stream/`; icons at
  `frontend.cfx-services.net/api/servers/icon/{code}/{iconVersion}.png`; join by passing
  `fivem://connect/cfx.re/join/{code}` to FiveM.exe. Never delete `game-storage` or `nui-storage`.
- Steam wishlist of this user is empty/private; price watch also supports manually added games.

## Roadmap (agreed with the user)
- Next: Achievements 🏆 (no sign-ups needed), then share v0.6 with 3-5 friends and collect feedback.
- Later: accounts + friends + leaderboard (Supabase + Discord login; the user must create those accounts),
  IsThereAnyDeal "historical low" (user must get a free API key), Discord server analyzer (from Discord data package),
  Tools page extras (RAM hogs, shutdown timer, organize Downloads), global hotkeys.
- Don't: inject into games / read game memory (anti-cheat), claim big FPS gains, build a VPN.
