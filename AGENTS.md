# Agent Notes

Read `C:\Users\Shuhari\.codex\memories\user-preferences.md` for stable cross-project collaboration preferences.

Project profile: `docs/agents/project-profile.md`.

This repository no longer uses the old Matt workflow / PRD / issue-gate process by default. For ordinary code, tests, documentation, packaging, and product-behavior changes, inspect the relevant code and implement directly unless the user asks for planning, issue creation, or a separate review flow.

For pure explanation, answer directly.

For product requirements, architecture choices, Windows notification behavior, app identity matching, audio playback behavior, packaging/startup behavior, or privacy/logging behavior, still name the risk plainly and verify local evidence before making durable changes.

Runtime boundary: the product must run directly on Windows 11. WSL may be used for repository editing and GitHub operations only; it must not be required to run the helper.

## Agent Skills Compatibility

### Issue tracker

GitHub Issues may be used when the user asks for issue tracking. They are not required before code changes.

### Triage labels

Use triage labels only when the user explicitly asks to manage GitHub Issues.

### Domain docs

This is a single-context repo using root `CONTEXT.md` and root `docs/adr/` when those files exist. See `docs/agents/domain.md`.
