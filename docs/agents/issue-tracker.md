# Optional Issue Tracker: GitHub

Use GitHub Issues only when the user explicitly asks to create, update, or inspect issues.

## Repository

- Owner: `Suochen`
- Repo: `codex-notification-booster`
- Remote: `https://github.com/Suochen/codex-notification-booster.git`

## Conventions

- **Create an issue**: `gh issue create --title "..." --body "..."`. Use a heredoc for multi-line bodies.
- **Read an issue**: `gh issue view <number> --comments`, filtering comments by `jq` and also fetching labels.
- **List issues**: `gh issue list --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'` with appropriate `--label` and `--state` filters.
- **Comment on an issue**: `gh issue comment <number> --body "..."`
- **Apply / remove labels**: `gh issue edit <number> --add-label "..."` / `--remove-label "..."`
- **Close**: `gh issue close <number> --comment "..."`

Infer the repo from `git remote -v` when possible; `gh` does this automatically when run inside a clone.

## Optional Publishing

Create a GitHub issue.

## Optional Fetch

Run `gh issue view <number> --comments`.
