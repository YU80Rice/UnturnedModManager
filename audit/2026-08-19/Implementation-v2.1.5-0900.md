# Implementation Report - v2.1.5

## Requirement Summary

Support the new `unmod.online` community download route: when a plugin entry declares `github_repo`, UMM must fetch the declared repository's GitHub `releases/latest` package rather than relying solely on the community-hosted package. The existing community package endpoint remains a controlled fallback for temporary GitHub failures.

## Traceability Matrix

| Requirement | Implementation |
| --- | --- |
| Read the community GitHub repository field | `Models/CommunityModels.cs` maps `github_repo` to `GitHubRepository`. |
| Fetch a repository's latest Release | `Services/CommunityApiClient.cs` validates `owner/repo`, requests GitHub's `releases/latest`, and accepts exactly one ZIP asset. |
| Prevent cross-repository or malformed asset downloads | `CommunityApiClient` requires HTTPS `github.com/{owner}/{repo}/releases/download/...` asset URLs and rejects invalid repository names, missing/multiple ZIP assets, malformed JSON, and malformed digests. |
| Verify package integrity | GitHub asset size is checked; an available SHA-256 digest is format-validated and compared with a fixed-time byte comparison. Integrity failures never fall back. |
| Preserve community fallback | HTTP/network/timeout failures on GitHub fall back to `unmod.online/api/mods/{id}/file`; structural and integrity errors remain visible failures. |
| Keep installed version truthful | `DownloadedMod.SourceVersion` records the actual downloaded source version. `CommunityModInstaller` writes that value into the install manifest, preventing a fallback community package from being labeled as the GitHub Release. |
| Keep UI update state truthful | `EffectiveVersion`/`EffectiveFileSize` represent a resolved GitHub Release while original community metadata remains intact. Detail and local-plugin views use the effective remote version for update comparisons. |
| Expose the source to users | `CommunityDetailViewModel` and `Pages/CommunityDetailPage.xaml` display the active source and resolved GitHub Release version. |
| Version and documentation | `UnturnedModManager.csproj`, `AppSettings.cs`, `README.md`, `CHANGELOG.md`, and `Pages/AboutPage.xaml` are updated to v2.1.5. |

## Files Changed

- `Models/CommunityModels.cs`
- `Services/CommunityApiClient.cs`
- `Services/CommunityModInstaller.cs`
- `ViewModels/CommunityDetailViewModel.cs`
- `ViewModels/LocalModsViewModel.cs`
- `Pages/CommunityDetailPage.xaml`
- `UnturnedModManager.Tests/ModelBehaviorTests.cs`
- `UnturnedModManager.csproj`
- `AppSettings.cs`
- `README.md`
- `CHANGELOG.md`
- `Pages/AboutPage.xaml`

## Verification

Commands run from the project root:

```powershell
dotnet build .\UnturnedModManager.csproj -c Release --no-restore
dotnet test .\UnturnedModManager.Tests\UnturnedModManager.Tests.csproj -c Release --no-restore
```

Result:

- Build: `0 warnings, 0 errors`.
- Tests: `38/38 passed`.
- Environment note: the installed .NET 10 preview SDK emitted informational `NETSDK1057`; the application target remains `net8.0-windows`.
- Diff validation: `git diff --check` passed.
- Live metadata sample: `unmod.online` Mod 13 declares `github_repo: YU80Rice/SteamP2PFriends`; its GitHub latest Release exposed exactly one ZIP asset and a SHA-256 digest, matching the implemented contract.

## Independent Audit Record

| Round | Result | Finding and resolution |
| --- | --- | --- |
| 1 | FAIL | GitHub tag could be written after a community fallback; invalid non-empty digest could skip validation. Metadata was separated from actual package version, and malformed digests now fail closed. |
| 2 | FAIL | Local plugin synchronization used the original community version instead of the resolved GitHub version. It now uses `remote.EffectiveVersion`; a scan-and-manifest regression test was added. |
| 3 | PASS | Independent review confirmed GitHub source validation, fallback boundaries, integrity handling, installation manifest correctness, local-list update state, and detail metadata consistency. |

## Deviations

None. The feature remains within the existing WPF/.NET 8 architecture and retains the established secure ZIP installation, ownership, rollback, and community-account behavior.

## Recommended Manual QA

1. Sign in to `unmod.online`, open a plugin with `github_repo`, and verify the detail page shows `GitHub latest Release` with the resolved tag and size.
2. Install the plugin and confirm the task center identifies the GitHub source; reopen the local-plugin page and confirm no update is shown for the same version.
3. Temporarily block GitHub access while authenticated, install again, and confirm the task center reports the community fallback. Confirm the local list continues to offer the newer GitHub Release when its version differs.
4. Test an entry with a malformed digest, an external asset URL, or multiple ZIP assets in a controlled test environment; the operation must fail without writing files or silently falling back.
