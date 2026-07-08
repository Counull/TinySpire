# Third-Party Notices

TinySpire uses the following third-party open-source components.
All are referenced declaratively via package managers unless noted as vendored.

| Component | Version | Source | License | Integration |
|---|---|---|---|---|
| Luban | 4.10.1 | <https://github.com/focus-creative-games/luban> | MIT | Vendored binaries at `Tools/Luban/` (see its `LICENSE` / `NOTICE.md`) |
| R3 | 1.3.1 | <https://github.com/Cysharp/R3> | MIT | NuGet (`TinySpire/Assets/packages.config`) |
| R3.Unity | git | <https://github.com/Cysharp/R3> | MIT | UPM git dependency (`TinySpire/Packages/manifest.json`) |
| UniTask | git | <https://github.com/Cysharp/UniTask> | MIT | UPM git dependency |
| VContainer | git | <https://github.com/hadashiA/VContainer> | MIT | UPM git dependency |
| NuGetForUnity | git | <https://github.com/GlitchEnzo/NuGetForUnity> | MIT | UPM git dependency (editor tooling) |
| Microsoft.Bcl.TimeProvider | 8.0.0 | <https://github.com/dotnet/runtime> | MIT | NuGet (transitive, R3) |
| System.ComponentModel.Annotations | 5.0.0 | <https://github.com/dotnet/runtime> | MIT | NuGet (transitive, R3) |
| System.Threading.Channels | 8.0.0 | <https://github.com/dotnet/runtime> | MIT | NuGet (transitive, R3) |

## MIT License obligations

All components above are MIT-licensed. Redistribution of copies or substantial
portions must retain the original copyright notice and license text. License
texts are distributed with each package by its package manager; for the
vendored Luban distribution, see `Tools/Luban/LICENSE`.

## Maintenance rule

When adding a new third-party dependency:

1. Prefer declarative references (UPM / NuGet) over vendoring.
2. If vendoring, keep the upstream `LICENSE` and add a `NOTICE.md`
   (source URL, version, license, retrieval date, modifications).
3. Add a row to the table above.
