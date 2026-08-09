// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Performance
{
    /// <summary>
    /// Process-level switches for individual Mosu update/render optimisations.
    /// </summary>
    /// <remarks>
    /// Every switch defaults to <c>true</c> (optimisation active). Setting the matching environment variable to
    /// <c>0</c> restores the original upstream behaviour, which allows a single published build to serve both
    /// sides of an A/B run. This mirrors the <c>MOSU_SKIP_EMPTY_TRANSFORM_TRACKERS</c> pattern already used in
    /// the framework.
    ///
    /// Values are read once on first access; changing an environment variable mid-session has no effect.
    /// </remarks>
    public static class MosuOptimisationToggles
    {
        /// <summary>
        /// Render Argon hit-circle fill/gradient layers with <c>FastCircle</c> (a single non-masking SDF quad)
        /// instead of <c>Circle</c> (a masking <c>CircularContainer</c> wrapping a <c>Box</c>).
        /// </summary>
        /// <remarks>
        /// <b>Default off: rejected by A/B on 2026-07-26.</b> The conversion does what it was designed to do --
        /// draw calls and batch flushes fell about 10%, draw-node <c>ApplyState</c> about 12% -- but it still cost
        /// roughly 1% average FPS and 3% p1 on Deferred D3D11 (1210.05 with it, 1222.41 without, everything else
        /// equal). <c>FastCircle</c> binds its own SDF shader, so shader binds rose about 50% (13.6 -> 20.3) as it
        /// ping-pongs with the <c>Box</c>/sprite shader used by adjacent geometry, and each <c>SetShader</c> also
        /// forces a flush. Its per-pixel cost over four large overlapping hit-circle fills outweighs the masking
        /// regions it removes.
        ///
        /// The path is kept behind this switch because the trade-off is GPU-dependent and worth re-testing on
        /// other hardware, or if the fills are ever reordered so FastCircles batch contiguously.
        /// Set <c>MOSU_ARGON_FAST_CIRCLES=1</c> to enable.
        /// </remarks>
        public const string ARGON_FAST_CIRCLES = "MOSU_ARGON_FAST_CIRCLES";

        /// <summary>
        /// Skip creating the per-hit-object kiai flash layer when it can never fire — either because the
        /// beatmap contains no kiai sections, or because skin performance mode disables kiai flashing.
        /// </summary>
        /// <remarks>
        /// The layer is a <c>BeatSyncedContainer</c>, so each live instance runs two control-point binary
        /// searches every update frame on top of its masking region.
        /// </remarks>
        public const string SKIP_UNUSED_KIAI_FLASH = "MOSU_SKIP_UNUSED_KIAI_FLASH";

        /// <summary>
        /// Copy and draw only the live window of the cursor trail ring buffer instead of all 2048 slots.
        /// </summary>
        public const string CURSOR_TRAIL_LIVE_WINDOW = "MOSU_CURSOR_TRAIL_LIVE_WINDOW";

        /// <summary>
        /// Let the aim-assist and raw-input debug overlays drop out of the update and draw-node traversals
        /// entirely while they are switched off, rather than being held in the tree by <c>AlwaysPresent</c>.
        /// </summary>
        public const string LAZY_DEBUG_OVERLAYS = "MOSU_LAZY_DEBUG_OVERLAYS";

        /// <summary>
        /// Quantise the replay/spectator scrolling banner to whole pixels so it does not invalidate its entire
        /// glyph subtree on every single update frame.
        /// </summary>
        public const string QUANTISE_SCROLLING_MESSAGE = "MOSU_QUANTISE_SCROLLING_MESSAGE";

        /// <summary>
        /// In skin performance mode, stop drawing the beatmap background entirely once the user's dim level is
        /// high enough that it is nearly black anyway.
        /// </summary>
        /// <remarks>
        /// R11 established the Vega 7 iGPU is fill/bandwidth-bound at 1080p (~5.1M blended pixels per frame
        /// against a ~7.3 GPix/s effective DDR4 ceiling; GPUBusy 0.70 ms of the 0.83 ms frame). The dimmed
        /// background is a guaranteed fullscreen textured pass (~2.07M of those pixels) every frame. Skipping it
        /// at high dim trades a barely-visible dark image for the single largest per-frame GPU saving available.
        /// </remarks>
        public const string PERF_BLACK_BACKGROUND = "MOSU_PERF_BLACK_BACKGROUND";

        /// <summary>
        /// Draw the default approach circle as a 16-segment ring mesh instead of a full quad, skipping the
        /// ~85% of the quad that is fully transparent yet still rasterised and blended.
        /// </summary>
        /// <remarks>
        /// Pixel attribution (R12) measured DefaultApproachCircle at ~0.49M px/frame — 11% of the fill budget —
        /// on the bandwidth-bound iGPU path. The mesh uses the same texture, shader and vertex batch as the
        /// original quad (only more vertices), so it cannot repeat the shader-ping-pong failure that killed
        /// <see cref="ARGON_FAST_CIRCLES"/>. Geometry margins (inner 16-gon at r=0.86, corner cuts outside
        /// r=1.18) sit strictly outside the texture's visible band (alpha&gt;0 at r=0.888–1.006).
        /// Scoped to <c>DefaultApproachCircle</c> (compiled-in texture); legacy skin art is untouched.
        /// </remarks>
        public const string APPROACH_RING_MESH = "MOSU_APPROACH_RING_MESH";

        /// <summary>
        /// Run replay playback (including the benchmark harness) inside the same LowLatency GC session that
        /// normal gameplay uses, instead of the upstream behaviour of opting replays out of it.
        /// </summary>
        /// <remarks>
        /// <b>Default off: experimental (R13).</b> Upstream excludes replays deliberately (spectator/render
        /// sessions can run for hours, where suppressing gen2 is unsafe); the benchmark replay is short, so
        /// this is a tail-latency experiment: LowLatency suppresses all gen2 collections for the run.
        /// </remarks>
        public const string REPLAY_HIGHPERF_GC = "MOSU_REPLAY_HIGHPERF_GC";

        /// <summary>
        /// Register input/update/draw threads with MMCSS ("Games" task) in Windows ultra performance mode.
        /// </summary>
        /// <remarks>
        /// MMCSS's "Games" task is scheduling category Medium and enforces a CPU quota (SystemResponsiveness,
        /// default 20%): threads that exhaust it are demoted below every normal-priority thread until the next
        /// period. Saturated uncapped threads are exactly the anti-pattern that quota targets, so registration
        /// may be a net negative here. Set <c>MOSU_ULTRA_MMCSS=0</c> to keep plain <c>ThreadPriority.Highest</c>
        /// under the HIGH/REALTIME priority class without MMCSS.
        /// </remarks>
        public const string ULTRA_MMCSS = "MOSU_ULTRA_MMCSS";

        /// <summary>
        /// In skin performance mode, draw the Argon hit-circle body (fills + gradients + white border)
        /// as two textured quads sharing one native texture, instead of four masking Circles plus a
        /// masking RingPiece.
        /// </summary>
        /// <remarks>
        /// Only takes effect while <c>SkinPerformanceMode.Enabled</c> — the lite tree drops the hit
        /// flash/border "bomb" animation the same way the default skin's perf branch does. The two
        /// sprites use the standard sprite shader and one shared texture, so unlike
        /// <see cref="ARGON_FAST_CIRCLES"/> no shader ping-pong is possible.
        /// Set <c>MOSU_ARGON_LITE_BODY=0</c> to restore the layered path for A/B.
        /// </remarks>
        public const string ARGON_LITE_BODY = "MOSU_ARGON_LITE_BODY";

        public static bool ArgonFastCircles { get; } = read(ARGON_FAST_CIRCLES, defaultValue: false);

        public static bool SkipUnusedKiaiFlash { get; } = read(SKIP_UNUSED_KIAI_FLASH);

        public static bool CursorTrailLiveWindow { get; } = read(CURSOR_TRAIL_LIVE_WINDOW);

        public static bool LazyDebugOverlays { get; } = read(LAZY_DEBUG_OVERLAYS);

        public static bool QuantiseScrollingMessage { get; } = read(QUANTISE_SCROLLING_MESSAGE);

        public static bool PerfBlackBackground { get; } = read(PERF_BLACK_BACKGROUND);

        public static bool ApproachRingMesh { get; } = read(APPROACH_RING_MESH);

        public static bool ReplayHighPerformanceGC { get; } = read(REPLAY_HIGHPERF_GC, defaultValue: false);

        public static bool UltraMmcss { get; } = read(ULTRA_MMCSS);

        /// <summary>
        /// In skin performance mode (SimplifyHud), hide the two decorative translucent gradient wedges
        /// behind the argon score counter (~99k px/frame measured, the largest HUD fill item).
        /// </summary>
        public const string PERF_HIDE_HUD_WEDGES = "MOSU_PERF_HIDE_HUD_WEDGES";

        /// <summary>
        /// In skin performance mode (SimplifyHud), hide the argon song-progress density graph (2-5 layered
        /// additive passes over the full-width strip) and the strip's translucent track backdrop.
        /// </summary>
        public const string PERF_SONG_PROGRESS_LITE = "MOSU_PERF_SONG_PROGRESS_LITE";

        /// <summary>
        /// Drop the background's blur BufferedContainer (and its ~9.6MB framebuffer) whenever blur is zero,
        /// re-creating it on demand. Also kills the full-FBO redraw storm during the 800ms blur-out at map
        /// start (~7-9M px/frame while it lasts). Lossless: the blit is replaced by an identical direct draw.
        /// </summary>
        public const string PERF_BG_SIGMA0_BYPASS = "MOSU_PERF_BG_SIGMA0_BYPASS";

        /// <summary>
        /// In skin performance mode (SimplifyEffects), drop the argon spinner's purely-decorative glow
        /// passes: the 25 per-tick white shadow EdgeEffects (25 blurred 90x65 quads, ~146k spinner-local
        /// px/frame for the spinner's whole lifetime) and the two progress-arc additive glow quads (each the
        /// disc-sized arc quad inflated by the sigma-50 blur kernel, ~360k local px apiece, drawn while the
        /// spinner is actively spun — over 1M screen px/frame combined at 1080p). Gameplay feedback is
        /// untouched: the tick bodies (rotation), progress arcs, disc fill glow (tracking alpha, progress
        /// scale and per-rotation completion pulses), centre piece and bonus/SPM counters all remain.
        /// </summary>
        public const string PERF_SPINNER_LITE = "MOSU_PERF_SPINNER_LITE";

        /// <summary>
        /// In skin performance mode, draw the Argon slider follow circle as a ring only — the white ring band
        /// from <c>ArgonLiteCircleTextures</c>, accent-tinted and drawn as a 16-segment ring mesh — instead of
        /// a masking <c>CircularContainer</c> border plus an additive interior fill.
        /// </summary>
        /// <remarks>
        /// Dropping the interior fill is a visible change, so it is opt-in per the project rule (2026-07-26):
        /// the user-facing opt-in is the <c>OsuSetting.ForkArgonFollowRing</c> checkbox (bridged through
        /// <c>SkinPerformanceMode.ArgonFollowRing</c>), and this environment variable is a kill-switch AND'ed
        /// on top — the same composition as <see cref="PERF_BLACK_BACKGROUND"/>. Set
        /// <c>MOSU_ARGON_FOLLOW_RING=0</c> to force-disable regardless of config for A/B.
        /// While a slider is tracked, the layered version rasterises ~155k px/frame of
        /// additive fill through a masking container (audit R13.G), whose masking push/pop also forces a batch
        /// flush. The ring mesh keeps ~38% of that quad (~59k px, same geometry rule as
        /// <see cref="APPROACH_RING_MESH"/>) on the standard sprite shader and default batch, saving ~96k
        /// additively blended px per tracked frame on the write-bound iGPU path. Only the fill goes: the
        /// press/release/end scale+fade animations, additive blending and accent gradient are unchanged.
        /// Only takes effect while <c>SkinPerformanceMode.Enabled</c>.
        /// </remarks>
        public const string ARGON_FOLLOW_RING = "MOSU_ARGON_FOLLOW_RING";

        public static bool ArgonLiteBody { get; } = read(ARGON_LITE_BODY);

        public static bool ArgonFollowRing { get; } = read(ARGON_FOLLOW_RING);

        public static bool PerfHideHudWedges { get; } = read(PERF_HIDE_HUD_WEDGES);

        public static bool PerfSongProgressLite { get; } = read(PERF_SONG_PROGRESS_LITE);

        public static bool PerfBackgroundSigma0Bypass { get; } = read(PERF_BG_SIGMA0_BYPASS);

        public static bool PerfSpinnerLite { get; } = read(PERF_SPINNER_LITE);

        /// <summary>
        /// All toggles and their resolved state, for diagnostics output.
        /// </summary>
        public static IReadOnlyList<(string Name, bool Enabled)> All { get; } = new[]
        {
            (ARGON_FAST_CIRCLES, ArgonFastCircles),
            (SKIP_UNUSED_KIAI_FLASH, SkipUnusedKiaiFlash),
            (CURSOR_TRAIL_LIVE_WINDOW, CursorTrailLiveWindow),
            (LAZY_DEBUG_OVERLAYS, LazyDebugOverlays),
            (QUANTISE_SCROLLING_MESSAGE, QuantiseScrollingMessage),
            (PERF_BLACK_BACKGROUND, PerfBlackBackground),
            (APPROACH_RING_MESH, ApproachRingMesh),
            (REPLAY_HIGHPERF_GC, ReplayHighPerformanceGC),
            (ULTRA_MMCSS, UltraMmcss),
            (ARGON_LITE_BODY, ArgonLiteBody),
            (ARGON_FOLLOW_RING, ArgonFollowRing),
            (PERF_HIDE_HUD_WEDGES, PerfHideHudWedges),
            (PERF_SONG_PROGRESS_LITE, PerfSongProgressLite),
            (PERF_BG_SIGMA0_BYPASS, PerfBackgroundSigma0Bypass),
            (PERF_SPINNER_LITE, PerfSpinnerLite),
        };

        /// <summary>
        /// A single-line, benchmark-loggable description of every toggle's state.
        /// </summary>
        public static string Describe() => string.Join(' ', All.Select(t => $"{t.Name}={(t.Enabled ? 1 : 0)}"));

        /// <summary>
        /// Whether every toggle is still at its shipped default. Used to flag mixed A/B builds in logs.
        /// </summary>
        public static bool AllDefault { get; } =
            ArgonFastCircles == false
            && SkipUnusedKiaiFlash
            && CursorTrailLiveWindow
            && LazyDebugOverlays
            && QuantiseScrollingMessage
            && PerfBlackBackground
            && ApproachRingMesh
            && ReplayHighPerformanceGC == false
            && UltraMmcss
            && ArgonLiteBody
            && ArgonFollowRing
            && PerfHideHudWedges
            && PerfSongProgressLite
            && PerfBackgroundSigma0Bypass
            && PerfSpinnerLite;

        private static bool read(string variable, bool defaultValue = true)
        {
            string? value = Environment.GetEnvironmentVariable(variable);

            if (string.IsNullOrEmpty(value))
                return defaultValue;

            return !(string.Equals(value, "0", StringComparison.Ordinal)
                     || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase));
        }
    }
}
