# TinySpire Docs · Hermes/Pegasus Handoff

Windows-local handoff copy for local agents.

## Start here

1. `AGENT_PROFILE.md` — Pegasus identity and responsibility boundary
2. `AGENT_HANDOFF.md` — compact project context for agents
3. `STATUS.md` — current checklist/status board
4. `SYNC_PROTOCOL.md` — sync rules between Pegasus and local agents
5. `index.md` — project wiki index
6. `design/project-definition.md` — current project/game definition and MVP scope
7. `design/decision-locks.md` — locked/provisional/deferred/open decisions
8. `design/decisions.md` — gameplay decision log
9. `art/art-style.md` — TinySpire 美术风格与 ComfyUI/Z-Image 资源管线
10. `architecture.md` — Unity/C# architecture baseline

## Role of this folder

- This folder is the Windows-readable working copy for local agents.
- Pegasus' long-term wiki source is in WSL: `~/.hermes/hermes-wiki/03_projects/card-game/`.
- If local agents change docs here, use git diff/commit so Pegasus can inspect and merge back.
