# MosuPp 1.0.0 client release

MosuPp 1.0.0 replaces production 0.4.0 in the MosuPp RX calculator. Its main change is **Section Types**:
rules that depend on map type now apply to the share of PP that each pattern earns, not to the whole map.
A stream map with hard jump sections is no longer nerfed as streams in those sections.

## Changes since 0.4.0

Full details and examples are in `MOSUPP_CHANGELOG.md` (0.5.0–1.0.0).

- **Section Types** (new, `RxSectionTypes.cs`): the stream weight is the share of the SS PP that the map's streams earn.
  It is computed from the 400 ms section peaks of the difficulty calculator.
  - Stream Map Nerf is proportional to the stream weight, up to −25% of that share.
  - For stream maps, RP Balance and the type scale are blended with the map's type without its streams.
- **CS Reduction** (new): lower CS through DA or EZ costs PP (CS 4 → 2 ≈ −45%).
- **CS Gain** (new, `RxCsGain.cs`): smaller circles pay only on maps that are hard on their own.
  - The reference CS is the map's own CS, at most 5.
  - The gain is final SS PP at the played CS divided by final SS PP at the reference CS. The map keeps gain^w, where
    w = smoothstep(final SS PP at the reference CS, 800, 2000).
  - With DA CS 8, Hashire Uma Musume goes from 1463 to 306 (NM 304). glass beach, VANISH and ganbare!kumokosan keep the
    whole gain.
- **RP Balance**:
  - The RP bonus (above 1) needs a map worth 300–600 SS PP before RP. Maps with almost no movement no longer get +20%.
  - Maps of unknown type take a content type.
  - The tech type is recognised by sliders that move about as much as the jumps.
  - The tech RP scale was refitted, so tech maps reach the +20% limit.
- **Aim Control**: the window is measured in CS 4 circles. The density term only counts moved notes.
- **Realistik pattern balance**:
  - The speed dominance part is removed (it took up to −32% from hard aim control with DT).
  - The "spike + filler" anti-farm is −12% (−8% in Realistik).
- **Balance** (frozen in `MosuPpBalance`):
  - global scale 1, Arles bonus 0.20, control scale 0.90, RP strength 1, stream nerf 0.25;
  - type scales: streams 0.97, aim slop 0.895, tech 1.20, aim control 0.87;
  - CS Reduction strength 1.6.
- **Diagnostics** (`[JsonIgnore]`, not serialised): each rule's multiplier, map composition, RP type, and stream weight.

Against 0.4.0 (SS, geometric mean by RP type over the 87 calibration rows):

| RP type | Change |
|---|---:|
| Aim slop | −12% |
| Aim control | −18% |
| Streams | +11% |
| Tech | +31% |
| Reading | −1% |

The rules run through `OsuDifficultyCalculator` / `OsuPerformanceCalculator` when the selected Relax PP system is
**MosuPp**, for RX and MRX. The MosuPp cache revision moves from 29 to 38, so cached PP is recalculated.
Normal client builds still have no `ConfigureForLab` method. The polygon override requires `EnableMosuPpLabTuning=true`.

## Validation

- Release build of `osu.Desktop/osu.Desktop.csproj` succeeded.
- 175 relevant NUnit tests passed, including the new Section Types, CS Reduction, CS Gain and RP bonus tests.
- The polygon was built against this branch with `EnableMosuPpLabTuning=false` and compared to the approved polygon
  assembly. Every numeric output was compared: PP components, stars, RP, control, Arles, the rule breakdown and the
  classifier.

| Input group | Cases | Largest absolute numeric difference |
|---|---:|---:|
| calibration | 348 | 0 |
| modifiers | 310 | 0 |
| real-rx | 63 | 0 |

All 721 cases matched.

## Build and tests

```sh
dotnet build osu.Desktop/osu.Desktop.csproj -c Release -m:1
dotnet test osu.Game.Rulesets.Osu.Tests/osu.Game.Rulesets.Osu.Tests.csproj -c Release -m:1 --filter 'FullyQualifiedName~RxRespectPointsMultiplierTest|FullyQualifiedName~RxCsGainTest|FullyQualifiedName~RxSectionTypesTest|FullyQualifiedName~RxCsReductionTest|FullyQualifiedName~RxArlesTest|FullyQualifiedName~MosuPpBalanceTest|FullyQualifiedName~RelaxPpSystemSelectionTest|FullyQualifiedName~RealistikRelaxManagedParityTest|FullyQualifiedName~RealistikRelaxBalanceTest|FullyQualifiedName~RelaxAimAntiAbuseTest'
```

This branch contains the calculator, client tests and release notes. Website code, deployment configuration, tokens,
SSH keys and cached player data are not part of the release.
