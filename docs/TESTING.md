---
document: TESTING
version: 1.11.0
app-version: 1.0.7
last-updated: 2026-06-01
last-audit: 2026-06-01
managed-by: manual-reconciliation
see-also: [CLAUDE.md, docs/BUGS.md, docs/ROADMAP.md, docs/HANDOFF.md, ../../TESTING_PROCEDURES.md]
---

# TESTING.md — KaptureVault

## Current state

**Test project: LIVE** (`KaptureVault.Tests`, xUnit + NSubstitute + FluentAssertions + coverlet + `Avalonia.Headless.XUnit`, on `KaptureVault.slnx`). **120 tests passing** as of 2026-06-01 (71 + 49 from F-02 Phase 2; +2 from Phase 0/1a — email + OpenVault). Persistence seams in place: base-dir (`EncryptionService`), connection-string (`DatabaseService`). The app project excludes `KaptureVault.Tests/**` from its compile glob. The headless tier uses `TestAppBuilder` (`[assembly: AvaloniaTestApplication]`) over the real `App`.

Suite inventory:
| Suite | Covers | Tests |
|---|---|---|
| `Services/CaptureServiceTests` | KV-005 self-exclusion regression; KV-012/T-07 non-blocking flush (`Flush_DoesNotBlockTheHookThreadOnTheDatabaseWrite`) + drain-on-stop (`Stop_DrainsBufferedEntriesAndDoesNotLoseData`) | 4 |
| `Services/EncryptionServiceTests` | KV-002 round-trip / tamper→throw / wrong-key→throw / passthrough; KV-006/T-11 strong-KDF persistence (`Configure_StoresStrongKdfParams_AndDerivesWithThem`) + legacy-vault unlock (`Unlock_LegacyVaultWithoutStoredIterations_StillUnlocksAndDecrypts`) | 6 |
| `Services/DatabaseServiceSearchTests` | KV-004 encrypted-content search | 3 |
| `Services/DatabaseServiceReplaceTests` | KV-003 pre-sync backup retention | 1 |
| `Services/DatabaseServiceCrudTests` | KV-009 full-field round-trip, null-expiry, pin/tags, GetAll limit | 4 |
| `Services/DatabaseServiceBackupTests` | F-01 local DB export: `CreateBackupCopy` writes a standalone copy containing every row; empty-vault backup is still a valid vault | 2 |
| `Services/ServiceRegistrationTests` | KV-010/T-10 DI composition + F-02 online services registered + both cloud providers present | 21 |
| `ViewModels/ConverterTests` | KV-033 brush caching + pure text/number converters | 16 |
| `ViewModels/MainWindowViewModelFilterTests` | T-16/KV-013 filter-selection regression: app/tag filter + selected entry survive a background Refresh; filter narrows Entries; selection clears when it leaves the vault | 7 |
| `ViewModels/MainWindowViewModelEntriesDiffTests` | T-09/KV-013 Entries diff-update: instance reuse, prepend ordering, removal; CaptureEntry IsPinned/Tags change notifications | 5 |
| `Views/MainWindowSmokeTests` | T-16 headless `[AvaloniaFact]`: MainWindow constructs + shows; the real sidebar ListBox SelectedItem binding keeps the filter across a refresh | 2 |
| `ShutdownCoordinatorTests` | T-08/KV-011 teardown: stops capture, sync-on-close gating, idempotency, swallows sync failures | 8 |
| `Services/CloudSync/KaptureOnlineApiClientTests` | F-02 P2: Online Vault HTTP contract (auth/session, auth/google, /me, billing, vault put/get-url, meta read+write, 401/402 mapping) via a stub handler | 11 |
| `Services/CloudSync/OnlineAccountServiceTests` | F-02 P2: secret-less sign-in, DPAPI session + auto-refresh (near-expiry + 401), sign-out, entitlement from /me, checkout/portal URLs | 13 |
| `Services/CloudSync/R2StorageProviderTests` | F-02 P2: Online Vault as `ICloudStorageProvider` — upload/download over presigned URLs + meta, find/mtime, R2 failure surfaced | 6 |
| `ViewModels/OnlineAccountViewModelTests` | F-02 P2: Settings account-panel logic — sign-in gating + persist provider, subscribe/billing open URLs, sign-out clears provider | 9 |

**Total: 120 tests.** *(F-02 client suites: KaptureOnlineApiClientTests 11, OnlineAccountServiceTests 13, R2StorageProviderTests 6, OnlineAccountViewModelTests 11.)*

> **F-02 Online Vault backend is a SEPARATE repo** — `kapturevault-backend` (`C:\dev\kapturevault-backend` / `github.com/Vybecode-LTD/kapturevault-backend`) with its **own** test suite: **26 vitest tests + `tsc --noEmit` typecheck + GitHub Actions CI** (`npm ci` → typecheck → test). Those are **not** part of the .NET `KaptureVault.Tests` count above.

**CI: LIVE.** `.github/workflows/tests.yml` runs on every push/PR to `main` (windows-latest, .NET 9) and enforces, in order: `dotnet build` → `dotnet format --verify-no-changes` → `dotnet list package --vulnerable --include-transitive` (fails the run on any vulnerable package) → `dotnet test` (TRX + Cobertura coverage). Verified green on GitHub (Actions run 26725669973, all steps passing).

**Run:** `dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj` (or `dotnet test KaptureVault.slnx`).

### Standing testing directive (binding — see `../../TESTING_PROCEDURES.md`, summarized in `CLAUDE.md`)
- **Every bug fix gets a regression test that fails before the fix and passes after** (proven RED→GREEN). New file → its test; new public method → test it.
- Never touch the real `%LOCALAPPDATA%\KaptureVault` vault in tests — use temp dirs / shared in-memory SQLite.
- **Required C# checks before declaring done (report results — evidence ledger):** `dotnet build`, `dotnet build -c Release`, `dotnet test --collect:"XPlat Code Coverage"`, `dotnet format --verify-no-changes`, `dotnet list package --vulnerable --include-transitive`, `dotnet publish -c Release -r win-x64` (deliverables).
- 2-strike on the same bug → enter the **DEBUG_PROTOCOL** diagnostic mode (freeze edits, get evidence). `BREAKLOOP` forces it.

### Known gaps
- ✅ `Avalonia.Headless.XUnit` smoke tests — added 2026-06-01 (`MainWindowSmokeTests`: window constructs/shows + ListBox binding survives a refresh).
- ✅ **VM filter regression** — added 2026-06-01 (`MainWindowViewModelFilterTests` + `MainWindowViewModelEntriesDiffTests`).
- Coverage % not yet collected/tracked (the only remaining T-16 nicety; CI already emits Cobertura).
- `KeyboardHookService`/`HotkeyService`/`ActiveWindowService` stay manual/E2E only (Win32 message loops) — by design.

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
