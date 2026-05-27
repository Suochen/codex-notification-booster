# Triage Labels

The skills speak in terms of five canonical triage roles. This file maps those roles to the actual label strings used in this repo's issue tracker.

| Label in mattpocock/skills | Label in our tracker | Meaning                                  |
| -------------------------- | -------------------- | ---------------------------------------- |
| `needs-triage`             | `needs-triage`       | Maintainer needs to evaluate this issue  |
| `needs-info`               | `needs-info`         | Waiting on reporter for more information |
| `ready-for-agent`          | `ready-for-agent`    | Fully specified, ready for an AFK agent  |
| `ready-for-human`          | `ready-for-human`    | Requires human implementation            |
| `wontfix`                  | `wontfix`            | Will not be actioned                     |

When a skill mentions a role, such as "apply the AFK-ready triage label", use the corresponding label string from this table.

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
| `lightweight` | Classification: PRD was explicitly skipped for a small bounded issue. |
| `verification` | Classification only; verification is not used for automatic routing yet. |
| `prd` | Discovery/type label for parent PRD planning issues. |

Only the controller changes labels. Workers should comment with suggested state and evidence.
