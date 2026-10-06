# Prototype installation and rollback

Target: **KSP 1.12.5**, stock patched conics, no Principia. This is a private/local test package, not a published release. Assembly version: **0.2.1.0**. The package's `build-manifest.json` identifies the exact clean source commit and SHA-256 of every built DLL. `package-files.json` identifies the packaged copies. Revised UI/toolbar behaviour and trajectory agreement await the combined test.

## Install exactly two files

1. Exit KSP completely before changing its files. Extract the supplied ZIP to a temporary directory. Its installable payload is only:

   ```text
   GameData/KerbalSlingshot/Plugins/KerbalSlingshot.Core.dll
   GameData/KerbalSlingshot/Plugins/KerbalSlingshot.KSP.dll
   ```

2. For Dave's existing Steam installation, the destination directory is:

   ```text
   C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\GameData\KerbalSlingshot\Plugins
   ```

3. If either named DLL already exists there, copy it to a new backup directory **outside GameData**, for example `Documents\KerbalSlingshot-backups\<package-build-id>`. Record which files existed and their SHA-256. Back up any matching `.pdb` as well if present; the package does not include PDBs. Preserve all other files, configurations, diagnostics, and mod directories. Never keep an extra DLL copy inside GameData, where KSP could load both.
4. Create the destination `Plugins` directory if needed, then copy **only those two DLLs** from the extracted package into it. Do not install the harness, `net8.0` outputs, game/Unity assemblies, or source-build output directories.
5. Verify both installed SHA-256 values match the package manifest. For this installation, the exact PowerShell check is:

   ```powershell
   Get-FileHash -Algorithm SHA256 -LiteralPath 'C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\GameData\KerbalSlingshot\Plugins\KerbalSlingshot.Core.dll', 'C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\GameData\KerbalSlingshot\Plugins\KerbalSlingshot.KSP.dll'
   ```

6. Launch KSP using a copied/recoverable save for **UI-ACCEPTANCE.md** (and KSP-TEST.md if comparing trajectories). In flight/map view, the **stock toolbar planet/flyby icon** opens the planner; there is no floating Slingshot button. The short title shows version; Details/log/diagnostics retain exact revision. A successful build or matching installed hash does not establish revised UI behaviour or trajectory agreement.

## Roll back

Exit KSP. For each of the two named DLLs that existed before installation, restore its backed-up copy and verify its original hash. If a named DLL did not previously exist, remove only that newly installed DLL. Do not remove other files or the `Diagnostics` directory. Restore the recoverable test save if you manually edited its manoeuvre node during the test. No plugin action changes saves or nodes; **Write diagnostics** is the only explicit plugin file-writing control.

## Rebuild the test package

From a clean source checkout with the .NET 8 SDK:

```powershell
./package.ps1 -KspManagedPath 'C:/Program Files (x86)/Steam/steamapps/common/Kerbal Space Program/KSP_x64_Data/Managed'
```

This builds, runs offline checks, inspects the actual plugin startup/reference metadata, packages only the two authored DLLs, and verifies their bytes inside the ZIP. It writes under `artifacts/packages/`, preserves an existing package of the same build ID, and never installs or launches KSP.
