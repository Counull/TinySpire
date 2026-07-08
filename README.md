# TinySpire

TinySpire is a Unity/C# card game project in early development.

The current focus is building a small BattleScene vertical slice: playing cards, resolving effects, updating battle state, and showing feedback in UI.

## Repository Layout

```text
TinySpire/   Unity project
Docs/        Project documentation and AI collaboration notes
Config/      Table/config sources, if present
Tools/       Local tooling, if present
```

## Documentation

Start here:

```text
Docs/COLLABORATION_SOURCE_OF_TRUTH.md
Docs/AI_COLLABORATION_RULES.md
Docs/Hermes_Pegasus/design/project-definition.md
```

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
