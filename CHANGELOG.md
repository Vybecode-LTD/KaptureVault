# Changelog

All notable changes to **KaptureVault** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[1.0.2]: https://github.com/Vybecode-LTD/KaptureVault/releases/tag/v1.0.2
[1.0.1]: https://github.com/Vybecode-LTD/KaptureVault/releases/tag/v1.0.1
[1.0.0]: https://github.com/Vybecode-LTD/KaptureVault/releases/tag/v1.0.0
