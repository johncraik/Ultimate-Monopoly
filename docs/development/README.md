# Development Docs

The working record behind `MP.GameEngine` and the wider app — its **design docs** and
**session notes**. Both are written by AI (Claude) during discussions with the developer, and
serve as a development aid.

> ### How this project is built
> The app is **not** purely AI-generated. AI is used as a **tool and assistant** — for doc
> generation, coding, reviews, second opinions, bug fixes, and so on. The **developer is the main
> driver**, and the **final decision on every change rests with them**. These docs are the artefact
> of that collaboration, not an autonomous output.

---

## `design-docs/`

The agreed design of the system, captured during developer–AI discussions. **Intended to be
authoritative** — but they can drift slightly from the code over time, so where a doc and the code
(or the developer's stated intent) disagree, the code and the developer win.

Organised by concern:

| Folder | Scope |
|---|---|
| `cards/` | The **cards sub-system / mini-engine** inside `MP.GameEngine`. |
| `game-design/` | The design of **`MP.GameEngine` itself** — rules, turn loop, economy, stats, deals, etc. |
| `frameworks/` | **`MP.GameEngine` cross-cutting architecture** — turn-state, choice/prompt events, event receipts, etc. |
| *(root)* | **Anything else** — e.g. web orchestration, SignalR design. |

---

## `session-notes/`

Per-session **context dumps** written by the AI at the end of a working session. Each records
everything the AI knew at that point — what had been done, the decisions made and the reasoning
behind them, and the open threads — so the **next session starts with a clear picture** instead of
re-deriving context from scratch.

They are a development aid and a record of the collaboration, **not** an authoritative spec: when a
session note disagrees with the design docs or the code, the **design docs and code win**. Read
together, they also illustrate the working model above — AI assisting across docs, code, reviews,
and fixes, with the developer making the calls.