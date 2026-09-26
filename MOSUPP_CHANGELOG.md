# MosuPp changelog (Relax / RX only)

> MosuPp ships its own calculator (osu.Game.Rulesets.Osu/Difficulty/Relax/MosuPpRelax): an exact copy of the MosuPp
> development calculator (Realistik core + all rules below), so MosuPp PP is identical to the development build.
> The "Mosu" and "Lazer (vanilla)" systems are unchanged and do not use it.

MosuPp = Mosu/Realistik RX calculator + the rules below. Vanilla PP and star rating are not changed.
Every change bumps `ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION` so cached PP is recalculated.
MosuPp is the third option of the Relax PP system setting (Mosu / Lazer (vanilla) / MosuPp); the other two are unchanged.
Examples are SS scores, RX, with all rules of that version.

## Version 15

- **Traceable = Hidden (RX)** (new): the Traceable mod (TC) is calculated exactly like Hidden (HD) — same reading PP,
  same HD reading guard, same PP for TC and HD scores (also with DT etc.). MosuRealistik unchanged.

## Version 14

- **Short Aim Nerf (RX)** (new): total × (1 − 0.10 · (1 − smoothstep(objects, 300, 700))
  · (1 − smoothstep(SS speed PP / aim PP, 0.5, 0.8))). Short aim-only maps −10% (bbydoll, Harumachi Clover,
  drivers license [aim], Spider-Man, Kami no Kotoba), Attack −5%, Crazy banger −2%; long maps and stream/mixed maps ±0%.
  By object count, so NM and DT are nerfed the same.

## Version 13

- **Map-type decisions use the SS reference**: Aim-Focused Flow Guard, Simple Stream Nerf and Stream-Only Guard now
  read speed PP / aim PP from an SS on the same map + mods (cached per map + mods) instead of the score's own values.
  Accuracy and misses no longer move a score across a rule threshold. SS results are unchanged.

## Version 12

- **One-Point Map Guard (RX)** (new): maps where almost every note is stacked on the previous one (< 5 px) and the
  whole map uses 1–2 spots give practically nothing with any mods: total × (1 − 0.97 · smoothstep(stacked, 60%, 90%)
  · (1 − smoothstep(spots per 32 notes, 2, 4))).
  hehehe - Iyul' [HS]: NM 134 → 4, DT 135 → 4, HD 208 → 6, HD+DT 264 → 8.
  Full maps with stacked streams (5+ spots), Kami no Kotoba (nothing stacked) and all normal maps ±0%.

## Version 11

- **Version 10 reverted**: Point Variety Nerf cuts only aim and speed PP again (as in version 6–9).
- **Stream-Only Guard (RX)**: stronger — speed PP stops counting in the total:
  speed × (1 − smoothstep(speed PP / aim PP, 5, 10)), **no speed PP at all from 10×**; the existing
  total × (1 − 0.9 · smoothstep(ratio, 6, 10)) stays.
  Point stream 400 BPM CS 10: NM 113 → 72, DT 159 → 101; 800 BPM CS 10 DT 3522 (v5) → 151.
  Worms of Soul (≤ 3.1×), A Tale Of Salt And Light (≤ 2.1×), heat, drivers license ±0%.

## Version 10 (reverted in version 11)

- **Point Variety Nerf (RX)**: now also cuts accuracy and reading PP (before: aim and speed only).
  hehehe - Iyul' [HS] (every note on the centre, AR 0): NM 134 → 19, HD 253 → 31, DT 135 → 20.
  Kami no Kotoba small CS NM 321 → 208. Maps with normal patterns (20–30 spots per 32 notes) ±0%.

## Version 9

- **Simple Stream Nerf (RX)**: now only for stream maps — extra gate smoothstep(speed PP / aim PP, 1.3, 1.65).
  Jump maps with streams are no longer touched: drivers license (full diff) back to NM 1479 / DT 4520 (was −21%).
  Worms of Soul stays −30% (speed/aim 1.71 NM, 3.06 DT); Tale ±0%.

## Version 8

- **Aim-Focused Flow Guard (RX)**: enabled again (as in version 6).
- **Simple Stream Nerf (RX)** (new): stream-dominated maps with simple streams (straight lines / one smooth curve)
  lose up to **30%** of the total PP; must be simple by both angle variety (< 16°, normal from 26°) and bending-direction
  changes (< 12%, normal from 18%); stream share 40% → 60%. Same for NM and DT.
  Worms of Soul −30% (NM 639 → 447, DT 2058 → 1440); drivers license (full diff) −21%;
  A Tale Of Salt And Light, heat, Novae Ruptis, Sky of Twilight, jump maps ±0%.

## Version 7

- **Aim-Focused Flow Guard (RX)**: **disabled for testing** (code kept in `RxAimFocusedFlowGuard.cs`, not called).
  Sky of Twilight / Novae Ruptis / Attack / Lament / Calm Down Juliet get their flow bonus back (≈ +15% vs version 6).
  All other version 6 rules stay.

## Version 6

- **Length Bonus (RX)**: max bonus raised from +40% to **+60%** (smoothstep of difficult strain count 100 → 400).
  A Tale Of Salt And Light NM +6% / DT +8%, Worms of Soul +1% / +2%, WE ♥ YURI +13%, most maps 0–1%.
- **Point Variety Nerf (RX)** (new): average distinct 24 px spots per 32-object window; below 12 spots aim and speed
  PP are cut, up to −85% at ≤ 3 spots. Kami no Kotoba "small CS" / "TURBO": about ×0.2 of total PP. Normal maps ±0%.
- **Aim-Focused Flow Guard (RX)** (new): the Realistik flow bonus (up to +17% aim strain) is removed on aim-focused
  maps (speed PP < 0.8× aim PP; full bonus again from 1.1×). Sky of Twilight / Novae Ruptis / Attack / Lament −13…14%,
  Calm Down Juliet −10% / −4%; Tale, Worms, heat and jump maps without flow ±0%.
- **Stream-Only Guard (RX)** (new): total PP × (1 − 0.9 · smoothstep(speed PP / aim PP, 6, 10)).
  Streams into one point at absurd BPM keep ~10% (400 BPM CS 10 DT 2270 → 227). Worms DT (3.1×) and Tale (≤ 2.1×) ±0%.

## Version 5

- **CS PP Buff (RX)**: ×1 at CS ≤ 3.5, +10% CS 4, +20% CS 5, +30% CS 6, +50% CS 7, +100% CS 8, +200% CS 9, +400% CS 10 (PCHIP).
- **Length Bonus (RX)**: up to +40% by difficult strain count (100 → 400).
- **Spike Nerf (RX)**: up to −45% by peak ratio (top-10 sections / 75th percentile) 1.8 → 4.0.
- **Low Accuracy Nerf (RX)**: ≥ 75% ×1, 75% → 70% smooth to ×0.25, below 70% ×0.25·((acc − 55%) / 15%)³.
