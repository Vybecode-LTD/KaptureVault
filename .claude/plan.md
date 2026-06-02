# KaptureVault — Plan & Context Pointer

> **Rewritten 2026-06-01.** The previous version (dated 2026-05-19) described the **pre-fork** full
> "Kapture" power tool — System Tweaks, Services Browser, Dashboard, Profiles, Startup Analyzer,
> Scheduler, Privacy Dashboard, v1.0.27, `requireAdministrator`. **None of that exists in this repo.**
> KaptureVault is the **vault-only fork**. Trust the canonical docs below, not any stale plan.

## Canonical memory (read in this order)
1. **`CLAUDE.md`** (repo root) — constitution, architecture, stack, standing directives, Lessons, full session log.
2. **`docs/HANDOFF.md`** — the canary: current state + next steps. Read first every session.
3. **`docs/ROADMAP.md`** — all to-dos: the F-02 feature roadmap + the audit-remediation P1/P2/P3 backlog, with status and the Phase-3 slice tracker.
4. **`docs/BUGS.md`** · **`docs/TESTING.md`** · **`docs/AUDIT-LOG.md`** — issue register (KV-001…045), test inventory, audit/reconciliation history.
5. Design refs (non-managed): **`docs/F-02-online-vault-design.md`** (§ Revision 2 = the product/tier model), **`docs/F-02-PHASE-3-DESIGN.md`** (the current work + slice tracker), **`docs/F-02-PROVISIONING.md`** (Cloudflare/Google/Stripe go-live runbook).

All managed docs share one `version` (currently **1.15.0**) with YAML frontmatter.

## What KaptureVault actually is
Vault-only fork: captures keystrokes / clipboard / screenshots → a local SQLite vault with search,
tags, pinning, auto-expiry, optional AES-256-GCM encryption, optional Google Drive sync, a global
Quick-Paste hotkey, and a screenshot annotation editor. **No** system-tweak suite; runs `asInvoker`
(not admin). .NET 9 / C# 13 / Avalonia 11.3.12. Repo `C:\DEV\Utilities\KaptureVault` (public). The
paid **Online Vault backend (F-02)** is a separate **private** repo `kapturevault-backend`
(`C:\dev\kapturevault-backend`) — a Cloudflare Worker (R2 + D1 + Stripe).

## Current state (2026-06-02) — see `docs/HANDOFF.md` for live detail
- **Shipped release: v1.0.7.** Client tests **162**, backend **59**.
- **All P0 + P1 audit issues resolved.** Remaining tech debt = the **P2/P3 backlog** in ROADMAP.
- **Current initiative — F-02 "Online Vault"** (free encrypted cloud sync; paid file hosting + share links):
  - Engine + Phases 0–2 done and **deployed LIVE** (`kapturevault-backend.kapture.workers.dev`); vault sync is **free**, quota-enforced (250 MB free / 10 GB paid); smoke-verified end-to-end.
  - **Phase 3 (client vault-sync v2) ✅ COMPLETE (2026-06-02, slices A–H)** — screenshots now sync to the Online Vault, end-to-end encrypted + quota-aware: **F** (`a00ee25`) client pipeline, **G** (`5cc03e6`) restore, **H** docs. ⚠️ **F/G/H client commits are LOCAL, not pushed.** **Next: a live end-to-end smoke → cut v1.0.8**, or the P2 backlog (incl. T-35). Tracker: `docs/F-02-PHASE-3-DESIGN.md` § 11.

## Working agreements (full versions in `CLAUDE.md` → STANDING DIRECTIVES)
Test-first (RED→GREEN; each fix its own commit; run the evidence ledger before declaring "done"); update
docs at the point of change; every session ends with a handoff; clean, recoverable git history and
**never commit a secret**; the desktop release pipeline is local-build → CI is the single release creator
→ the `kapture.tools` website reads live from GitHub.
