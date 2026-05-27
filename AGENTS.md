# Agent Notes

Read `C:\Users\Shuhari\.codex\memories\user-preferences.md` for stable cross-project collaboration preferences.

Use the standalone Matt AI Workflow pack at `Suochen/matt-ai-workflow`.

Project profile: `docs/agents/project-profile.md`.

This repository is currently in product discovery for a Windows 11 notification helper. Do not start implementation just because a prototype goal exists. Product behavior must first pass Matt Workflow Intake, Requirement Analysis, `grill-me`, PRD approval, and issue publication gates.

For pure explanation, answer directly and remind the user they can enter workflow if the answer should become a durable change.

For any task that changes code, tests, configuration, issue/PRD state, workflow files, branches, hooks/scripts, project profile, or system behavior:

1. Enter Workflow Intake.
2. Recommend a route.
3. Wait for user confirmation unless the user already authorized the route.
4. Ensure every code/system-behavior change has a GitHub issue.
5. Use Lightweight issue only when Parent PRD is none, the change is truly small, and explicit skip approval is recorded.

For product requirements, architecture choices, Windows notification behavior, app identity matching, audio playback behavior, packaging/startup behavior, or privacy/logging behavior:

1. Run Requirement Analysis first.
2. Use `grill-me` before PRD creation or implementation.
3. Ask one question at a time and include a recommended answer.
4. Inspect available repo/GitHub/workflow context instead of asking for facts already discoverable.
5. Record significant confirmations as GitHub Grill Log evidence before syncing them into a PRD.

Runtime boundary: the product must run directly on Windows 11. WSL may be used for repository editing and GitHub workflow operations only; it must not be required to run the helper.

## Agent Skills Compatibility

### Issue tracker

Issues and PRDs are tracked in GitHub Issues for `Suochen/codex-notification-booster`. See `docs/agents/issue-tracker.md`.

### Triage labels

Use the Matt workflow triage labels and local execution labels recorded in `docs/agents/triage-labels.md`.

### Domain docs

This is a single-context repo using root `CONTEXT.md` and root `docs/adr/` when those files exist. See `docs/agents/domain.md`.
