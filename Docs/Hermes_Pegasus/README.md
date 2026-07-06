# TinySpire Docs · Hermes/Pegasus Handoff

Windows-local handoff copy for local agents.

## Start here

1. `AGENT_HANDOFF.md` — compact project context for agents
2. `STATUS.md` — current checklist/status board
3. `SYNC_PROTOCOL.md` — sync rules between Pegasus and local agents
4. `index.md` — project wiki index
5. `art/art-style.md` — art direction and AI asset pipeline
6. `architecture.md` — Unity/C# architecture baseline
7. `design/decisions.md` — gameplay decision log

## Role of this folder

- This folder is the Windows-readable working copy for local agents.
- Pegasus' long-term wiki source is in WSL: `~/.hermes/hermes-wiki/03_projects/card-game/`.
- If local agents change docs here, use git diff/commit so Pegasus can inspect and merge back.
