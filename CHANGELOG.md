# Changelog

All notable changes to **KaptureVault** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased] — planned v1.1.0 (Online Vault)

The first release of the **Online Vault** — optional, end-to-end-encrypted cloud sync of your
vault and screenshots, plus a web viewer to read them in a browser. Free, offline use and Google
Drive sync are unchanged. *(Goes live once the cloud accounts/origins are configured.)*

### Added
- **Sync your vault and screenshots to your KaptureVault account.** Sign in with Google in
  Settings → Online Vault and choose "Use the Online Vault for sync." Your encrypted vault and
  your screenshots (re-encoded to PNG, end-to-end encrypted) upload to your account and restore
  automatically on another device — counted against your storage quota (oldest kept first when
  you run low). The server only ever stores ciphertext, and a vault password is required since
  that password is the only key.
- **View your vault — including screenshots — in any browser.** The web vault at
  `kapture.tools/vault` can now open your Online Vault: sign in, enter your vault password, and
  browse, search, and view your captured entries and screenshot images. Everything is decrypted
  locally in your browser; the server only ever holds ciphertext.
- **A clear "Use the Online Vault for sync" control** in Settings → Online Vault, with a
  "✓ this is your sync target" indicator, so switching your sync destination takes effect
  immediately.

### Fixed
- **The app no longer closes immediately after you enter your vault password.** A startup
  window-lifecycle bug could shut the app down right after unlocking an encrypted vault; it now
  opens reliably.

---

## [1.0.7] — 2026-06-01

Reliability and performance hardening — the final batch from the post-audit P1 pass.
Internal robustness; no new day-to-day features.

### Changed
- **The vault list no longer flickers or loses your place while you work.** The entry list
  now updates in place instead of being torn down and rebuilt on every capture, so your
  selected entry and the active app/tag filter are preserved. Reading and decrypting entries
  runs off the UI thread and rapid captures are coalesced, keeping the window responsive on
  large, encrypted vaults.

### Fixed
- **Quitting or restarting now shuts everything down cleanly.** Every exit path — Quit, the
  encryption-unlock cancel, and the Capture-Admin-Apps restart — now runs one shared shutdown
  that stops capture, syncs once when enabled, and releases the global hotkey and other
  resources. Previously only the tray "Quit" performed the full cleanup.

---

## [1.0.6] — 2026-06-01

A new free-tier feature — save a complete backup of your vault to a file you choose.

### Added
- **Export your entire vault to a file you choose.** A new **Export DB** button in the
  toolbar saves a complete, standalone backup copy of the vault database (`.db`) to any
  location — independent of Google Drive sync (free tier). If encryption is enabled the
  backup is encrypted and restoring it needs your password. Uses a WAL-safe `VACUUM INTO`
  copy and runs off the UI thread.

---

## [1.0.5] — 2026-05-31

Reliability, security, and architecture hardening from the post-audit P1 pass (batch 2).
Internal robustness/security; no change to day-to-day behavior.

### Changed
- **Typing no longer waits on the database.** When a long burst of captured text was
  saved, the database write happened directly on the system-wide keyboard hook, which
  could add input lag and risk Windows dropping the hook. Saving now happens on a
  background worker, so keystroke capture stays responsive no matter how busy the disk is.
- **Stronger encryption key strengthening.** New encrypted vaults now derive their key with
  600,000 PBKDF2 iterations (up from 100,000, matching current OWASP guidance), and the
  settings used are recorded so the vault still opens after future changes. Existing vaults
  keep working and open exactly as before.

### Fixed
- **The Quick-Paste hotkey and main window are now created through the app's service
  container,** removing a hand-wired object that could leak its background thread on some
  exit paths. No visible change; groundwork for cleaner shutdown.

---

## [1.0.4] — 2026-05-30

Reliability and performance hardening from the post-audit P1 pass. Internal robustness/perf; no change to day-to-day behavior.

### Changed
- **A vault created by a different app version could be misread.** Database reads now
  match columns by name instead of by position, so a schema or version difference can no
  longer shift the data.
- **Database access could clash with a cloud-sync swap.** The guard that blocks reads and
  writes while the local database is being replaced now applies to every database
  operation, not just some of them.
- **Large vaults did extra work on every refresh.** The entry list now shows the 1,000
  most recent items and reuses shared colors, so refreshing stays fast as the vault grows.

### Fixed
- **The screenshot annotation editor leaked memory and could crash on export.** It now
  releases the screenshot image when the window closes, and "Save As" no longer crashes
  when the original screenshot file is missing.

---

## [1.0.3] — 2026-05-30

Security and data-integrity release following a full codebase audit. Addresses the
critical and high-severity findings; first release with an automated test suite.

### Fixed
- **KaptureVault no longer captures its own input.** The self-exclusion check used a
  stale process name (`"Kapture"`), so keystrokes typed into KaptureVault's own
  windows (search, tag boxes) and clipboard content it set itself (Copy, Quick Paste)
  were being recorded. It now derives the name from the running process.
- **Search works when encryption is enabled.** Content search ran against encrypted
  data and silently returned nothing; it now searches your decrypted entries.

### Security
- **Tampered or corrupted entries are detected** instead of being shown as raw
  ciphertext — decryption failures now surface clearly rather than being silently
  ignored (restores the AES-256-GCM integrity guarantee).
- **Rotated all Google OAuth credentials** and purged the previously-committed secret
  from repository history.

### Added
- **Cloud sync keeps a recovery backup.** When a newer copy is pulled from Google
  Drive, the pre-sync local database is retained as `vault.db.pre_sync_backup` so a
  sync can no longer leave you with no way back. (Full multi-device merge is still
  planned; until then, sync remains safest on a single device.)
- **Automated test suite** (`KaptureVault.Tests`) covering the fixes above.

---

## [1.0.2] — 2026-05-30

### Fixed
- **App / Tag sidebar filters losing their selection.** Selecting an application
  (or tag) in the left sidebar would silently fall back to showing all entries —
  especially after opening a screenshot or viewing a full clipboard entry and
  returning to the list. Root cause: rebuilding the list with `Clear()` caused
  Avalonia's `ListBox` to post a *deferred* `SelectedItem = null` callback that
  landed outside the change-suppression window, wiping the active filter. The
  list is now diff-updated in place (items are added/removed individually and the
  selected item is never removed unless it truly left the database), so the
  selection is preserved across background capture refreshes.

### Added
- **Mobile vault viewer** — a companion web app at `kapture.tools/vault` that lets
  you browse, search, and copy your vault from a phone or tablet. Connects to
  Google Drive (read-only), opens `vault.db` entirely in the browser via
  WebAssembly SQLite, and decrypts AES-256-GCM content locally with WebCrypto.
  Installable as a PWA. No data leaves the device.

---

## [1.0.1] — 2026-05-29

### Added
- **About dialog** — a new `?` button beside Settings opens an About window showing
  the logo, version number, and links to `kapture.tools` and the publisher site.
- **Save screenshots as images.** The **Save** button on a screenshot entry now
  exports a real image file (PNG / JPEG / BMP) via SkiaSharp, instead of writing
  the file path out as text.
- **Screenshot annotation editor.** Clicking **Edit / Annotate** on a screenshot
  opens a full editor with pen, rectangle, ellipse, arrow, text, and highlight
  tools; eight colour swatches; three stroke widths; undo and clear; and export to
  PNG or JPEG at the screenshot's native resolution.
- **Release automation.** Added `scripts/Invoke-Release.ps1` to build, package,
  version-bump, tag, and publish a release in one step.

### Fixed
- **Installer icon.** The setup executable showed the generic yellow Windows icon.
  The application icon (`app.ico`) was rebuilt with classic BMP frames so it embeds
  correctly into the installer and application executables.
- First pass at the sidebar filter selection issue (superseded by the complete fix
  in 1.0.2).

### Changed
- Wired up the `kapture.tools` custom domain: added a `CNAME` file and canonical
  URL / description meta tags to the marketing and legal pages.

---

## [1.0.0] — 2026-05-27

Initial public release of **KaptureVault** — the vault-only fork (keystroke,
clipboard, and screenshot capture plus settings).

### Added
- **KV brand icon everywhere** — taskbar, system tray, window title bars, all
  dialogs, and the installer.
- **Capture Admin Apps** setting — optionally relaunches KaptureVault elevated so
  its low-level keyboard hook can capture input from administrator-level
  applications (Task Manager, Registry Editor, etc.). Reverts safely if the UAC
  prompt is declined.
- **Interactive installer** — choose install location, Start Menu folder, optional
  desktop shortcut, and run-at-startup; branded wizard imagery throughout.
- **Uninstaller data removal** — the uninstaller now offers to permanently delete
  all vault data (captures, database, encryption keys, sync tokens) with a
  safe "No" default.
- **Terms of Service and Privacy Policy** pages for the Google OAuth consent screen.

### Fixed
- **Google Drive sync** silently failing — the OAuth `client_secret` was missing
  from the token exchange. Credentials are now loaded from `client_secret.json`
  and authentication errors are surfaced in the Settings UI.
- **Error 740 (`CreateProcess failed`)** on launch — dropped the
  `requireAdministrator` manifest level to `asInvoker` (the removed system-tweak
  features were the only thing that needed elevation) and switched startup from a
  scheduled task to the per-user `Run` registry key.

[1.0.7]: https://github.com/Vybecode-LTD/KaptureVault/releases/tag/v1.0.7
[1.0.6]: https://github.com/Vybecode-LTD/KaptureVault/releases/tag/v1.0.6
[1.0.5]: https://github.com/Vybecode-LTD/KaptureVault/releases/tag/v1.0.5
[1.0.4]: https://github.com/Vybecode-LTD/KaptureVault/releases/tag/v1.0.4
[1.0.3]: https://github.com/Vybecode-LTD/KaptureVault/releases/tag/v1.0.3
[1.0.2]: https://github.com/Vybecode-LTD/KaptureVault/releases/tag/v1.0.2
[1.0.1]: https://github.com/Vybecode-LTD/KaptureVault/releases/tag/v1.0.1
[1.0.0]: https://github.com/Vybecode-LTD/KaptureVault/releases/tag/v1.0.0
