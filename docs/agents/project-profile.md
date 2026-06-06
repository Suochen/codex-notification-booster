# Project Profile

This profile keeps project-specific facts for this repository.

## Project

- Name: Codex Notification Booster
- Owner: Suochen
- Primary contact: repository maintainer
- Profile location: `docs/agents/project-profile.md`
- Last verified: 2026-05-27 by Codex agent

## Repositories

| Purpose | Repository | Default branch | Local path | Notes |
| --- | --- | --- | --- | --- |
| Primary application | `Suochen/codex-notification-booster` | `main` | `/home/shuhari/code/codex-notification-booster` | Windows 11 helper source. WSL is the editing workspace, not the target runtime. |
## Issue Tracker

- System: GitHub Issues, only when requested by the user.
- Project or repository: `Suochen/codex-notification-booster`
- Code/system-behavior changes do not require a PRD, issue, or workflow gate by default.

## Worktree

- Default working path: `/home/shuhari/code/codex-notification-booster`
- Branch naming: `codex/<short-purpose>`
- Parallel worktree path: not established yet
- One-owner files or directories: none established yet
- Cleanup expectations: do not remove user-created files or unrelated GitHub issues

## DB And Runtime

- Runtime stack: Windows 11 desktop helper; initial probe may use Windows PowerShell, final helper expected to be Windows-native
- Required language/tool versions: Windows 11 with notification listener APIs; .NET SDK still needs installation before C# builds can run locally
- Local services: none
- Ports: none
- Credentials source: GitHub CLI authenticated as `Suochen` for issue management
- Startup command: Windows-side command only; do not require WSL to run the helper
- Stop/restart command: stop the Windows process or close the helper
- Seed or fixture data: captured notification metadata JSONL records
- Runtime deviations from production: WSL may edit files and call Windows commands, but product runtime must work from Windows without WSL

## Test Fast Paths

| Scope | Command | When to use | Notes |
| --- | --- | --- | --- |
| Windows notification API smoke | Windows PowerShell command that loads `Windows.UI.Notifications.Management.UserNotificationListener` | When checking API availability and listener permission | Must run on Windows, not inside Linux-only PowerShell. |
| Product build | `dotnet build CodexNotificationBooster.sln` | Before committing code changes when .NET SDK is available | Run from Windows PowerShell. |
| Product tests | `dotnet test tests\CodexNotificationBooster.Core.Tests\CodexNotificationBooster.Core.Tests.csproj` | Before committing core behavior changes when .NET SDK is available | Run from Windows PowerShell. |

## Domain Risk Domains

| Domain | Why it is risky | Required checks | Escalation trigger |
| --- | --- | --- | --- |
| Windows notification access | Requires user permission and OS API availability | Verify access status and document remediation | Access denied or API unavailable |
| App identity matching | False positives could boost unrelated notifications | Capture Codex and non-Codex metadata fixtures | Codex identity is ambiguous or shared |
| Audio behavior | Must not change Codex app volume or global notification volume | Review for playback-only implementation | Any mixer/global volume mutation |
| Startup/background behavior | Background helpers can annoy users or persist unexpectedly | Document startup, stop, and uninstall behavior | Auto-start or tray behavior added |
| Diagnostics | Logs may contain notification content | Store locally and document sensitivity | Logs leave the machine or include secrets unexpectedly |

## Notes

- The first prototype goal is metadata discovery, not custom sound playback.
- The product must be runnable directly on Windows 11. WSL-specific commands are acceptable for repository maintenance only.
