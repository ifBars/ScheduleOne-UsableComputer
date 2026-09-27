# Build and check local packages

Use PowerShell 7 and the configured game/S1API references in `local.build.props`:

```powershell
pwsh -NoProfile -File scripts/Build-Packages.ps1 -S1ApiSourceRoot C:\path\to\S1API-hosting-preview -Label local-candidate
```

The script rebuilds S1API and UsableComputer sequentially for Mono and IL2CPP with automatic deployment disabled. `S1ApiSourceRoot` must contain the external-hosting contract, and `local.build.props` must point to that checkout's two S1API outputs. Output defaults to a new folder under the system temporary directory; `-OutputRoot` can select another location outside this repository. Existing output is never overwritten. These commands do not publish a release.

The manifest records `targetGameVersion` (default `0.4.7f6`). Supply `-GameVersion` when intentionally targeting another build; the value describes the intended target, while live runtime tests establish compatibility.

Each runtime ZIP includes UsableComputer, the matching S1API hosting-preview DLL, three companion libraries, installation instructions, licenses/notices, and a manifest with file sizes and SHA-256 hashes. MelonLoader, Unity/game assemblies, local path configuration, saves, IWADs, and smoke artifacts are excluded. Two source-only ZIPs capture UsableComputer and the modified S1API, including current tracked and untracked source changes. The UsableComputer archive includes developer scripts, text documentation, and vendored source/licenses; documentation screenshots are omitted. Runtime manifests name and hash both source ZIPs and record both Git revisions and dirty states. Packaging fails if selected source files change during the build. Keep all four archives together when distributing a candidate. The S1API preview is not an official release and is required until its hosting API is published upstream.

Hashes detect accidental corruption and mismatches; they do not authenticate a publisher. Before publication, review the source snapshot, dependencies, outstanding release checklist, and applicable notices. A successful package build does not establish release readiness or bit-for-bit build reproducibility.

Run the archive checks with an actual output path:

```powershell
pwsh -NoProfile -File tests/Test-RuntimePackage.ps1 -PackagePath C:\Packages\UsableComputer-local-candidate-Mono.zip -Runtime Mono
```

`Run-VfsSmoke.ps1` accepts `-PackagePath` alongside its usual runtime, game path, disposable source save, and scenario flags. Package mode verifies and extracts the archive into the evidence directory, then installs the archived mod and companion DLLs into the isolated test installation. It builds only the smoke harness. The timeline records the archive hash. The normal mod/library/preference backups and restoration still apply.

Test both runtime ZIPs from their actual archived contents. Exercise player workflows such as `-Reports`, `-LuaStorage`, and `-Noodle` in addition to the default save/reload check. Nested-game and Doom workflows need their own runtime checks and are not proved by loading their companion libraries.
