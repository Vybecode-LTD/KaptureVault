---
document: TESTING
version: 1.2.0
app-version: 1.0.3
last-updated: 2026-05-30
last-audit: 2026-05-30
managed-by: manual-reconciliation
see-also: [CLAUDE.md, docs/BUGS.md, docs/ROADMAP.md, docs/HANDOFF.md, ../../TESTING_PROCEDURES.md]
---

# TESTING.md — KaptureVault

## Current state

**Test project: LIVE** (`KaptureVault.Tests`, xUnit + NSubstitute + FluentAssertions + coverlet, on `KaptureVault.slnx`). **30 tests passing** as of 2026-05-30. Persistence seams in place: base-dir (`EncryptionService`), connection-string (`DatabaseService`). The app project excludes `KaptureVault.Tests/**` from its compile glob.

Suite inventory:
| Suite | Covers | Tests |
|---|---|---|
| `Services/CaptureServiceTests` | KV-005 self-exclusion regression | 2 |
| `Services/EncryptionServiceTests` | KV-002 round-trip / tamper→throw / wrong-key→throw / passthrough | 4 |
| `Services/DatabaseServiceSearchTests` | KV-004 encrypted-content search | 3 |
| `Services/DatabaseServiceReplaceTests` | KV-003 pre-sync backup retention | 1 |
| `Services/DatabaseServiceCrudTests` | KV-009 full-field round-trip, null-expiry, pin/tags, GetAll limit | 4 |
| `ViewModels/ConverterTests` | KV-033 brush caching + pure text/number converters | 16 |

**Run:** `dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj` (or `dotnet test KaptureVault.slnx`).

### Standing testing directive (binding — see `../../TESTING_PROCEDURES.md`, summarized in `CLAUDE.md`)
- **Every bug fix gets a regression test that fails before the fix and passes after** (proven RED→GREEN). New file → its test; new public method → test it.
- Never touch the real `%LOCALAPPDATA%\KaptureVault` vault in tests — use temp dirs / shared in-memory SQLite.
- **Required C# checks before declaring done (report results — evidence ledger):** `dotnet build`, `dotnet build -c Release`, `dotnet test --collect:"XPlat Code Coverage"`, `dotnet format --verify-no-changes`, `dotnet list package --vulnerable --include-transitive`, `dotnet publish -c Release -r win-x64` (deliverables).
- 2-strike on the same bug → enter the **DEBUG_PROTOCOL** diagnostic mode (freeze edits, get evidence). `BREAKLOOP` forces it.

### Known gaps (KV-045 / T-16)
- No `Avalonia.Headless.XUnit` UI smoke tests (app builder / window-open / binding-resolves) yet.
- No **VM filter regression** test yet (the `AppList`/`TagList` diff-update fix) — high priority since it broke twice.
- No CI test job (only `auto-release.yml`); `dotnet format` + vulnerable-scan not yet wired into the loop.
- Coverage % not yet collected/tracked.

---

## Recommended stack

A **separate** test project — do not add test packages to the WinExe app.

- **Project:** `KaptureVault.Tests/KaptureVault.Tests.csproj`, `<TargetFramework>net9.0-windows</TargetFramework>`, `<IsPackable>false</IsPackable>`, `ProjectReference` → `KaptureVault.csproj`. Also add a root `KaptureVault.sln` (none exists today).

| Package | Purpose |
|---|---|
| `Microsoft.NET.Test.Sdk` | test host |
| `xunit` + `xunit.runner.visualstudio` | framework (`[Fact]`/`[Theory]`) |
| `NSubstitute` | mock the service interfaces |
| `FluentAssertions` | assertions (per project convention) |
| `Microsoft.Data.Sqlite` (9.0.0, match app) | in-memory SQLite for DB tests |
| `coverlet.collector` | coverage (`--collect:"XPlat Code Coverage"`) |
| `Avalonia.Headless.XUnit` (11.3.12) | **only** for VM/converter tests touching Avalonia types |
| `Avalonia.Themes.Fluent` (11.3.12) | required by the headless `TestAppBuilder` |

**Two tiers:** a *pure* unit tier (no Avalonia runtime) for services/models/filter-logic, and a *headless* tier (`[AvaloniaFact]`) for brush-returning converters and any dispatcher/clipboard path. In headless tests call `Dispatcher.UIThread.RunJobs()` between act and assert to drain the `Dispatcher.UIThread.Post(Refresh)` callbacks wired in the VM constructor.

---

## Ranked test targets (risk × testability)

| # | Target | File | Risk | Priority |
|---|--------|------|------|----------|
| 1 | `EncryptionService` round-trip / wrong-password / **tamper rejection** | `Services/EncryptionService.cs` | Critical | P0 |
| 2 | `DatabaseService` CRUD / search / expiry / encrypt-at-rest (in-memory SQLite) | `Services/DatabaseService.cs` | Critical | P0 |
| 3 | `MainWindowViewModel` filter logic + **app-filter-loses-selection regression** | `ViewModels/MainWindowViewModel.cs` | High | P0 |
| 4 | `LanguageDetector.Detect/GetDisplayName` (pure static) | `Services/LanguageDetector.cs` | Medium | P1 |
| 5 | `CaptureService` buffer logic (mock hook events; skip timers) | `Services/CaptureService.cs` | High | P1 |
| 6 | `CaptureEntry` (`TagList`, `IsScreenshot`, `ScreenshotPath`) | `Models/CaptureEntry.cs` | Low-Med | P1 |
| 7 | `AppSettings` JSON round-trip + defaults + `[JsonPropertyName]` keys | `Models/AppSettings.cs` | Medium | P1 |
| 8 | Converters via the public static fields on `MainWindowViewModel` | `ViewModels/Converters.cs` | Low-Med | P1 |
| 9 | `ExpiryDialogViewModel` options/mapping | `ViewModels/ExpiryDialogViewModel.cs` | Low | P2 |
| 10 | `ThemeRegistry` (6 themes present, case-insensitive) | `Themes/ThemeRegistry.cs` | Low | P2 |

---

## Seams needed before some targets are testable

The three persistence services hardcode their LocalAppData paths (`static readonly` / ctor `Path.Combine`) — **the top blocker**:
- `DatabaseService` (ctor builds `…\vault.db`) → add `DatabaseService(IEncryptionService? enc = null, string? connectionString = null)`. Use a **shared-cache named in-memory DB** held open by a keep-alive connection (`Data Source=file:memdb-{guid}?mode=memory&cache=shared`) because `Open()` opens/closes per call.
- `EncryptionService` / `SettingsService` → add an injectable base directory (default to current LocalAppData path so production is unchanged).

**Genuinely not unit-testable** (defer to manual/E2E, Windows-only): `KeyboardHookService`, `HotkeyService`, `ActiveWindowService`, the clipboard/registry internals of `ClipboardMonitorService`/`ScreenshotService`/`StartupService` (static Win32 P/Invoke + message loops). Their *consumers* are testable via the existing interfaces — keep that boundary.

---

## First-PR plan (8 files)

**Step 0 — infra:** create the test project + `.sln`; add the ctor seams above (default args preserve prod paths); add the headless `TestAppBuilder`.

1. `LanguageDetectorTests.cs` — ~10 languages + sub-threshold/empty → null (pure, fastest win)
2. `CaptureEntryTests.cs` — TagList split/trim/dedupe, screenshot flags
3. `AppSettingsSerializationTests.cs` — defaults + round-trip + snake-case keys
4. `MainWindowViewModelFilterTests.cs` — **regression centerpiece** (scenarios below)
5. `EncryptionServiceTests.cs` — round-trip, wrong-password, **tamper → rejection**, non-`ENC:` passthrough
6. `DatabaseServiceTests.cs` — CRUD/search/expiry + encrypt-at-rest round-trip (in-memory)
7. `ConverterTests.cs` — preview truncation, icon mapping, buffer-fill math (brush converters → headless)
8. `ThemeRegistryTests.cs` *(optional)* — theme presence/typo guard

**Regression scenarios for the filter bug (file 4), asserted via public surface:**
1. Selection survives a refresh that re-lists apps (`SelectedAppFilter` stays `"chrome"`, not `"All Apps"`).
2. Selecting an app calls `_db.GetByApp("chrome")`, not `GetAll`.
3. A vanished app falls back to `"All Apps"` + `GetAll`.
4. `"All Apps"` always present and first after `RefreshAppList`.
5. Mirror for tags (`RefreshTagList`).

**CI follow-on:** add `.github/workflows/test.yml` → `dotnet test --logger trx --collect:"XPlat Code Coverage"` on `windows-latest` (required for the `net9.0-windows` target). Keep `auto-release.yml` as tag-only packaging.

---

## Coverage thresholds (target, once suite exists)
Per the project constitution: PR gate 85% line, deploy gate 95%, new/security-critical code 95%. Encryption + database + sync are security-critical → 95% target.
