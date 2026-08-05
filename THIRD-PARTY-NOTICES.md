# Third-Party Notices

## Scope and ownership

TinySpire uses third-party software, tools, templates, and Unity packages. Each
component remains governed by its own license or terms. No TinySpire copyright
statement or repository policy overrides those third-party terms, and this file
does not sublicense any third-party component.

Versions below come from the tracked source of truth for each integration:
vendored files, `Tools/Luban/Luban.deps.json`,
`TinySpire/Packages/packages-lock.json`, or
`TinySpire/Assets/packages.config`.

## Components distributed with this repository

| Component | Version / snapshot | Source | License | Integration and retained notice |
|---|---|---|---|---|
| DOTween Free and its accompanying DemiLib files | 1.3.030 | <https://github.com/Demigiant/dotween> | [DOTween license](https://dotween.demigiant.com/license.php) | Unmodified standard-version files under `TinySpire/Assets/Plugins/Demigiant/DOTween/` and `DemiLib/`; retain Demigiant copyright, disclaimers, and `DOTween/readme.txt`. This is not MIT. |
| Luban CLI | 4.10.1 | <https://github.com/focus-creative-games/luban> | MIT | Unmodified binary release under `Tools/Luban/`; retain `Tools/Luban/LICENSE`, `Tools/Luban/NOTICE.md`, and the bundled-dependency notices below. |
| Unity `.gitattributes` template | 2026-02-04 snapshot | <https://github.com/gitattributes/gitattributes/blob/master/Unity.gitattributes> | MIT | Adapted in `.gitattributes`; upstream source and license remain in the file header. |
| GitHub Unity `.gitignore` template | 2025-12-18 snapshot | <https://github.com/github/gitignore/blob/main/Unity.gitignore> | CC0-1.0 | Adapted in `TinySpire/.gitignore`; upstream source and dedication remain in the file header. |

DOTween Free may be redistributed only under its own license conditions. In
particular, a redistributed standard-version source copy must keep the original
copyright notices, disclaimers, and original readme. Modified versions are not
covered by the standard-version redistribution permission.

### Luban CLI bundled dependencies

The vendored Luban binary release includes these runtime assemblies. The list
and versions are taken from `Tools/Luban/Luban.deps.json`; they are not covered
by Luban's own MIT license.

| Component | Version | Upstream / package | License |
|---|---:|---|---|
| CommandLineParser | 2.9.1 | <https://www.nuget.org/packages/CommandLineParser/2.9.1> | MIT |
| ExcelDataReader | 3.7.0 | <https://www.nuget.org/packages/ExcelDataReader/3.7.0> | MIT |
| Google.Protobuf | 3.29.0 | <https://www.nuget.org/packages/Google.Protobuf/3.29.0> | BSD-3-Clause |
| MessagePack / MessagePack.Annotations | 3.1.7 | <https://www.nuget.org/packages/MessagePack/3.1.7> | MIT |
| Microsoft.NET.StringTools | 17.11.4 | <https://www.nuget.org/packages/Microsoft.NET.StringTools/17.11.4> | MIT |
| NeoLua | 1.3.14 | <https://github.com/neolithos/neolua> | Apache-2.0 |
| Newtonsoft.Json | 13.0.1 | <https://www.nuget.org/packages/Newtonsoft.Json/13.0.1> | MIT |
| Newtonsoft.Json.Bson | 1.0.3 | <https://www.nuget.org/packages/Newtonsoft.Json.Bson/1.0.3> | MIT |
| NLog | 5.3.4 | <https://www.nuget.org/packages/NLog/5.3.4> | BSD-3-Clause |
| Scriban | 7.2.1 | <https://www.nuget.org/packages/Scriban/7.2.1> | BSD-2-Clause |
| Ude.NetStandard | 1.2.0 | <https://github.com/yinyue200/ude> | MPL-1.1 OR GPL-2.0-or-later OR LGPL-2.1-or-later |
| YamlDotNet.NetCore | 1.0.0 | <https://www.nuget.org/packages/YamlDotNet.NetCore/1.0.0> | MIT according to upstream YamlDotNet; this legacy package omits license metadata |

The current upstream Luban release snapshot did not place each bundled
dependency's full license text beside its DLL. This inventory must therefore be
kept with the binary distribution, and a future Luban refresh must also retain
the corresponding upstream copyright and license texts. Do not describe the
contents of `Tools/Luban/` as being wholly MIT-licensed.

## Package-managed dependencies

The following packages are declared in Git, but their restored package contents
are not vendored as repository blobs.

### UPM Git dependencies

| Component | Locked revision | Source | License |
|---|---|---|---|
| HybridCLR 8.12.0 | `e4def761d69b` | <https://github.com/focus-creative-games/hybridclr_unity> | MIT |
| Luban.Unity 1.2.0 | `061314200e2f` | <https://github.com/focus-creative-games/luban_unity> | MIT |
| MCP for Unity | `4ce7dd3cc54e` | <https://github.com/CoplayDev/unity-mcp> | MIT |
| R3.Unity | `3fed50ae5c7e` | <https://github.com/Cysharp/R3> | MIT |
| UniTask | `e5acc106ee19` | <https://github.com/Cysharp/UniTask> | MIT |
| NuGetForUnity | `acc1c7bc9ea3` | <https://github.com/GlitchEnzo/NuGetForUnity> | MIT |
| VContainer | `5401e5a7ebc4` | <https://github.com/hadashiA/VContainer> | MIT |

### NuGet dependencies

`TinySpire/Assets/packages.config` is tracked, while restored packages under
`TinySpire/Assets/Packages/` are ignored.

| Component | Version | Source | License |
|---|---:|---|---|
| R3 | 1.3.1 | <https://github.com/Cysharp/R3> | MIT |
| Microsoft.Bcl.TimeProvider | 8.0.0 | <https://github.com/dotnet/runtime> | MIT |
| System.ComponentModel.Annotations | 5.0.0 | <https://github.com/dotnet/runtime> | MIT |
| System.Threading.Channels | 8.0.0 | <https://github.com/dotnet/runtime> | MIT |

MIT entries require retention of their original copyright notice and full MIT
license text when copies or substantial portions are redistributed. Other
entries must be handled under their named licenses.

## Unity Editor and Unity packages

The Unity Editor is not distributed by this repository. Unity Package Manager
restores the packages declared by `TinySpire/Packages/manifest.json`; exact
resolved versions and transitive dependencies are recorded in
`TinySpire/Packages/packages-lock.json`. `Library/PackageCache/` is local and is
not tracked.

| Governing terms | Direct packages and resolved versions |
|---|---|
| [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license) | `com.unity.2d.animation` 15.1.0; `com.unity.2d.aseprite` 5.0.3; `com.unity.2d.psdimporter` 14.0.3; `com.unity.2d.spriteshape` 15.0.3; `com.unity.2d.tilemap.extras` 8.0.3; `com.unity.addressables` 2.9.1; `com.unity.inputsystem` 1.19.0; `com.unity.localization` 1.5.12; `com.unity.render-pipelines.universal` 17.5.0; `com.unity.test-framework` 1.7.0; `com.unity.timeline` 1.8.12; `com.unity.ugui` 2.5.0 |
| [Unity Package Distribution License](https://unity.com/legal/licenses/unity-package-distribution-license) | `com.unity.2d.sprite` 1.0.0; `com.unity.2d.tilemap` 1.0.0; `com.unity.collab-proxy` 2.13.3; `com.unity.multiplayer.center` 1.0.1; `com.unity.visualscripting` 1.9.11 |
| [Unity Editor Software Terms](https://unity.com/legal/editor-terms-of-service/software) / package-specific terms | `com.unity.2d.tooling` 3.0.1; `com.unity.ai.inference` 2.6.1; `com.unity.feature.2d` 2.0.2 meta-package; all `com.unity.modules.*` built-ins |
| MIT | `com.unity.ide.rider` 3.0.40; `com.unity.ide.visualstudio` 2.0.27 |

The manifest currently requests URP 17.6.0, while the tracked lock file resolves
17.5.0; this notice records the resolved package actually pinned by the lock.
The Unity Package Distribution License must not be read as permission to vendor
those package sources into this repository; the packages remain UPM-resolved.

## Git submodule

`Docs/_external/llm-workflow` is a Git submodule pinned at `a0da6740efc6`.
It is MIT-licensed by Counull, and its full license is retained inside the
submodule as `LICENSE`. Clone with submodules enabled to obtain its contents.

## Local-only proprietary tools

DOTween Pro is proprietary software and is not distributed by this repository.
Licensed developers may install it locally under
`TinySpire/Assets/Plugins/Demigiant/`. All DOTween Pro files, examples,
metadata, and Pro readme files are excluded from version control. TinySpire does
not sublicense DOTween Pro, and a clean clone uses only the freely
redistributable DOTween standard version. A developer who does not use Pro does
not need to download it to build or run TinySpire.

## Asset provenance

Third-party visual, audio, font, or other content must have a recorded source,
license, and required attribution before it is committed. Existing project and
documentation images that have no third-party provenance record are not granted
a third-party license by this notice; if an external origin is identified, add
its exact terms here before redistribution.

## Maintenance rule

When adding or updating a third-party dependency:

1. Prefer declarative UPM or NuGet references over vendoring.
2. If vendoring, retain the upstream copyright and full license text, and add a
   `NOTICE.md` with source URL, exact version, retrieval date, and modifications.
3. Never commit paid or per-seat Asset Store package contents. Add precise ignore
   rules, and verify a clean clone does not require the local-only package.
4. Update this notice and the relevant lock/source-of-truth file in the same
   change. Do not claim a license from memory or infer that every transitive
   component shares its parent's license.
