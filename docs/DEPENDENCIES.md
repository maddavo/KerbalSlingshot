# Dependencies and licence review

Milestones 1 and 2 add no third-party numerical library or copied solver code. Conic propagation, event handling, coordinate search, planner plumbing, and independent RK4 checks are authored here. Locally inspected MechJeb conventions guide native API/frame integration; MechJeb is not a dependency.

| Dependency/reference | Version | Purpose | Distribution/review status |
| --- | --- | --- | --- |
| Microsoft .NET SDK | Tested with 8.0.425 | Build tools and offline harness | External toolchain; not shipped |
| Microsoft.NETFramework.ReferenceAssemblies | 1.0.3 | Build-time .NET Framework references | Development-only NuGet package, `PrivateAssets=all` |
| Microsoft.NETFramework.ReferenceAssemblies.net48 | 1.0.3, transitive | Framework 4.8 reference assemblies | Development-only; not copied into mod output |
| .NET 8 framework libraries | Harness target | JSON fixture/report handling | SDK/shared framework; no separate numerical package |
| KSP Assembly-CSharp / firstpass | Local KSP 1.12.5 | Live plugin compile references | Proprietary game files; local references only, `Private=false` |
| UnityEngine.CoreModule | Local KSP runtime | KSP type references | Local reference only, `Private=false` |
| UnityEngine.IMGUIModule | Local KSP runtime | In-game GUI controls | Local reference only, `Private=false` |
| UnityEngine.AnimationModule / TextRenderingModule | Local KSP runtime | Stock toolbar type hierarchy and explicit GUI text styles | Local compile references only, `Private=false`; no Unity files packaged |
| System.Reflection.Metadata / PEReader | .NET 8 SDK/shared framework | Offline startup/reference metadata inspection | Harness-only framework libraries; not packaged with plugin |
| MechJeb source | Local commit `aeee32212801b987e0fffcad800e0cb565584abb` | Design/API research | No runtime/build dependency; no solver implementation copied |
| ILSpy command-line | 9.1.0.7988; package declares MIT | Local installed-KSP API inspection | Ignored artifacts/tools only; not a runtime dependency or packaged file |

0.3.0's zero-revolution Lambert implementation is authored here from universal-variable equations, not imported from MechJeb/ALGLIB or another solver. The KSP native-frame/API behaviour was inspected locally; proprietary decompiler output stays in ignored artifacts/api and is not committed or distributed.

The two NuGet packages' installed `.nuspec` files identify Microsoft, mark them as development dependencies, and point to [Microsoft's licence](https://github.com/Microsoft/dotnet/blob/master/LICENSE). This records the declared licence location; review its terms before redistribution. Do not assume all .NET/game/Unity reference binaries share one licence.

The project's own licence remains undecided. The local test package contains authored DLLs/documents only. The toolbar icon and opaque panel texture are authored procedurally in code, with no external art dependency. No public release is published and no KSP/Unity/reference assemblies are distributed. Review and record licence choices before importing a Lambert/optimisation library or publishing a mod package.
