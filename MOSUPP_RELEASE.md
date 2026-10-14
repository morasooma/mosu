# MosuPp 0.4.0 client release

Wide jumps mixed with bursts and changes of cursor speed/direction now receive an Arles bonus,
up to 30%, in the MosuPp RX calculator. The patch also includes the polygon's AR/OD lock,
visible-notes reading, aim control, RP balance, spike/filler guard and revised stream-map detection.

The approved settings are frozen in `MosuPpBalance`: global scale 1, Arles bonus 0.30,
control scale 1, RP strength 1, stream-map nerf 0.50, all type scales 1. Normal client builds
have no `ConfigureForLab` method and cannot replace `MosuPpBalance.Current`.
The optional polygon override requires `EnableMosuPpLabTuning=true` when building the ruleset.

The rules run through `OsuDifficultyCalculator` / `OsuPerformanceCalculator` when the selected
Relax PP system is **MosuPp**, for RX and MRX. Existing system selection is retained.
The MosuPp cache revision moves from 25 to 29 so old client PP values are recalculated.

## Validation

- Release build of `osu.Desktop/osu.Desktop.csproj`: succeeded.
- 151 relevant NUnit tests: passed, including Arles, immutable production settings,
  NM/DT score monotonicity, system selection, managed/native parity and anti-abuse controls.
- Production client assembly versus the saved approved polygon assembly:

| Input group | Cases | Largest absolute numeric difference |
|---|---:|---:|
| calibration | 388 | 0 |
| modifiers | 310 | 0 |
| real-rx | 63 | 0 |

All 761 cases matched, including PP components, stars, RP/control/Arles diagnostics and
classifier output. Two RX scores whose beatmaps could not be downloaded were excluded from
this comparison; they were not replaced with synthetic scores.

The Arles heuristic is calibrated on ten labelled maps and negative controls. Its limits,
calibration examples and the preceding rules are documented in `MOSUPP_CHANGELOG.md`.

## Build and tests

```sh
dotnet build osu.Desktop/osu.Desktop.csproj -c Release -m:1
dotnet test osu.Game.Rulesets.Osu.Tests/osu.Game.Rulesets.Osu.Tests.csproj -c Release -m:1 --filter 'FullyQualifiedName~RxArlesTest|FullyQualifiedName~MosuPpBalanceTest|FullyQualifiedName~RelaxPpSystemSelectionTest|FullyQualifiedName~RealistikRelaxManagedParityTest|FullyQualifiedName~RealistikRelaxBalanceTest|FullyQualifiedName~RelaxAimAntiAbuseTest'
```

The calculator, client tests and release notes are included in this branch. Website code,
deployment configuration, tokens, SSH keys and cached player data are kept outside the release.
