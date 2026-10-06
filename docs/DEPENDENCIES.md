# Dependencies and licence review

Milestone 1 adds no third-party numerical library or copied solver code. Conic propagation, event handling, coordinate search, and the independent RK4 harness are authored in this repository.

| Dependency/reference | Version | Purpose | Distribution/review status |
| --- | --- | --- | --- |
| Microsoft .NET SDK | Tested with 8.0.425 | Build tools and offline harness | External toolchain; not shipped |
| Microsoft.NETFramework.ReferenceAssemblies | 1.0.3 | Build-time .NET Framework references | Development-only NuGet package, `PrivateAssets=all` |
| Microsoft.NETFramework.ReferenceAssemblies.net48 | 1.0.3, transitive | Framework 4.8 reference assemblies | Development-only; not copied into mod output |
| .NET 8 framework libraries | Harness target | JSON fixture/report handling | SDK/shared framework; no separate numerical package |
| KSP Assembly-CSharp / firstpass | Local KSP 1.12.5 | Compile-time API contract | Proprietary game files; local references only, `Private=false` |
| UnityEngine.CoreModule | Local KSP runtime | KSP type references | Local reference only, `Private=false` |
| MechJeb source | Local commit `aeee32212801b987e0fffcad800e0cb565584abb` | Design/API research | No runtime/build dependency; no solver implementation copied |

The two NuGet packages' installed `.nuspec` files identify Microsoft, mark them as development dependencies, and point to [Microsoft's licence](https://github.com/Microsoft/dotnet/blob/master/LICENSE). This records the declared licence location; review its terms before redistribution. Do not assume all .NET/game/Unity reference binaries share one licence.

The project's own licence remains undecided. This milestone creates no release or redistribution package. Review and record licence choices before importing any Lambert/optimisation library or publishing a mod package.
