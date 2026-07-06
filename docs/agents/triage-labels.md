# Optional GitHub Labels

Use these labels only when the user explicitly asks to manage GitHub Issues.

| Label | Meaning |
| --- | --- |
| `needs-triage` | Maintainer needs to evaluate this issue. |
| `needs-info` | Waiting on reporter for more information. |
| `ready-for-agent` | Fully specified, ready for an agent. |
| `ready-for-human` | Requires human implementation. |
| `wontfix` | Will not be actioned. |

## Local Execution Labels

These labels are not Matt-native triage states. They describe local progress after triage has made an issue actionable.

| Label | Meaning |
| --- | --- |
| `agent-claimed` | An agent is actively working the issue. |
| `ready-for-review` | Worker output is complete enough for controller review. |
| `blocked` | Progress is blocked by review, dependency, missing evidence, or external decision. |
| `done` | Controller accepted the result and closed the loop. |

## Type / Classification Labels

| Label | Meaning |
| --- | --- |
| `afk` | Classification: expected to be agent-independent after approval. |
| `hitl` | Classification: human-in-the-loop decision or review is expected. |
| `lightweight` | Classification: small bounded issue. |
| `verification` | Classification only; verification is not used for automatic routing yet. |
| `prd` | Planning issue label, only when explicitly requested. |
