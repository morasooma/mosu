# MosuPp changelog (Relax / RX only)

> MosuPp ships its own calculator (osu.Game.Rulesets.Osu/Difficulty/Relax/MosuPpRelax): an exact copy of the MosuPp
> development calculator (Realistik core + all rules below), so MosuPp PP is identical to the development build.
> The "Mosu" and "Lazer (vanilla)" systems are unchanged and do not use it.

> Score (osu!, all PP systems) — shipped in the same patch:
> - Relax (RX) has no score penalty any more: score multiplier 0.1x → 1.0x (OsuScoreMultiplierCalculatorV2 only; V1 keeps
>   0.1x because it describes how old scores were stored and is used to migrate them).
> - Difficulty Adjust (DA) score multiplier, per 0.1 of change: CS +0.004x raised / −0.03x lowered, OD +0.001x raised /
>   −0.02x lowered, AR −0.005x either way, HP no effect; each part and the product at least 0.1x.

MosuPp = Mosu/Realistik RX calculator + the rules below. Vanilla PP and star rating are not changed.
Version names: **MosuPp X.Y.Z** — X = full rework, Y = a rule added or removed, Z = numbers changed in existing rules.
The "cache" number is `ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION`; it goes up by 1 with every change so cached PP is recalculated.
Examples are SS scores, RX, with all rules of that version.
Numbering restarts after 1.13.0: **1.13.0 = 0.0.1**. Before / after numbers are made with the test polygon in
`tools/MosuPpLab` (`./lab.sh`), which calculates the maps of `tools/MosuPpLab/maps.txt` with the baseline and the current code.

## MosuPp 1.0.0 (cache 40) — release

First release after production 0.4.0; it ships everything of 0.5.0–0.9.0 below plus:
- **Balance**: aim control type scale 0.75 → 0.87. 0.75 was the 0.5.0 patch for small-spacing control maps that escaped the
  type balance as "unknown"; 0.5.0 also fixed that at its root (unknown-type fallback, density term, CS 4 control window), and
  the patch then cut every control map by a quarter (−26% against 0.4.0, −1.5% without it).
- **CS Gain (RX)** (new, cache 36): smaller circles pay only on maps that are hard by themselves. Reference CS = the map's
  own CS, at most 5. Gain = final SS PP at the played CS / final SS PP at the reference CS (same mods; distances in radii,
  small circle bonus, CS PP Buff, RP, Arles, type scale — all of it; RP and Arles rated with the reference circle). The map
  keeps gain^w, w = smoothstep(final SS PP at the reference CS, 800, 2000). Easy maps no longer turn into farm maps with DA /
  HR, hard maps keep the whole gain, maps with their own CS up to 5 are not touched without CS mods.
  Examples (SS): Hashire Uma Musume DA CS 8 1463 → 306 (NM 304); Overkill DA CS 8 1683 → 434; Zaplatit' DA CS 8 1613 → 625;
  Sendan Life (/b/2267564) DA CS 8 4808 → 2425; glass beach, VANISH, ganbare!kumokosan, Attack with DA CS 8 unchanged;
  Bleed (/b/2815425, CS 6) 957 → 649; HR: Smiling 1557 → 974, Preserved Valkyria 1126 → 868, L9 1738 → 1570, CosMo – 255
  2177 → 2146.
- **RP Balance (RX)** (cache 37): the RP bonus (multiplier above 1) needs a map worth 300–600 SS PP before RP (none below,
  full above, smooth between); the penalty side is unchanged. RP grows as intensity^0.75 and was fitted on maps of
  1000–2800 PP, so it never went below ~330 for an aim slop map: maps with almost no movement had RP 1.6–2.5× their PP
  and all got +20% (median RP / PP 2.2 below 300 PP, 0.9 above 2500 PP).
  Examples (SS): the owner's easy set −17% (Cat (/b/5688819) 256 → 213, Horrible Kids (/b/2596015) 243 → 202, Zombie Nation
  (/b/4507434) 329 → 274, Caramelldansen (/b/150837) 153 → 128); Overkill 434 → 381; Feelin Sky 538 → 462; Libera 496 → 463;
  maps above 600 PP unchanged.
- **Balance** (cache 38, owner's scenario): Stream Map Nerf 0.35 → 0.25, stream type scale 0.85 → 0.97.
  Examples (SS, cache 37 → 38): Expurget (/b/4565459) 327 → 415, DT 1170 → 1307; Worms of Soul (/b/5245120) 310 → 407,
  DT 982 → 1214; A Tale Of Salt And Light (/b/5571670) DT 1400 → 1560; Ixion's Torment 648 → 747.
  Against production 0.4.0 (SS, geometric mean by RP type): aim slop −12%, aim control −18%, streams +11%, tech +31%,
  reading −1%.
- **CS Gain (RX)** (cache 39): HR's own CS raise (×1.3) is not a gain — HR also mirrors the patterns and raises AR / OD, it
  has to be played. With HR the reference CS is raised by ×1.3 (at most 10), so only what DA adds is gated. HR maps are back
  at their PP without CS Gain: Smiling 984 → 1563, Preserved Valkyria 868 → 1126, Dreamin' attraction 792 → 1068, L9 1570 →
  1738; DA CS 6 rows unchanged.
- **Accuracy Gate (RX)** (new, cache 40): accuracy PP is paid by how hard the map is. Realistik accuracy PP depends only on OD,
  accuracy and circle count, so an easy OD 9 map paid as much accuracy PP as a hard one (≈ 34% of the PP of easy maps, 5–9% of
  hard ones), while in RX the game clicks the notes. Accuracy PP × (0.15 + 0.85 · smoothstep(skill PP, 200, 350)), skill PP =
  aim + speed PP of an SS on the map + mods after the MosuPp aim rules.
  The owner's easy set is now about half of production (median −49%, −38..−55%), maps with 1000+ PP are unchanged. Other maps
  that lose more than 3% (SS, cache 39 → 40): heat abnormal 394 → 316, Overkill 381 → 314, LATHI 482 → 408, Feelin Sky
  463 → 407, Hashire Uma Musume 254 → 225, burn this moment 331 → 296, Arche 475 → 427.

Against production 0.4.0 (SS, geometric mean by RP type over the test set, cache 40): aim slop −12%, aim control −19%,
streams +11%, tech +28%, reading −11% (heat, the only reading map, is easy at NM); the examples below are from cache 35. Examples (0.4.0 → 1.0.0): ganbare!kumokosan (/b/2833124) 2975 → 2662; Attack (/b/5637274) 2474 → 2153,
DT 6297 → 5632; glass beach (/b/5870318) 2796 → 2502, DT 7429 → 6649; Six Seven (/b/5711898) 2128 → 1904; Hills Have Eyes
(/b/4354559) 1980 → 1772; Sendan Life (/b/2267564) 1526 → 1366; Mor 3 (/b/4654706) 2038 → 1752; VANISH (/b/4323073)
2652 → 2277; UMMU (/b/1743443) 1191 → 994, DT 2363 → 1989; FEEEEVER TIME (/b/3446204) DT 1932 → 1681; Bleed (/b/2815425)
824 → 957; THE PLATINUM TRIGGER (/b/4352925) 690 → 1240; Lost Requiem [Zenith] (/b/5026621) 970 → 1731; HAWATARI
(/b/3977191) 836 → 1415; Nhelv (/b/2932798) DT 1636 → 1787; Expurget (/b/4565459) 284 → 327, DT 1184 → 1170; Worms of Soul
(/b/5245120) DT 1119 → 982; Songs Compilation (/b/4897206) DT 985 → 1168.

## MosuPp 0.9.0 (cache 34)

- **Realistik pattern balance (RX)**: anti-farm "spike + filler" −14% → −12%.
- **Balance**: tech type scale 1.00 → 1.20 (owner's final value); aim slop type scale 0.87 → 0.895, so aim slop maps are on
  average −11% against production 0.4.0 (geometric mean over the 34 aim slop SS rows of the test set; most of them exactly −11%).
  Against 0.4.0 by RP type: aim slop −11%, aim control −26%, streams +1%, tech +36%, reading 0%.
  Examples (SS, 0.8.0 → 0.9.0): Attack (/b/5637274) 2064 → 2153, DT 5473 → 5632; ganbare!kumokosan (/b/2833124) 2588 → 2662;
  THE PLATINUM TRIGGER (/b/4352925) 1033 → 1240; Lost Requiem [Zenith] 1442 → 1731; Songs Compilation (/b/4897206) DT
  1154 → 1168.

## MosuPp 0.8.0 (cache 33)

- **Section Types (RX)** (new): the map-type rules no longer see one type for the whole map. The difficulty is a weighted sum of
  400 ms section peaks (hardest first, 0.9 per rank), so every section's share of the PP is known (aim and speed combined by
  their share of the total). A section's stream share is the share of its notes in straight streams (stacked notes excluded);
  the map's stream weight = Σ section PP share × section stream share. Example: Ne Obliviscaris – Songs Compilation
  (/b/4897206), 6:33 with about a minute of hard jump and stream sections: streams earn 59% of its PP (Worms of Soul 99%,
  Expurget 89%, Libera 39%, Sendan Life 3%).
- **Stream Map Nerf (RX)**: proportional to the stream weight instead of smoothstep(stream note share, 30%, 60%) — only the
  part of the PP the streams earn loses up to 35%; hard jump sections of stream maps are not nerfed as streams.
- **RP Balance + type scale (RX)**: a stream map is rated as streams only by its stream weight; the rest takes the RP and the
  type scale of the map's type without its streams (0.2.0 classification with no stream notes, aim slop when nothing fits).
  Other types are unchanged (their RP scales were fitted together with their streams).
  Examples (SS, 0.7.0 → 0.8.0): Songs Compilation DT 886 → 1154 (players' estimate ≈ 1200), NM 339 → 417; Libera 383 → 487;
  Yomi yori Kitarite Utsutsu 505 → 674; This Calling 376 → 483; Last Resort 917 → 1129; Expurget 303 → 322, DT 1042 → 1165;
  A Tale Of Salt And Light DT 1229 → 1393; BLANKFIELD – Goodbye 474 → 379 and Jashin 490 → 435 (their streams earn 67% / 85%
  of the PP but had too few stream notes for the old share gate); burn this moment DT 960 → 857. Average by RP type: streams
  +11%, aim control −1%, tech −0.3%, aim slop / reading / unknown 0%.

## MosuPp 0.7.0 (cache 32)

- **Realistik pattern balance (RX)**: the speed dominance part (up to −32% when speed PP ≫ aim PP, full from 850 speed PP) is
  removed. It grew with the map's difficulty and only hit hard aim control with DT (FEEEEVER TIME, Rubik's Cube, UMMU), while
  streams are already balanced by Stream Map Nerf, RP Balance and the type scale.
- **Realistik pattern balance (RX)**: the "spike + filler" anti-farm part −8% → −14% (Spike Filler Guard still lifts it for maps
  with 2–3+ hard moments). The flow overboost and HR wide stream parts are unchanged. The site shows every part separately.
- **Stream Map Nerf (RX)**: maximum −50% → −35% (`StreamNerf` 0.50 → 0.35).
  Examples (SS, 0.6.0 → 0.7.0): Rubik's Cube DT 1513 → 1873; FEEEEVER TIME DT 1338 → 1461; UMMU DT 1715 (Aim Control decides);
  Worms of Soul 242 → 305; Expurget 241 → 303, DT 1006 → 1042 (RP takes the rest back); Libera 315 → 383; A Tale Of Salt And
  Light 403 → 448; Attack (/b/5637274) 2152 → 2064; Euphoria 671 → 627. Average by RP type: streams +7%, aim control +2%, aim
  slop −0.5%, tech −0.4%; all maps +1.3%.

## MosuPp 0.6.0 (cache 31)

- **RP Balance (RX), tech type**: tech maps are recognised by sliders that move about as much as the jumps, not by the slider
  share alone. Tech = slider share 33–43% (was 45–55%) × slider / jump speed over the hardest 20 s 0.40–0.55 (new), streams
  tolerated up to 25–45% (was 15–35%). Jump and control maps with many slow sliders (Zaplatit', Hashire, Machinegun, Sound
  Chimera, LATHI: ratio ≤ 0.47) keep their type. 14 of the 15 tech maps of the test set are tech now (was 6); HERA (54% streams)
  stays aim control. No other map of the test set changes its type.
- **RP Balance (RX)**: tech RP scale 147 → 280. The old scale was fitted on 3 maps and pushed the rest to the −20% limit while
  tech maps the classifier missed got +20% as "unknown"; every tech map of the test set now reaches the +20% limit.
- **Balance**: tech type scale 0.95 → 1.00 (owner: tech is hard for everyone, about +20% RP and +200 PP).
  Examples (SS, 0.5.0 → 0.6.0): THE PLATINUM TRIGGER (/b/4352925) 654 → 1033; Lost Requiem [Zenith] (/b/5026621) 913 → 1442,
  [Celestial] (/b/5229944) 779 → 1212; HAWATARI (/b/3977191) 794 → 1193; desocializedvandalism (/b/3233265) 802 → 1053;
  Egonomy (/b/3569579) 294 → 464; Nhelv (/b/2932798) 471 → 628, DT 1147 → 1530 (owner's target ≈ 1500); Transhumanist
  (/b/3352759) 612 → 644; RPG (/b/3333745) 704 → 704. Average over the 14 tech maps +209 PP; other maps unchanged.

## MosuPp 0.5.0 (cache 30)

Fixes after 0.4.0 went live: dense small-spacing aim control maps were still overpaid, and lowering CS with DA cost too little.
Worked out together with Codex (OpenAI) on the polygon; numbers below are SS, compared with 0.4.0 as released.

- **Balance**: the owner's site scenario becomes the client default (`MosuPpBalance`): Arles bonus 0.30 → 0.20, control scale
  1 → 0.90, type scales stream 0.85, aim slop 0.87, tech 0.95, aim control 0.75 (global 1, RP strength 1, stream nerf 0.50 kept).
  Average change against 0.4.0 by type: streams −17%, aim slop −13%, aim control −22%, tech −11%, reading −9%.
- **RP Balance (RX)**: maps the 0.2.0 classification leaves unknown take the content type of the polygon's pattern classifier
  (stream / aim slop / tech / aim control, no reading / Arles override); aim control only when the fast-run spacing (circles
  played) is ≤ 1.1. These maps had the generous unknown RP scale (363) and no per-type scale, so the aim control scale never
  reached them. Meshuggah – Bleed DT 1794 → 1345, Sunless Rise – Ghosts Of The Past DT 2001 → 1501, Hashire Uma Musume DT
  873 → 654, Camellia – Arche DT 1235 → 926, goreshit – burn this moment DT 1280 → 960 (−25% against the scenario alone;
  We Could Get More Machinegun Psystyle!, 1.13 circles, a jump map, stays unknown).
- **RP Balance (RX)**: the density term of the aim control Fitts intensity pays only for notes reached by a move
  (× smoothstep(spacing, 0.5, 1.0 circles)) — tiny moves are not hard aim control in RX. Small effect (UMMU DT −3%, HERA ±5%).
- **Aim Control (RX)**: the spacing window that decides which moves are control is measured in CS 4 circles; velocity still uses
  the circles played. Bigger circles no longer turn wide jumps into control moves: with DA CS 4 → 2 the control multiplier of
  Mor 3 grows ×1.57 → ×1.87 instead of ×2.59, control maps lose like jump maps.
- **CS Reduction (RX)** (new): bigger circles than the map has (DA with a lower CS, EZ) — total × (native radius / played
  radius)^k, k = `MosuPpBalance.CsReductionStrength` = 1.6 (site slider «Штраф за понижение CS», 0 disables). Only a reduction
  relative to the map's own CS; native low-CS maps and raised CS are not touched. Applied last, after RP and Arles.
  DA CS 4 → 2: ganbare!kumokosan, glass beach, Sendan Life, Vanish, Mor 3 −45% (0.4.0: −13…−22%); CS 5 → 2 Attack −64%;
  CS 3 → 2 Expurget −22%. EZ: Six Seven, ganbare −39%, Mor 3 −48% (0.4.0: Six Seven EZ +20%). Code: `RxCsReduction.cs`.
- The RP type is the type of the map as played with its mods (owner's decision): spacing is measured in the circles played, so
  weak control with a low CS can become streams, streams with HR can become control, EZ (low AR) makes a map a reading map.
  E.g. We Could Get More Machinegun Psystyle! DA CS 2 → aim control (−64% with CS Reduction), Six Seven EZ → reading (−39%).

## MosuPp 0.4.0 (cache 29)

The client includes the approved polygon balance: global/control/type scales 1, Arles bonus 0.30,
RP strength 1 (existing ±20% clamp), stream-map nerf 0.50. `MosuPpBalance.Current` is immutable
in normal builds. The polygon override method is compiled only when the ruleset is built with
`EnableMosuPpLabTuning=true`; release builds do not load lab JSON or website settings.
This revision also includes the 0.1.0–0.3.0 rules below. The client cache revision moves from 25 to 29.


- **Arles (RX)**: content detector shared by the client and the polygon (`RxArles.cs`), calibrated on the ten
  maps in `tools/MosuPpLab/dataset.txt`. Wide jumps with changes of spacing and non-trivial turns, mixed with
  flowing bursts or wider technical jump/burst sections. Dense streams, slider-heavy tech and simple jump spam
  are gated out. Does not use map IDs, titles or authors. Arles is the fifth pattern style; Reading remains a
  separate low-AR classification override. The existing RP classification/scales are retained.
- Confidence < 0.6: no bonus; 0.6–0.85: smooth ramp; ≥ 0.85: +30%. Applied after RP to prevent its ±20% clamp
  from absorbing the bonus. One multiplier per prepared map + mods, independent of accuracy/misses. Rate mods
  keep the shape confidence; HR/DA CS can change spacing in circle diameters. Existing penalties remain active.
- Compared against the **pre-Arles MosuPp 0.3.0 calculator**: all 10 NM labels detected, SS gain +24.96…30%; 388 calculations in the
  original pool plus dataset, only these ten and Calling Out changed. Calling Out was already described as
  Arles in the old player's note and is now relabelled. The other 86 map/mod combinations keep both PP and
  classifier output exactly. Another 310 modifier/score cases cover DT, HD, HR, HT, DT lock, DA AR/CS and
  1/15/20 misses: RP and existing skills before the Arles factor remain unchanged; factor stays in [1, 1.3]
  and is independent of score accuracy/misses. Results: `tools/MosuPpLab/results/arles/`.
- This is a heuristic calibrated on a small labelled sample, not a learned model or a guarantee for unseen
  maps. Extend both positive labels and negative controls before widening thresholds.

## MosuPp 0.3.0 (cache 28)

- **Stream Map Nerf (RX)**: a note where the cursor turns (angle between the move in and the move out < 120°) is no longer a
  stream note. Real streams go straight or along a curve (8% of their notes turn), aim control patterns keep turning (57%), so
  fast aim control maps are no longer cut as stream maps (LATHI DT ×0.71 → ×1, Rubik's Cube ×0.67 → ×1, FEEEEVER TIME ×0.81 → ×1);
  Expurget, Worms of Soul, A Tale Of Salt And Light keep −50%.
- **Aim Control (RX)** (new): fast moves (≤ 110 ms) that change direction at small / medium spacing form a third skill.
  Control pressure per second = Σ turn · spacing window · velocity / 10 circles per s (turn = 1 − smoothstep(angle, 70°, 130°),
  window = 1 − smoothstep(spacing, 1.5, 2.5 circles)); control PP = 67.2 · (hardest 20 s) · (1 + seconds ≥ 40% of it / 30)^0.5;
  total × (1 + (control PP / SS PP)³)^(1/3) — one multiplier per map + mods, same for every accuracy. Jump, stream and reading maps
  change by < 3% (Ne Obliviscaris – Libera NM +17%, heat +5%). Code: `RxAimControl.cs`.
  Fitted to DT SS targets UMMU 1700, Rubik's Cube 1450, FEEEEVER TIME 2000, Nhelv 1500, LATHI 1600 and Mor 3 NM 2000.
  Results (SS, 0.2.0 → 0.3.0, with RP Balance): FEEEEVER TIME (/b/3446204) DT 752 → 1932; Mor 3 (/b/4654706) NM 1334 → 2038;
  Nhelv (/b/2932798) DT 1081 → 1614; LATHI (/b/3888384) DT 697 → 1387; UMMU (/b/1743443) DT 1117 → 2363;
  Rubik's Cube (/b/1754266) DT 1190 → 2148 (the last two are above their targets; kept as agreed).
- **RP Balance (RX)**: aim slop RP × (1 − 0.45 · smoothstep(jump repetition, 35%, 50%)) — jumps repeating the previous jump
  (spacing ±15%, angle ±15°) are easy; aim slop scale 328 → 331.8. Sayfalse – ESSE CARA! (/b/5322879, 47% repeated, "simple
  comfortable jumps") DT 1800 → 1513 (target 1500); Attack and #WE ♥ YURI (~31%) not touched. Fitted with the new aim slop targets
  DK – Zaplatit' DT 1650, DJ Mars – Lemon Tree NM 1950.
- **Spike Filler Guard (RX)** (new): the Realistik "spike + filler" anti-farm (aim −8% when the 3 hardest aim sections are ~2× the
  60th percentile and cover < 8% of the map) fired on about two thirds of all maps. It no longer applies to maps with several hard
  moments (≥ 8 map seconds at ≥ 60% of the hardest 20 s, gaps ≤ 3 s merged): weight × (1 − smoothstep(moments, 1.5, 2.5)) — one
  moment keeps it (Attack, ganbare!kumokosan, Hills Have Eyes, DK – Zaplatit'), two halve it, three and more remove it. Only the
  anti-farm aim part is lifted (the spike weight also switches off the Realistik speed-dominance penalty, which stays as it was).
  Code: `RxSpikeFillerGuard.cs`; hard moments are counted in `RxRespectPoints.cs`. RP aim slop scale 331.8 → 340.4.
  Agreed target katagiri – Sendan Life (/b/2267564) NM 1600: 1487 → 1526. Other changes 0…+9% (maps with several hard moments whose
  RP limit is reached: Flamewall NM 1664 → 1729, Stardust, Children Of Bodom, Nanahoshi DT +8.7%); nothing goes down.

## MosuPp 0.2.0 (cache 27)

- **RP Balance (RX)** (new): RP (respect points) is a difficulty value of a map + mods built only from what the player physically
  faces, independent of the PP formulas; the total PP moves to RP within ±20%: total × clamp(RP / SS PP, 0.8, 1.2), where SS PP is
  the MosuPp PP of an SS on the same map + mods (all other rules applied). One multiplier per map + mods, the same for every
  accuracy / miss count. Code: `RxRespectPoints.cs` (port of the `tools/MosuPpLab` prototype, identical RP on all test maps).
  - Map type by content: streams (≥ ~190 BPM), aim slop (jumps, any length), tech (≥ 55% sliders), aim control (stream-like density
    below ~190 BPM), reading (effective AR ≤ ~8).
  - Skill pressure, hardest 20 s: every move (velocity / 50 circles per s)^1.5 — "speed × size", faster moves weigh more; moves after
    a slider timed from its end (−36 ms tail leniency); every slider span (velocity / 50)^1.5; stream moves only above the curve
    "BPM × spacing" (1.5 circles at 230 BPM, 0.5 at 410 BPM). Aim control uses the Fitts throughput instead.
  - RP = scale(type) · intensity^0.75 (aim control: Fitts^1.25) · (1 + hard seconds / 30)^0.15 · reading · precision.
  - Fitted 2026-10-02 on 25 labelled maps to agreed PP: ganbare 2800, Six Seven 2400, Attack ≈ Hills Have Eyes ≈ 2100,
    Flamewall [ETERNAL SACRED FIRE] 1400, Vanish 2700, Mor 3 2000, glass beach as before; other maps of each type keep their PP on
    average.
  Examples (SS, 0.1.0 → 0.2.0): ganbare!kumokosan (/b/2833124) 3203 → 2866; Attack (/b/5637274) 3092 → 2474 (−20%, limit), DT 7871
  → 6297; Six Seven (/b/5711898) 1773 → 2128 (+20%, limit); Hills Have Eyes (/b/4354559) 2084 → 1908; Flamewall (/b/3135149)
  1988 → 1645; Vanish (/b/4323073) 3009 → 2652; Mor 3 (/b/4654706) 1112 → 1334 (+20%, limit); glass beach (/b/5870318) 2575 → 2694,
  DT 7971 → 7158; Expurget (/b/4565459) 339 → 271, DT 909 → 1091; A Tale Of Salt And Light (/b/5571670) DT 1133 → 1360;
  Worms of Soul (/b/5245120) 341 → 283; HAWATARI (/b/3977191) 994 → 836.

## MosuPp 0.1.0 (cache 26)

- **AR/OD Lock (RX)** (new): rate mods with "Блокировать AR/OD" (DT / NC / HT / DC, SB, TS) are calculated with the AR and
  OD really played. Before, the RX core sped up the approach time by the rate and used the map OD as the rate-adjusted
  baseline, so a locked play got exactly the PP of an unlocked one (Attack DT / DT lock 7871 / 7871). A locked play is now
  calculated like an unlocked play with AR / OD moved so that on screen they match the locked values — the same as DA:
  DT 1.5 lock on AR 10 / OD 10 = DT with DA AR 8.5 / OD 8.33. Unlocked plays are unchanged.
  Alone (without Visible Notes Reading), SS DT → DT lock: Attack (/b/5637274) 7871 → 6344 (−19%; AR part −1%, OD part −18%):
  the OD multiplier (OD 10 ×1.00 → OD 8.33 ×0.83) and accuracy PP drop, and the Realistik reading skill rates AR 11 above AR 10.
- **Visible Notes Reading (RX)** (new): the more spaced notes are on screen at once, the harder the map is to read (lower AR
  and higher BPM both put more notes on screen). For every note, the notes already appeared after it (within the approach time
  of the effective AR) weighted by smoothstep(spacing, 0.5, 1.5 circle diameters) — stacks and tight streams count little;
  map value = average of the 20% most cluttered notes. Total × (1 + 0.20 · smoothstep(visible notes, 5, 9)).
  Normal maps at AR 10 have ≤ 5 (jump maps 4–5, stream maps 1.5–3), so NM and plain DT / NC / HT are unchanged (DT raises AR
  with the rate, the number of notes on screen stays the same); AR/OD lock, DA AR and EZ change it. Fades out below effective
  AR 8 → 7, where the Realistik low-AR reading bonus takes over.
  Examples (SS, 0.0.1 → 0.1.0, both rules): DT lock — Attack (/b/5637274, 6.7 notes) 7871 → 6816 (−13%),
  ganbare!kumokosan (/b/2833124, 7.9) 8547 → 7880 (−8%), Six Seven (/b/5711898, 7.0) 5056 → 4364 (−14%),
  glass beach (/b/5870318, 6.0) 7971 → 6405 (−20%), Sky of Twilight (/b/5208003, 4.6) 3911 → 3041 (−22%),
  Expurget (/b/4565459, 2.3) 909 → 670 (−26%), heat (/b/4303461, AR 0) 1013 → 924 (−9%);
  DA AR 8 (NM) — ganbare 3396 → 4074 (+20%), Attack 3161 → 3573 (+13%), glass beach 2751 → 3026 (+10%),
  Sky of Twilight, Flamewall, MCR ±0% (≤ 5 notes); DA AR 9 — ganbare +10%, Attack +3%.

## MosuPp 1.13.0 (cache 25)

- **Simple Stream Nerf (RX)**: **removed**.
- **Stream Map Nerf (RX)** (new): every stream map, whatever its stream shapes, total × (1 − 0.50 · smoothstep(SS speed/aim,
  1.0, 1.3) · smoothstep(stream share, 30%, 60%)).
- **Global PP Scale (RX)**: **removed** (the +10% of 1.11.0).
  Examples (SS, NM / DT, 1.12.3 → 1.13.0): Expurget (/b/4565459, 86% streams) 745 / 1999 → 339 / 909;
  A Tale Of Salt And Light (/b/5571670, 70%) 824 / 2493 → 542 / 1133; Worms of Soul (/b/5245120) 375 / 1186 → 341 / 1079;
  heat (/b/4303461) 423 / 1135 → 379 / 1013; drivers license [full] (/b/4566445, jump map with streams) 1601 / 4924 → 1455 / 4477 (only −10% scale);
  all other maps −9% (the removed +10%): glass beach (/b/5870318) 2832 / 8768 → 2575 / 7971, Attack (/b/5637274) 3401 / 8658 → 3092 / 7871.

## MosuPp 1.12.3 (cache 24)

- **Simple Stream Nerf (RX)**: max nerf raised from −35% to **−50%**.
  Worms of Soul (/b/5245120) SS NM / DT 487 / 1542 → 375 / 1186. A Tale Of Salt And Light (/b/5571670) 824 / 2493,
  heat (/b/4303461), Novae Ruptis (/b/3802844), drivers license (/b/4566445) and jump maps ±0% (not simple streams).

## MosuPp 1.12.2 (cache 23)

- **Simple Stream Nerf (RX)**: stream-map gate moved from speed/aim 1.3–1.65 to **1.1–1.35**, so NM stream maps get the
  full nerf too. Worms of Soul (/b/5245120) SS NM / DT 707 / 1542 → 487 / 1542. All other test maps ±0%
  (drivers license, A Tale Of Salt And Light, Novae Ruptis keep ±0% because their streams are not simple).

## MosuPp 1.12.1 (cache 22)

- **Simple Stream Nerf (RX)**: max nerf raised from −20% to **−35%**.
- **Spike Nerf (RX)**: max nerf lowered from −30% to **−20%** (maps with one hard part and filler).
  Examples (SS, NM / DT, 1.12.0 → 1.12.1): Attack (/b/5637274) 2976 / 7577 → 3401 / 8658 (+14%);
  Lament (/b/5826020) 2263 / 6597 → 2452 / 7278 (+8% / +10%); mcr (/b/5326952) +3%; Sky of Twilight (/b/5208003), Juliet (/b/4543721) +1%;
  Worms of Soul (/b/5245120) 623 / 1787 → 707 / 1542 (NM +13%: speed/aim 1.39 only half-opens the stream-map gate and the
  weaker Spike Nerf helps it; DT −14%). Other test maps ±0%.

## MosuPp 1.12.0 (cache 21)

- **Simple Stream Nerf (RX)**: max nerf lowered from −30% to **−20%**.
- **Low CS Nerf (RX)**: **removed**.
- **CS PP Buff (RX)**: **restored with half the bonus**: CS ≤ 3.5 +0%, 4 +5%, 5 +10%, 6 +15%, 7 +25%, 8 +50%, 9 +100%, 10 +200%
  (same smooth PCHIP curve, CS incl. HR/EZ/DA).
  Examples (SS, NM / DT, 1.11.0 → 1.12.0): Worms of Soul (/b/5245120, CS 4.4) 542 / 1457 → 623 / 1787;
  Attack (/b/5637274, CS 5) 2705 / 6888 → 2976 / 7577; Spider-Man (/b/4784320, CS 6.1) 935 / 2766 → 1081 / 3200;
  glass beach (/b/5870318, CS 4) 2697 / 8350 → 2832 / 8768; Sky of Twilight (/b/5208003, CS 3.6) 1298 / 4050 → 1366 / 4263;
  WE ♥ YURI (/b/5377716, CS 3.5) 1995 / 6424 → 2100 / 6762; drivers license [full] (/b/4566445, CS 2.6) 1313 / 4038 → 1601 / 4924.

## MosuPp 1.11.0 (cache 20)

- **Point Variety Nerf (RX)**: **restored** (same as 1.9.0). Kami no Kotoba small CS (/b/2589130) 971 / 2265 → 218 / 546.
- **Heavy Miss Penalty (RX)** (new): total × e^(−0.11 · max(0, misses − 15)) on top of the base miss penalty
  (replaces the removed Low Accuracy Nerf as the main punishment for bad plays). Sky of Twilight (/b/5208003), share of
  the SS PP kept: 15 misses 41% (unchanged), 20 → 20%, 25 → 10%, 30 → 5%.
- **Low CS Nerf (RX)** (new): total × CS 4+ 1.00, 3.5 0.95, 3 0.90, 2.5 0.80, ≤ 2 0.70 (linear in between, CS incl. HR/EZ).
- **Global PP Scale (RX)** (new): every MosuPp score ×1.10.
  Examples (SS, NM / DT, 1.10.0 → 1.11.0): glass beach (/b/5870318, CS 4) 2452 / 7591 → 2697 / 8350 (+10%);
  jump pack 3 (/b/2303920) 2222 / 7272 → 2445 / 7999; Attack (/b/5637274) 2459 / 6262 → 2705 / 6888;
  Sky of Twilight (/b/5208003, CS 3.6) 1229 / 3835 → 1298 / 4050 (+6%); WE ♥ YURI (/b/5377716, CS 3.5) 1909 / 6148 → 1995 / 6424;
  Crazy banger (/b/2562794, CS 3) 1263 / 3285 → 1251 / 3252 (−1%); drivers license [full] (/b/4566445, CS 2.6)
  1455 / 4477 → 1313 / 4038 (−10%).

## MosuPp 1.10.0 (cache 19)

- **CS PP Buff (RX)**: **removed** (CS no longer multiplies the PP; high-CS maps rely on the base calculator only).
- **Low Accuracy Nerf (RX)**: **removed** (scores below 75% accuracy are no longer cut extra).
- **Point Variety Nerf (RX)**: **removed**.
  Examples (SS, NM / DT, 1.9.0 → 1.10.0): Attack (/b/5637274, CS 5) 2951 / 7515 → 2459 / 6262;
  Spider-Man (/b/4784320, CS 6.1) 1117 / 3304 → 850 / 2514; glass beach (/b/5870318) 2697 / 8350 → 2452 / 7591;
  jump pack 3 (/b/2303920) 2445 / 7999 → 2222 / 7272; Novae Ruptis (/b/3802844) 1932 / 5891 → 1756 / 5355;
  Kami no Kotoba small CS (/b/2589130) 273 / 686 → 971 / 2265 (was held down by Point Variety Nerf);
  WE ♥ YURI (/b/5377716), drivers license (/b/4566443, /b/4566445), Crazy banger (/b/2562794) ±0% (CS ≤ 3.5).

## MosuPp 1.9.0 (cache 18)

- **Short High CS Nerf (RX)** (new): total × (1 − 0.15 · smoothstep(CS, 5, 6) · (1 − smoothstep(objects, 300, 700))).
  Smooth in both CS and length (no cliff at CS 6): CS 6+ → −15% at ≤ 300 objects, −7.5% at 500, ±0% from 700;
  CS 5.5 → half; CS ≤ 5 → ±0%. CS includes HR/EZ.
  Spider-Man Theme (/b/4784320, CS 6.1, 256 objects) SS NM / DT 1314 / 3887 → 1117 / 3304 (−15%);
  Kami no Kotoba small CS (/b/2589130, CS 6.5, 46 objects) 321 / 807 → 273 / 686 (−15%).
  Not touched: Attack (/b/5637274, CS 5, 500 objects) and all other test maps (CS ≤ 5).

## MosuPp 1.8.0 (cache 17)

- **Extreme Jump Nerf (RX)** (new): extreme jump = move between two objects with cursor velocity ≥ 4.5 px/ms
  (measured at clock rate 1, so NM / DT / HR get the same multiplier). Aim PP × (1 − 0.31 · smoothstep(extreme jumps, 80, 180)).
  glass beach (/b/5870318, 180 extreme jumps) SS NM / DT 3859 / 11881 → 2697 / 8350 (−30%).
  Not touched (±0%): Attack (/b/5637274, 60), Lament (/b/5826020, 51), Kami no Kotoba (/b/2589130, 43),
  drivers license (/b/4566443, /b/4566445, 20), Novae Ruptis (/b/3802844), jump pack 3 (/b/2303920), YURI (/b/5377716),
  Sky of Twilight (/b/5208003), mcr (/b/5326952) and all other test maps.

## MosuPp 1.7.0 (cache 16)

- **Length Bonus (RX)**: max bonus lowered from +60% to **+20%**.
- **Spike Nerf (RX)**: max nerf lowered from −45% to **−30%**.
- **Short Aim Nerf (RX)**: **removed**.
  Examples (SS, NM / DT, before → after): jump pack 3 (/b/2303920) 3148 / 10503 → 2445 / 7999;
  glass beach (/b/5870318) 4008 / 12417 → 3859 / 11881; Expurget (/b/4565459) 777 / 2204 → 678 / 1818;
  WE ♥ YURI (/b/5377716) 2484 / 8138 → 1909 / 6148; A Tale Of Salt And Light (/b/5571670) 877 / 2731 → 773 / 2337;
  Worms of Soul (/b/5245120) 447 / 1440 → 564 / 1518; Attack (/b/5637274) 2212 / 5668 → 2951 / 7515.

## MosuPp 1.6.0 (cache 15)

- **Traceable = Hidden (RX)** (new): the Traceable mod (TC) is calculated exactly like Hidden (HD) — same reading PP,
  same HD reading guard, same PP for TC and HD scores (also with DT etc.). MosuRealistik unchanged.

## MosuPp 1.5.0 (cache 14)

- **Short Aim Nerf (RX)** (new): total × (1 − 0.10 · (1 − smoothstep(objects, 300, 700))
  · (1 − smoothstep(SS speed PP / aim PP, 0.5, 0.8))). Short aim-only maps −10% (bbydoll, Harumachi Clover,
  drivers license [aim], Spider-Man, Kami no Kotoba), Attack −5%, Crazy banger −2%; long maps and stream/mixed maps ±0%.
  By object count, so NM and DT are nerfed the same.

## MosuPp 1.4.1 (cache 13)

- **Map-type decisions use the SS reference**: Aim-Focused Flow Guard, Simple Stream Nerf and Stream-Only Guard now
  read speed PP / aim PP from an SS on the same map + mods (cached per map + mods) instead of the score's own values.
  Accuracy and misses no longer move a score across a rule threshold. SS results are unchanged.

## MosuPp 1.4.0 (cache 12)

- **One-Point Map Guard (RX)** (new): maps where almost every note is stacked on the previous one (< 5 px) and the
  whole map uses 1–2 spots give practically nothing with any mods: total × (1 − 0.97 · smoothstep(stacked, 60%, 90%)
  · (1 − smoothstep(spots per 32 notes, 2, 4))).
  hehehe - Iyul' [HS]: NM 134 → 4, DT 135 → 4, HD 208 → 6, HD+DT 264 → 8.
  Full maps with stacked streams (5+ spots), Kami no Kotoba (nothing stacked) and all normal maps ±0%.

## MosuPp 1.3.3 (cache 11)

- **1.3.2 reverted**: Point Variety Nerf cuts only aim and speed PP again (as in 1.1.0–1.3.1).
- **Stream-Only Guard (RX)**: stronger — speed PP stops counting in the total:
  speed × (1 − smoothstep(speed PP / aim PP, 5, 10)), **no speed PP at all from 10×**; the existing
  total × (1 − 0.9 · smoothstep(ratio, 6, 10)) stays.
  Point stream 400 BPM CS 10: NM 113 → 72, DT 159 → 101; 800 BPM CS 10 DT 3522 (1.0.0) → 151.
  Worms of Soul (≤ 3.1×), A Tale Of Salt And Light (≤ 2.1×), heat, drivers license ±0%.

## MosuPp 1.3.2 (cache 10) (reverted in 1.3.3)

- **Point Variety Nerf (RX)**: now also cuts accuracy and reading PP (before: aim and speed only).
  hehehe - Iyul' [HS] (every note on the centre, AR 0): NM 134 → 19, HD 253 → 31, DT 135 → 20.
  Kami no Kotoba small CS NM 321 → 208. Maps with normal patterns (20–30 spots per 32 notes) ±0%.

## MosuPp 1.3.1 (cache 9)

- **Simple Stream Nerf (RX)**: now only for stream maps — extra gate smoothstep(speed PP / aim PP, 1.3, 1.65).
  Jump maps with streams are no longer touched: drivers license (full diff) back to NM 1479 / DT 4520 (was −21%).
  Worms of Soul stays −30% (speed/aim 1.71 NM, 3.06 DT); Tale ±0%.

## MosuPp 1.3.0 (cache 8)

- **Aim-Focused Flow Guard (RX)**: enabled again (as in 1.1.0).
- **Simple Stream Nerf (RX)** (new): stream-dominated maps with simple streams (straight lines / one smooth curve)
  lose up to **30%** of the total PP; must be simple by both angle variety (< 16°, normal from 26°) and bending-direction
  changes (< 12%, normal from 18%); stream share 40% → 60%. Same for NM and DT.
  Worms of Soul −30% (NM 639 → 447, DT 2058 → 1440); drivers license (full diff) −21%;
  A Tale Of Salt And Light, heat, Novae Ruptis, Sky of Twilight, jump maps ±0%.

## MosuPp 1.2.0 (cache 7)

- **Aim-Focused Flow Guard (RX)**: **disabled for testing** (code kept in `RxAimFocusedFlowGuard.cs`, not called).
  Sky of Twilight / Novae Ruptis / Attack / Lament / Calm Down Juliet get their flow bonus back (≈ +15% vs 1.1.0).
  All other 1.1.0 rules stay.

## MosuPp 1.1.0 (cache 6)

- **Length Bonus (RX)**: max bonus raised from +40% to **+60%** (smoothstep of difficult strain count 100 → 400).
  A Tale Of Salt And Light NM +6% / DT +8%, Worms of Soul +1% / +2%, WE ♥ YURI +13%, most maps 0–1%.
- **Point Variety Nerf (RX)** (new): average distinct 24 px spots per 32-object window; below 12 spots aim and speed
  PP are cut, up to −85% at ≤ 3 spots. Kami no Kotoba "small CS" / "TURBO": about ×0.2 of total PP. Normal maps ±0%.
- **Aim-Focused Flow Guard (RX)** (new): the Realistik flow bonus (up to +17% aim strain) is removed on aim-focused
  maps (speed PP < 0.8× aim PP; full bonus again from 1.1×). Sky of Twilight / Novae Ruptis / Attack / Lament −13…14%,
  Calm Down Juliet −10% / −4%; Tale, Worms, heat and jump maps without flow ±0%.
- **Stream-Only Guard (RX)** (new): total PP × (1 − 0.9 · smoothstep(speed PP / aim PP, 6, 10)).
  Streams into one point at absurd BPM keep ~10% (400 BPM CS 10 DT 2270 → 227). Worms DT (3.1×) and Tale (≤ 2.1×) ±0%.

## MosuPp 1.0.0 (cache 5)

- **CS PP Buff (RX)**: ×1 at CS ≤ 3.5, +10% CS 4, +20% CS 5, +30% CS 6, +50% CS 7, +100% CS 8, +200% CS 9, +400% CS 10 (PCHIP).
- **Length Bonus (RX)**: up to +40% by difficult strain count (100 → 400).
- **Spike Nerf (RX)**: up to −45% by peak ratio (top-10 sections / 75th percentile) 1.8 → 4.0.
- **Low Accuracy Nerf (RX)**: ≥ 75% ×1, 75% → 70% smooth to ×0.25, below 70% ×0.25·((acc − 55%) / 15%)³.
