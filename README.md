# TinySpire

<p align="center">
  <img src="TinySpire/Assets/Arts/Loading/tinyspire-calliope-cover-paper-playwright-pencil-hand-fixed.png" alt="TinySpire - Calliope Cover" width="800"/>
</p>

TinySpire is a Unity/C# card game project in early development.

The BattleScene MVP milestone is complete. The project now has a validated standalone combat loop covering card play, effects, battle state, enemy actions, outcomes, and runtime feedback.

Development is now transitioning to the Run phase. The next planning target is the Run lifecycle and cross-scene progression; implementation will proceed through separately reviewed slices rather than one monolithic G1 plan.

The current BattleScene UI, visual feedback, and animation are functional but provisional. They may change during later presentation and polish work while development prioritizes the game’s core Run systems.

## Current Status

- BattleScene MVP (M0–M10): complete
- Milestone tag: `milestone-battlescene-mvp-2026-08-14`
- Next phase: Run roadmap and G1 slice definition
- Latest recorded native Unity validation (2026-08-14): 807/807 EditMode tests passed

## Repository Layout

```text
TinySpire/   Unity project
Docs/        Project documentation and AI collaboration notes
DataTables/  Table/config sources and Luban generation
Tools/       Local tooling, if present
```

## Documentation

Start here:

```text
Docs/COLLABORATION_SOURCE_OF_TRUTH.md
Docs/AI_COLLABORATION_RULES.md
Docs/Hermes_Pegasus/design/project-definition.md
```

Current phase planning and validation:

- [Run MVP roadmap](Docs/Copilot_Daedalus/RUN_ROADMAP.md)
- [Testing and acceptance index](Docs/Copilot_Daedalus/06_testing/README.md)

External workflow reference is included as a submodule:

```text
Docs/_external/llm-workflow
```

If the submodule is missing after clone, run:

```bash
git submodule update --init --recursive
```

## Development

Open the Unity project from:

```text
TinySpire/
```

This repository is still in an early iteration stage; prefer small, reviewable changes.
