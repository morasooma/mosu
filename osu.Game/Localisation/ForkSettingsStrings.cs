// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ForkSettingsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.ForkSettings";

        /// <summary>
        /// "About Morasooma"
        /// </summary>
        public static LocalisableString AboutMorasoomaHeader => new TranslatableString(getKey(@"about_morasooma_header"), @"About Morasooma");

        /// <summary>
        /// "About Morasooma"
        /// </summary>
        public static LocalisableString AboutMorasoomaButton => new TranslatableString(getKey(@"about_morasooma_button"), @"About Morasooma");

        /// <summary>
        /// "Close"
        /// </summary>
        public static LocalisableString AboutMorasoomaClose => new TranslatableString(getKey(@"about_morasooma_close"), @"Close");

        /// <summary>
        /// "Morasooma is an independent fork of the open-source osu! client. It is not affiliated with, endorsed by, or sponsored by ppy Pty Ltd. The osu! and ppy names and logos are trademarks of their respective owner. Upstream code is used under the MIT License; third-party and upstream resources retain their respective licenses."
        /// </summary>
        public static LocalisableString AboutMorasoomaDescription => new TranslatableString(getKey(@"about_morasooma_description"),
            @"Morasooma is an independent fork of the open-source osu! client. It is not affiliated with, endorsed by, or sponsored by ppy Pty Ltd. The osu! and ppy names and logos are trademarks of their respective owner. Upstream code is used under the MIT License; third-party and upstream resources retain their respective licenses.");

        /// <summary>
        /// "Aim Assist"
        /// </summary>
        public static LocalisableString AimAssistHeader => new TranslatableString(getKey(@"aim_assist_header"), @"Aim Assist");
        /// <summary>
        /// "Enable aim assist"
        /// </summary>
        public static LocalisableString AimAssistEnableCaption => new TranslatableString(getKey(@"aim_assist_enable_caption"), @"Enable aim assist");
        /// <summary>
        /// "Enables assistance in moving the cursor to notes and sliders."
        /// </summary>
        public static LocalisableString AimAssistEnableHint => new TranslatableString(getKey(@"aim_assist_enable_hint"), @"Enables assistance in moving the cursor to notes and sliders.");
        /// <summary>
        /// "Show aim assist targets"
        /// </summary>
        public static LocalisableString AimAssistShowTargetsCaption => new TranslatableString(getKey(@"aim_assist_show_targets_caption"), @"Show aim assist targets");
        /// <summary>
        /// "Shows debug targets and areas of aim assist operation."
        /// </summary>
        public static LocalisableString AimAssistShowTargetsHint => new TranslatableString(getKey(@"aim_assist_show_targets_hint"), @"Shows debug targets and areas of aim assist operation.");
        /// <summary>
        /// "Show flow-debug assist"
        /// </summary>
        public static LocalisableString AimAssistShowFlowCaption => new TranslatableString(getKey(@"aim_assist_show_flow_caption"), @"Show flow-debug assist");
        /// <summary>
        /// "Shows how the aim assist annotates pattern, which chain is considered flow, and along which trajectory the cursor is moved."
        /// </summary>
        public static LocalisableString AimAssistShowFlowHint => new TranslatableString(getKey(@"aim_assist_show_flow_hint"), @"Shows how the aim assist annotates pattern, which chain is considered flow, and along which trajectory the cursor is moved.");
        /// <summary>
        /// "Assist strength"
        /// </summary>
        public static LocalisableString AimAssistStrengthCaption => new TranslatableString(getKey(@"aim_assist_strength_caption"), @"Assist strength");
        /// <summary>
        /// "How strongly assist pulls the cursor to the current target."
        /// </summary>
        public static LocalisableString AimAssistStrengthHint => new TranslatableString(getKey(@"aim_assist_strength_hint"), @"How strongly assist pulls the cursor to the current target.");
        /// <summary>
        /// "FOV radius"
        /// </summary>
        public static LocalisableString AimAssistFovCaption => new TranslatableString(getKey(@"aim_assist_fov_caption"), @"FOV radius");
        /// <summary>
        /// "Radius within which the assist begins to confidently lock onto the target."
        /// </summary>
        public static LocalisableString AimAssistFovHint => new TranslatableString(getKey(@"aim_assist_fov_hint"), @"Radius within which the assist begins to confidently lock onto the target.");
        /// <summary>
        /// "Intent threshold"
        /// </summary>
        public static LocalisableString AimAssistIntentCaption => new TranslatableString(getKey(@"aim_assist_intent_caption"), @"Intent threshold");
        /// <summary>
        /// "How clearly the player&apos;s movement must be directed towards the target for assist to help."
        /// </summary>
        public static LocalisableString AimAssistIntentHint => new TranslatableString(getKey(@"aim_assist_intent_hint"), @"How clearly the player's movement must be directed towards the target for assist to help.");
        /// <summary>
        /// "Dynamic friction"
        /// </summary>
        public static LocalisableString AimAssistFrictionCaption => new TranslatableString(getKey(@"aim_assist_friction_caption"), @"Dynamic friction");
        /// <summary>
        /// "Additional &quot;stickiness&quot; near target. Higher values stick cursor tighter."
        /// </summary>
        public static LocalisableString AimAssistFrictionHint => new TranslatableString(getKey(@"aim_assist_friction_hint"), @"Additional ""stickiness"" near target. Higher values stick cursor tighter.");
        /// <summary>
        /// "Anti-jitter after click"
        /// </summary>
        public static LocalisableString AimAssistJitterCaption => new TranslatableString(getKey(@"aim_assist_jitter_caption"), @"Anti-jitter after click");
        /// <summary>
        /// "Smooths tiny cursor shake right after click."
        /// </summary>
        public static LocalisableString AimAssistJitterHint => new TranslatableString(getKey(@"aim_assist_jitter_hint"), @"Smooths tiny cursor shake right after click.");
        /// <summary>
        /// "Overshoot allowance"
        /// </summary>
        public static LocalisableString AimAssistOvershootCaption => new TranslatableString(getKey(@"aim_assist_overshoot_caption"), @"Overshoot allowance");
        /// <summary>
        /// "How much the assist forgives overshooting the center while still holding the target."
        /// </summary>
        public static LocalisableString AimAssistOvershootHint => new TranslatableString(getKey(@"aim_assist_overshoot_hint"), @"How much the assist forgives overshooting the center while still holding the target.");
        /// <summary>
        /// "Center bias"
        /// </summary>
        public static LocalisableString AimAssistCenterCaption => new TranslatableString(getKey(@"aim_assist_center_caption"), @"Center bias");
        /// <summary>
        /// "How strongly the assist targets the center of the note. Lower values mean more freedom inside the object."
        /// </summary>
        public static LocalisableString AimAssistCenterHint => new TranslatableString(getKey(@"aim_assist_center_hint"), @"How strongly the assist targets the center of the note. Lower values mean more freedom inside the object.");

        /// <summary>
        /// "General"
        /// </summary>
        public static LocalisableString ForkSettingsHeader => new TranslatableString(getKey(@"fork_settings_header"), @"General");
        /// <summary>
        /// "Reduce volume outside gameplay"
        /// </summary>
        public static LocalisableString ReduceVolumeOutsideGameplayCaption => new TranslatableString(getKey(@"reduce_volume_outside_gameplay_caption"), @"Reduce volume outside gameplay");
        /// <summary>
        /// "Halves the master volume in menus and on results screens without changing your volume sliders."
        /// </summary>
        public static LocalisableString ReduceVolumeOutsideGameplayHint => new TranslatableString(getKey(@"reduce_volume_outside_gameplay_hint"), @"Halves the master volume in menus and on results screens without changing your volume sliders.");
        /// <summary>
        /// "Connection"
        /// </summary>
        public static LocalisableString ConnectionHeader => new TranslatableString(getKey(@"connection_header"), @"Connection");
        /// <summary>
        /// "Connection route"
        /// </summary>
        public static LocalisableString ConnectionProxyCaption => new TranslatableString(getKey(@"connection_proxy_caption"), @"Connection route");
        /// <summary>
        /// "Selects the route used for Morasooma API, website and multiplayer traffic. Latency is measured using a small HTTPS request."
        /// </summary>
        public static LocalisableString ConnectionProxyHint => new TranslatableString(getKey(@"connection_proxy_hint"), @"Selects the route used for Morasooma API, website and multiplayer traffic. Latency is measured using a small HTTPS request.");
        /// <summary>
        /// "The game will restart to apply the new connection route."
        /// </summary>
        public static LocalisableString ConnectionProxyRestartBody => new TranslatableString(getKey(@"connection_proxy_restart_body"), @"The game will restart to apply the new connection route.");
        /// <summary>
        /// "Main server"
        /// </summary>
        public static LocalisableString ConnectionRouteDirect => new TranslatableString(getKey(@"connection_route_direct"), @"Main server");
        /// <summary>
        /// "Proxy 1"
        /// </summary>
        public static LocalisableString ConnectionRouteProxy1 => new TranslatableString(getKey(@"connection_route_proxy1"), @"Proxy 1");
        /// <summary>
        /// "Proxy 2"
        /// </summary>
        public static LocalisableString ConnectionRouteProxy2 => new TranslatableString(getKey(@"connection_route_proxy2"), @"Proxy 2");
        /// <summary>
        /// "measuring…"
        /// </summary>
        public static LocalisableString ConnectionLatencyMeasuring => new TranslatableString(getKey(@"connection_latency_measuring"), @"measuring…");
        /// <summary>
        /// "unavailable"
        /// </summary>
        public static LocalisableString ConnectionLatencyUnavailable => new TranslatableString(getKey(@"connection_latency_unavailable"), @"unavailable");
        /// <summary>
        /// "{0} ({1}) — {2}"
        /// </summary>
        public static LocalisableString ConnectionLatencyStatus(LocalisableString route, string host, LocalisableString status) => new TranslatableString(getKey(@"connection_latency_status"), @"{0} ({1}) — {2}", route, host, status);
        /// <summary>
        /// "{0} ms"
        /// </summary>
        public static LocalisableString ConnectionLatencyMilliseconds(long milliseconds) => new TranslatableString(getKey(@"connection_latency_milliseconds"), @"{0} ms", milliseconds);
        /// <summary>
        /// "Performance"
        /// </summary>
        public static LocalisableString PerformanceHeader => new TranslatableString(getKey(@"performance_header"), @"Performance");
        /// <summary>
        /// "Interface"
        /// </summary>
        public static LocalisableString InterfaceHeader => new TranslatableString(getKey(@"interface_header"), @"Interface");
        /// <summary>
        /// "Use skin cursor outside gameplay"
        /// </summary>
        public static LocalisableString UseSkinCursorOutsideGameplayCaption => new TranslatableString(getKey(@"use_skin_cursor_outside_gameplay_caption"), @"Use skin cursor outside gameplay");
        /// <summary>
        /// "Uses the selected skin's gameplay cursor in the main menu, song select, and other interface screens."
        /// </summary>
        public static LocalisableString UseSkinCursorOutsideGameplayHint => new TranslatableString(getKey(@"use_skin_cursor_outside_gameplay_hint"), @"Uses the selected skin's gameplay cursor in the main menu, song select, and other interface screens.");
        /// <summary>
        /// "Debug"
        /// </summary>
        public static LocalisableString DebugHeader => new TranslatableString(getKey(@"debug_header"), @"Debug");
        /// <summary>
        /// "Show beatmaps without audio"
        /// </summary>
        public static LocalisableString ShowBeatmapsWithMissingAudioCaption => new TranslatableString(getKey(@"show_beatmaps_with_missing_audio_caption"), @"Show beatmaps without audio");
        /// <summary>
        /// "Allows loading beatmaps with missing audio files (silence will be used instead)."
        /// </summary>
        public static LocalisableString ShowBeatmapsWithMissingAudioHint => new TranslatableString(getKey(@"show_beatmaps_with_missing_audio_hint"), @"Allows loading beatmaps with missing audio files (silence will be used instead).");
        /// <summary>
        /// "Quick export logs"
        /// </summary>
        public static LocalisableString QuickExportLogsBtn => new TranslatableString(getKey(@"quick_export_logs_btn"), @"Quick export logs");
        /// <summary>
        /// "Game restart is required for this setting to apply."
        /// </summary>
        public static LocalisableString SkinPerfRestartRequired => new TranslatableString(getKey(@"skin_perf_restart_required"), @"Game restart is required for this setting to apply.");
        /// <summary>
        /// "Go to tablet settings (Anti-smoothing)"
        /// </summary>
        public static LocalisableString TabletSettingsBtn => new TranslatableString(getKey(@"tablet_settings_btn"), @"Go to tablet settings (Anti-smoothing)");
        /// <summary>
        /// "Enable anti-smoothing (Reconstructor)"
        /// </summary>
        public static LocalisableString TabletReconstructorCaption => new TranslatableString(getKey(@"tablet_reconstructor_caption"), @"Enable anti-smoothing (Reconstructor)");
        /// <summary>
        /// "Anti-smoothing strength (Weight)"
        /// </summary>
        public static LocalisableString TabletReconstructorWeightCaption => new TranslatableString(getKey(@"tablet_reconstructor_weight_caption"), @"Anti-smoothing strength (Weight)");
        /// <summary>
        /// "Enable jitter suppression (CHATTER EXTERMINATOR RAW)"
        /// </summary>
        public static LocalisableString TabletChatterCaption => new TranslatableString(getKey(@"tablet_chatter_caption"), @"Enable jitter suppression (CHATTER EXTERMINATOR RAW)");
        /// <summary>
        /// "The original Kuuube screen filter. Suppresses small cursor movements with minimal latency."
        /// </summary>
        public static LocalisableString TabletChatterHint => new TranslatableString(getKey(@"tablet_chatter_hint"), @"The original Kuuube screen filter. Suppresses small cursor movements with minimal latency.");
        /// <summary>
        /// "Jitter suppression strength (2–3 drag, 5–6 hover)"
        /// </summary>
        public static LocalisableString TabletChatterStrengthCaption => new TranslatableString(getKey(@"tablet_chatter_strength_caption"), @"Jitter suppression strength (2–3 drag, 5–6 hover)");
        /// <summary>
        /// "Original filter recommendations: 2–3 while dragging and 5–6 while hovering."
        /// </summary>
        public static LocalisableString TabletChatterStrengthHint => new TranslatableString(getKey(@"tablet_chatter_strength_hint"), @"Original filter recommendations: 2–3 while dragging and 5–6 while hovering.");
        /// <summary>
        /// "Enable dynamic smoothing (Radial Follow)"
        /// </summary>
        public static LocalisableString TabletRadialFollowCaption => new TranslatableString(getKey(@"tablet_radial_follow_caption"), @"Enable dynamic smoothing (Radial Follow)");
        /// <summary>
        /// "Stabilises small movements while preserving responsiveness during fast movements."
        /// </summary>
        public static LocalisableString TabletRadialFollowHint => new TranslatableString(getKey(@"tablet_radial_follow_hint"), @"Stabilises small movements while preserving responsiveness during fast movements.");
        /// <summary>
        /// "Radial Follow: outer radius (px)"
        /// </summary>
        public static LocalisableString TabletRadialFollowOuterRadiusCaption => new TranslatableString(getKey(@"tablet_radial_follow_outer_radius_caption"), @"Radial Follow: outer radius (px)");
        /// <summary>
        /// "Maximum cursor lag from the real position. Original value: 5 px."
        /// </summary>
        public static LocalisableString TabletRadialFollowOuterRadiusHint => new TranslatableString(getKey(@"tablet_radial_follow_outer_radius_hint"), @"Maximum cursor lag from the real position. Original value: 5 px.");
        /// <summary>
        /// "Radial Follow: inner radius (px)"
        /// </summary>
        public static LocalisableString TabletRadialFollowInnerRadiusCaption => new TranslatableString(getKey(@"tablet_radial_follow_inner_radius_caption"), @"Radial Follow: inner radius (px)");
        /// <summary>
        /// "Dead zone within which no movement is created. Original value: 0 px."
        /// </summary>
        public static LocalisableString TabletRadialFollowInnerRadiusHint => new TranslatableString(getKey(@"tablet_radial_follow_inner_radius_hint"), @"Dead zone within which no movement is created. Original value: 0 px.");
        /// <summary>
        /// "Radial Follow: smoothing coefficient"
        /// </summary>
        public static LocalisableString TabletRadialFollowSmoothingCaption => new TranslatableString(getKey(@"tablet_radial_follow_smoothing_caption"), @"Radial Follow: smoothing coefficient");
        /// <summary>
        /// "Higher values move the cursor more slowly from the outer radius to the inner radius. Original value: 0.95."
        /// </summary>
        public static LocalisableString TabletRadialFollowSmoothingHint => new TranslatableString(getKey(@"tablet_radial_follow_smoothing_hint"), @"Higher values move the cursor more slowly from the outer radius to the inner radius. Original value: 0.95.");
        /// <summary>
        /// "Radial Follow: transition softness"
        /// </summary>
        public static LocalisableString TabletRadialFollowSoftKneeCaption => new TranslatableString(getKey(@"tablet_radial_follow_soft_knee_caption"), @"Radial Follow: transition softness");
        /// <summary>
        /// "Controls transition smoothness at the outer-radius boundary. Original value: 1."
        /// </summary>
        public static LocalisableString TabletRadialFollowSoftKneeHint => new TranslatableString(getKey(@"tablet_radial_follow_soft_knee_hint"), @"Controls transition smoothness at the outer-radius boundary. Original value: 1.");
        /// <summary>
        /// "Radial Follow: residual smoothing"
        /// </summary>
        public static LocalisableString TabletRadialFollowLeakCaption => new TranslatableString(getKey(@"tablet_radial_follow_leak_caption"), @"Radial Follow: residual smoothing");
        /// <summary>
        /// "Amount of smoothing outside the outer radius. Original value: 0%."
        /// </summary>
        public static LocalisableString TabletRadialFollowLeakHint => new TranslatableString(getKey(@"tablet_radial_follow_leak_hint"), @"Amount of smoothing outside the outer radius. Original value: 0%.");
        /// <summary>
        /// "Enable spline smoothing (BezierInterpolator)"
        /// </summary>
        public static LocalisableString TabletBezierCaption => new TranslatableString(getKey(@"tablet_bezier_caption"), @"Enable spline smoothing (BezierInterpolator)");
        /// <summary>
        /// "Spatially smooths the pen path by interpreting it as a continuous bezier spline."
        /// </summary>
        public static LocalisableString TabletBezierHint => new TranslatableString(getKey(@"tablet_bezier_hint"), @"Spatially smooths the pen path by interpreting it as a continuous bezier spline.");
        /// <summary>
        /// "BezierInterpolator: smoothing factor"
        /// </summary>
        public static LocalisableString TabletBezierSmoothingCaption => new TranslatableString(getKey(@"tablet_bezier_smoothing_caption"), @"BezierInterpolator: smoothing factor");
        /// <summary>
        /// "EMA weight applied to raw positions before interpolation. 1 = no extra smoothing, lower is smoother. Original value: 1."
        /// </summary>
        public static LocalisableString TabletBezierSmoothingHint => new TranslatableString(getKey(@"tablet_bezier_smoothing_hint"), @"EMA weight applied to raw positions before interpolation. 1 = no extra smoothing, lower is smoother. Original value: 1.");
        /// <summary>
        /// "TTF/OTF format is not supported!"
        /// </summary>
        public static LocalisableString UnsupportedFontHeader => new TranslatableString(getKey(@"unsupported_font_header"), @"TTF/OTF format is not supported!");
        /// <summary>
        /// "The font must be converted to BMFont format (.fnt + .png) to work.\nOpen the guide to learn how to do this."
        /// </summary>
        public static LocalisableString UnsupportedFontBody => new TranslatableString(getKey(@"unsupported_font_body"), @"The font must be converted to BMFont format (.fnt + .png) to work.\nOpen the guide to learn how to do this.");
        /// <summary>
        /// "Open guide (browser)"
        /// </summary>
        public static LocalisableString UnsupportedFontGuideButton => new TranslatableString(getKey(@"unsupported_font_guide_button"), @"Open guide (browser)");
        /// <summary>
        /// "Cancel"
        /// </summary>
        public static LocalisableString UnsupportedFontCancelButton => new TranslatableString(getKey(@"unsupported_font_cancel_button"), @"Cancel");
        /// <summary>
        /// "Beatmap download mirror"
        /// </summary>
        public static LocalisableString DownloadMirrorCaption => new TranslatableString(getKey(@"download_mirror_caption"), @"Beatmap download mirror");
        /// <summary>
        /// "Auto-select (fastest)"
        /// </summary>
        public static LocalisableString DownloadMirrorAuto => new TranslatableString(getKey(@"download_mirror_auto"), @"Auto-select (fastest)");
        /// <summary>
        /// "Sayobot"
        /// </summary>
        public static LocalisableString DownloadMirrorSayobot => new TranslatableString(getKey(@"download_mirror_sayobot"), @"Sayobot");
        /// <summary>
        /// "Nerinyan"
        /// </summary>
        public static LocalisableString DownloadMirrorNerinyan => new TranslatableString(getKey(@"download_mirror_nerinyan"), @"Nerinyan");
        /// <summary>
        /// "Mino"
        /// </summary>
        public static LocalisableString DownloadMirrorMino => new TranslatableString(getKey(@"download_mirror_mino"), @"Mino");
        /// <summary>
        /// "BeatConnect"
        /// </summary>
        public static LocalisableString DownloadMirrorBeatConnect => new TranslatableString(getKey(@"download_mirror_beatconnect"), @"BeatConnect");
        /// <summary>
        /// "Chimu"
        /// </summary>
        public static LocalisableString DownloadMirrorChimu => new TranslatableString(getKey(@"download_mirror_chimu"), @"Chimu");
        /// <summary>
        /// "OsuDirect"
        /// </summary>
        public static LocalisableString DownloadMirrorOsuDirect => new TranslatableString(getKey(@"download_mirror_osudirect"), @"OsuDirect");
        /// <summary>
        /// "Server (official)"
        /// </summary>
        public static LocalisableString DownloadMirrorServer => new TranslatableString(getKey(@"download_mirror_server"), @"Server (official)");
        /// <summary>
        /// "Hinamizawa"
        /// </summary>
        public static LocalisableString DownloadMirrorHinamizawa => new TranslatableString(getKey(@"download_mirror_hinamizawa"), @"Hinamizawa");
        /// <summary>
        /// "Download all beatmaps"
        /// </summary>
        public static LocalisableString PlaylistDownloadAll => new TranslatableString(getKey(@"playlist_download_all"), @"Download all beatmaps");
        /// <summary>
        /// "There are no maps to download in the playlist"
        /// </summary>
        public static LocalisableString PlaylistNoMapsToDownload => new TranslatableString(getKey(@"playlist_no_maps_to_download"), @"There are no maps to download in the playlist");
        /// <summary>
        /// "Download all beatmaps ({0}/{1}, {2} active, {3} queued)"
        /// </summary>
        public static LocalisableString PlaylistDownloadProgress(int local, int total, int active, int queued) => new TranslatableString(getKey(@"playlist_download_progress"), @"Download all beatmaps ({0}/{1}, {2} active, {3} queued)", local, total, active, queued);
        /// <summary>
        /// "Download all beatmaps ({0} sets not downloaded yet)"
        /// </summary>
        public static LocalisableString PlaylistMissingSets(int missing) => new TranslatableString(getKey(@"playlist_missing_sets"), @"Download all beatmaps ({0} sets not downloaded yet)", missing);
        /// <summary>
        /// "All playlist beatmaps are already downloaded"
        /// </summary>
        public static LocalisableString PlaylistAllDownloaded => new TranslatableString(getKey(@"playlist_all_downloaded"), @"All playlist beatmaps are already downloaded");
        /// <summary>
        /// "Download official beatmaps directly from osu!"
        /// </summary>
        public static LocalisableString UseOfficialOsuBeatmapServiceCaption => new TranslatableString(getKey(@"use_official_osu_beatmap_service_caption"), @"Download official beatmaps directly from osu!");
        /// <summary>
        /// "Uses the official lazer token from game.ini for beatmap search, metadata and downloads. Scores and realtime services remain connected to Morasooma."
        /// </summary>
        public static LocalisableString UseOfficialOsuBeatmapServiceHint => new TranslatableString(getKey(@"use_official_osu_beatmap_service_hint"), @"Uses the official lazer token from game.ini for beatmap search, metadata and downloads. Scores and realtime services remain connected to Morasooma.");
        /// <summary>
        /// "This feature uses the official osu! account found in game.ini. Morasooma is not responsible for possible account restrictions. Account data and the token are not sent to Morasooma servers; the client uses them locally and sends the token only to official osu! services for these requests."
        /// </summary>
        public static LocalisableString UseOfficialOsuBeatmapServiceWarning => new TranslatableString(getKey(@"use_official_osu_beatmap_service_warning"), @"This feature uses the official osu! account found in game.ini. Morasooma is not responsible for possible account restrictions. Account data and the token are not sent to Morasooma servers; the client uses them locally and sends the token only to official osu! services for these requests.");
        /// <summary>
        /// "Official osu! beatmap account"
        /// </summary>
        public static LocalisableString OfficialOsuAccountStatusCaption => new TranslatableString(getKey(@"official_osu_account_status_caption"), @"Official osu! beatmap account");
        /// <summary>
        /// "Official osu! beatmap account: {0}"
        /// </summary>
        public static LocalisableString OfficialOsuAccountStatus(LocalisableString status) => new TranslatableString(getKey(@"official_osu_account_status"), @"Official osu! beatmap account: {0}", status);
        /// <summary>
        /// "Disabled"
        /// </summary>
        public static LocalisableString OfficialOsuStatusDisabled => new TranslatableString(getKey(@"official_osu_status_disabled"), @"Disabled");
        /// <summary>
        /// "No token in game.ini"
        /// </summary>
        public static LocalisableString OfficialOsuStatusTokenMissing => new TranslatableString(getKey(@"official_osu_status_token_missing"), @"No token in game.ini");
        /// <summary>
        /// "Checking account..."
        /// </summary>
        public static LocalisableString OfficialOsuStatusConnecting => new TranslatableString(getKey(@"official_osu_status_connecting"), @"Checking account...");
        /// <summary>
        /// "Connected: {0}"
        /// </summary>
        public static LocalisableString OfficialOsuStatusConnected(LocalisableString username) => new TranslatableString(getKey(@"official_osu_status_connected"), @"Connected: {0}", username);
        /// <summary>
        /// "Authorisation failed"
        /// </summary>
        public static LocalisableString OfficialOsuStatusAuthenticationFailed => new TranslatableString(getKey(@"official_osu_status_authentication_failed"), @"Authorisation failed");
        /// <summary>
        /// "osu! unavailable"
        /// </summary>
        public static LocalisableString OfficialOsuStatusNetworkUnavailable => new TranslatableString(getKey(@"official_osu_status_network_unavailable"), @"osu! unavailable");
        /// <summary>
        /// "Retry"
        /// </summary>
        public static LocalisableString OfficialOsuRetryButton => new TranslatableString(getKey(@"official_osu_retry_button"), @"Retry");
        /// <summary>
        /// "Official osu! beatmap integration: no token was found in game.ini."
        /// </summary>
        public static LocalisableString OfficialOsuTokenMissing => new TranslatableString(getKey(@"official_osu_token_missing"), @"Official osu! beatmap integration: no token was found in game.ini.");
        /// <summary>
        /// "Official osu! beatmap integration: the token stored in game.ini is invalid."
        /// </summary>
        public static LocalisableString OfficialOsuTokenInvalid => new TranslatableString(getKey(@"official_osu_token_invalid"), @"Official osu! beatmap integration: the token stored in game.ini is invalid.");
        /// <summary>
        /// "Official osu! beatmap integration could not authorise the account. Open official lazer and sign in again."
        /// </summary>
        public static LocalisableString OfficialOsuAuthenticationFailed => new TranslatableString(getKey(@"official_osu_authentication_failed"), @"Official osu! beatmap integration could not authorise the account. Open official lazer and sign in again.");
        /// <summary>
        /// "Official osu! beatmap integration could not reach osu!. Morasooma will continue using its normal beatmap source."
        /// </summary>
        public static LocalisableString OfficialOsuNetworkUnavailable => new TranslatableString(getKey(@"official_osu_network_unavailable"), @"Official osu! beatmap integration could not reach osu!. Morasooma will continue using its normal beatmap source.");
        /// <summary>
        /// "Without FPS limit"
        /// </summary>
        public static LocalisableString UncappedFpsCaption => new TranslatableString(getKey(@"uncapped_fps_caption"), @"Without FPS limit");
        /// <summary>
        /// "Enables uncapped frame rate mode and removes the built-in 1000 FPS ceiling. Usually this only increases heat and system load with no noticeable benefit once low frame times are reached."
        /// </summary>
        public static LocalisableString UncappedFpsHint => new TranslatableString(getKey(@"uncapped_fps_hint"), @"Enables uncapped frame rate mode and removes the built-in 1000 FPS ceiling. Usually this only increases heat and system load with no noticeable benefit once low frame times are reached.");
        /// <summary>
        /// "Limit menu FPS to 2× refresh rate"
        /// </summary>
        public static LocalisableString LimitMenuFps2xCaption => new TranslatableString(getKey(@"limit_menu_fps_2x_caption"), @"Limit menu FPS to 2× refresh rate");
        /// <summary>
        /// "Caps the frame rate in menus and song select to twice your monitor&apos;s refresh rate (e.g. 120 FPS at 60Hz or 280 FPS at 140Hz) to reduce heat and GPU load. Automatically unlocks during gameplay and replay playback."
        /// </summary>
        public static LocalisableString LimitMenuFps2xHint => new TranslatableString(getKey(@"limit_menu_fps_2x_hint"), @"Caps the frame rate in menus and song select to twice your monitor's refresh rate (e.g. 120 FPS at 60Hz or 280 FPS at 140Hz) to reduce heat and GPU load. Automatically unlocks during gameplay and replay playback.");
        /// <summary>
        /// "Windows ultra latency mode"
        /// </summary>
        public static LocalisableString WindowsUltraPerfCaption => new TranslatableString(getKey(@"windows_ultra_perf_caption"), @"Windows ultra latency mode");
        /// <summary>
        /// "Aggressive mode for Windows: sets process priority to realtime, enables MMCSS/Highest for input, update and draw threads, requests a system timer resolution below 1ms, and automatically applies basic Windows optimizations."
        /// </summary>
        public static LocalisableString WindowsUltraPerfHint => new TranslatableString(getKey(@"windows_ultra_perf_hint"), @"Aggressive mode for Windows: sets process priority to realtime, enables MMCSS/Highest for input, update and draw threads, requests a system timer resolution below 1ms, and automatically applies basic Windows optimizations.");
        /// <summary>
        /// "Enable 8000Hz input polling"
        /// </summary>
        public static LocalisableString Use8kPollingRateCaption => new TranslatableString(getKey(@"use_8k_polling_rate_caption"), @"Enable 8000Hz input polling");
        /// <summary>
        /// "Sets input thread limit to 8000Hz instead of 1000Hz. Increases CPU load."
        /// </summary>
        public static LocalisableString Use8kPollingRateHint => new TranslatableString(getKey(@"use_8k_polling_rate_hint"), @"Sets input thread limit to 8000Hz instead of 1000Hz. Increases CPU load.");
        /// <summary>
        /// "Aggressive song carousel performance mode"
        /// </summary>
        public static LocalisableString CarouselPerformanceModeCaption => new TranslatableString(getKey(@"carousel_performance_mode_caption"), @"Aggressive song carousel performance mode");
        /// <summary>
        /// "Uses lightweight map cards and removes long carousel transitions, rounded masking, and animated effects. Map card previews are controlled independently by the setting below."
        /// </summary>
        public static LocalisableString CarouselPerformanceModeHint => new TranslatableString(getKey(@"carousel_performance_mode_hint"), @"Uses lightweight map cards and removes long carousel transitions, rounded masking, and animated effects. Map card previews are controlled independently by the setting below.");
        /// <summary>
        /// "Show map card previews"
        /// </summary>
        public static LocalisableString CarouselPreviewsCaption => new TranslatableString(getKey(@"carousel_previews_caption"), @"Show map card previews");
        /// <summary>
        /// "Loads beatmap backgrounds into map cards and keeps the 64 most recently viewed previews cached. Disable this independently to minimise loading work and GPU memory usage."
        /// </summary>
        public static LocalisableString CarouselPreviewsHint => new TranslatableString(getKey(@"carousel_previews_hint"), @"Loads beatmap backgrounds into map cards and keeps the 64 most recently viewed previews cached. Disable this independently to minimise loading work and GPU memory usage.");
        /// <summary>
        /// "Lazy-load map card previews"
        /// </summary>
        public static LocalisableString CarouselLazyLoadingCaption => new TranslatableString(getKey(@"carousel_lazy_loading_caption"), @"Lazy-load map card previews");
        /// <summary>
        /// "Uses Torii-style staggered loading: visible previews wait at least 200 ms and more distant cards wait longer, reducing simultaneous texture decoding while scrolling."
        /// </summary>
        public static LocalisableString CarouselLazyLoadingHint => new TranslatableString(getKey(@"carousel_lazy_loading_hint"), @"Uses staggered loading: visible previews wait at least 200 ms and more distant cards wait longer, reducing simultaneous texture decoding while scrolling.");
        /// <summary>
        /// "Map card preview resolution"
        /// </summary>
        public static LocalisableString CarouselPreviewResolutionCaption => new TranslatableString(getKey(@"carousel_preview_resolution_caption"), @"Map card preview resolution");
        /// <summary>
        /// "Experimental. Downscales each cropped preview before GPU upload. Lower values reduce texture memory and bandwidth; 100% preserves the current quality."
        /// </summary>
        public static LocalisableString CarouselPreviewResolutionHint => new TranslatableString(getKey(@"carousel_preview_resolution_hint"), @"Experimental. Downscales each cropped preview before GPU upload. Lower values reduce texture memory and bandwidth; 100% preserves the current quality.");
        /// <summary>
        /// "Skin performance mode"
        /// </summary>
        public static LocalisableString SkinPerfCaption => new TranslatableString(getKey(@"skin_perf_caption"), @"Skin performance mode");
        /// <summary>
        /// "Freezes animations of legacy skins and disables expensive HUD elements like Argon wireframes, glow, rolling counters, and pop effects. Argon hit circles use a simplified body without the hit flash/explosion animation."
        /// </summary>
        public static LocalisableString SkinPerfHint => new TranslatableString(getKey(@"skin_perf_hint"), @"Freezes animations of legacy skins and disables expensive HUD elements like Argon wireframes, glow, rolling counters, and pop effects. Argon hit circles use a simplified body without the hit flash/explosion animation.");
        /// <summary>
        /// "Detailed performance optimisation settings"
        /// </summary>
        public static LocalisableString PerformanceOptimisationSettingsButton => new TranslatableString(getKey(@"performance_optimisation_settings_button"), @"Detailed performance optimisation settings");
        /// <summary>
        /// "Performance optimisation"
        /// </summary>
        public static LocalisableString PerformanceOptimisationSettingsHeader => new TranslatableString(getKey(@"performance_optimisation_settings_header"), @"Performance optimisation");
        /// <summary>
        /// "Green means recommended, blue means optional, orange means test first, purple means situational, and red means leave disabled."
        /// </summary>
        public static LocalisableString PerformanceOptimisationSettingsDescription => new TranslatableString(getKey(@"performance_optimisation_settings_description"), @"Green means recommended, blue means optional, orange means test first, purple means situational, and red means leave disabled.");
        /// <summary>
        /// "Audio latency"
        /// </summary>
        public static LocalisableString AudioLatencySettingsHeader => new TranslatableString(getKey(@"audio_latency_settings_header"), @"Audio latency");
        /// <summary>
        /// "Extreme low-latency audio"
        /// </summary>
        public static LocalisableString ExclusiveAudioCaption => new TranslatableString(getKey(@"exclusive_audio_caption"), @"Extreme low-latency audio");
        /// <summary>
        /// "Uses exclusive WASAPI access on Windows to minimise the output queue."
        /// </summary>
        public static LocalisableString ExclusiveAudioHint => new TranslatableString(getKey(@"exclusive_audio_hint"), @"Uses exclusive WASAPI access on Windows to minimise the output queue.");
        /// <summary>
        /// "Only during gameplay"
        /// </summary>
        public static LocalisableString ExclusiveAudioGameplayOnlyCaption => new TranslatableString(getKey(@"exclusive_audio_gameplay_only_caption"), @"Only during gameplay");
        /// <summary>
        /// "Releases exclusive access in menus and enables it while loading or playing a beatmap."
        /// </summary>
        public static LocalisableString ExclusiveAudioGameplayOnlyHint => new TranslatableString(getKey(@"exclusive_audio_gameplay_only_hint"), @"Releases exclusive access in menus and enables it while loading or playing a beatmap.");
        /// <summary>
        /// "EXTREME MODE. Morasooma takes exclusive control of the selected output device. Other applications cannot use it, and Windows or those applications may redirect sound to another device, including speakers. Exclusive access is released automatically when Morasooma loses focus. Your microphone is unaffected. Unsupported drivers may cause crackling, dropouts, or no audio."
        /// </summary>
        public static LocalisableString ExclusiveAudioWarning => new TranslatableString(getKey(@"exclusive_audio_warning"), @"EXTREME MODE. Morasooma takes exclusive control of the selected output device. Other applications cannot use it, and Windows or those applications may redirect sound to another device, including speakers. Exclusive access is released automatically when Morasooma loses focus. Your microphone is unaffected. Unsupported drivers may cause crackling, dropouts, or no audio.");
        /// <summary>
        /// "Latency: {0}"
        /// </summary>
        public static LocalisableString CurrentAudioLatency(LocalisableString latency) => new TranslatableString(getKey(@"current_audio_latency"), @"Latency: {0}", latency);
        /// <summary>
        /// "Latency: measuring..."
        /// </summary>
        public static LocalisableString AudioLatencyMeasuring => new TranslatableString(getKey(@"audio_latency_measuring"), @"Latency: measuring...");
        /// <summary>
        /// "Renderer and latency"
        /// </summary>
        public static LocalisableString RendererOptimisationSettingsHeader => new TranslatableString(getKey(@"renderer_optimisation_settings_header"), @"Renderer and latency");
        /// <summary>
        /// "Song carousel"
        /// </summary>
        public static LocalisableString CarouselOptimisationSettingsHeader => new TranslatableString(getKey(@"carousel_optimisation_settings_header"), @"Song carousel");
        /// <summary>
        /// "HIGH GAIN"
        /// </summary>
        public static LocalisableString PerformanceImpactHigh => new TranslatableString(getKey(@"performance_impact_high"), @"HIGH GAIN");
        /// <summary>
        /// "MEDIUM GAIN"
        /// </summary>
        public static LocalisableString PerformanceImpactMedium => new TranslatableString(getKey(@"performance_impact_medium"), @"MEDIUM GAIN");
        /// <summary>
        /// "SMALL GAIN"
        /// </summary>
        public static LocalisableString PerformanceImpactLow => new TranslatableString(getKey(@"performance_impact_low"), @"SMALL GAIN");
        /// <summary>
        /// "LOWER LATENCY"
        /// </summary>
        public static LocalisableString PerformanceImpactLatency => new TranslatableString(getKey(@"performance_impact_latency"), @"LOWER LATENCY");
        /// <summary>
        /// "TRADE-OFF"
        /// </summary>
        public static LocalisableString PerformanceImpactMixed => new TranslatableString(getKey(@"performance_impact_mixed"), @"TRADE-OFF");
        /// <summary>
        /// "SITUATIONAL"
        /// </summary>
        public static LocalisableString PerformanceImpactContextual => new TranslatableString(getKey(@"performance_impact_contextual"), @"SITUATIONAL");
        /// <summary>
        /// "SLOWER"
        /// </summary>
        public static LocalisableString PerformanceImpactNegative => new TranslatableString(getKey(@"performance_impact_negative"), @"SLOWER");
        /// <summary>
        /// "Recommended: the improvement can be noticeable."
        /// </summary>
        public static LocalisableString PerformanceImpactGuidanceHigh => new TranslatableString(getKey(@"performance_impact_guidance_high"), @"Recommended: the improvement can be noticeable.");
        /// <summary>
        /// "Recommended: mainly improves frame-time stability rather than the average FPS counter."
        /// </summary>
        public static LocalisableString PerformanceImpactGuidanceMedium => new TranslatableString(getKey(@"performance_impact_guidance_medium"), @"Recommended: mainly improves frame-time stability rather than the average FPS counter.");
        /// <summary>
        /// "Optional: the improvement is small and may not be noticeable."
        /// </summary>
        public static LocalisableString PerformanceImpactGuidanceLow => new TranslatableString(getKey(@"performance_impact_guidance_low"), @"Optional: the improvement is small and may not be noticeable.");
        /// <summary>
        /// "For minimum latency only: may increase CPU usage or slightly reduce average FPS."
        /// </summary>
        public static LocalisableString PerformanceImpactGuidanceLatency => new TranslatableString(getKey(@"performance_impact_guidance_latency"), @"For minimum latency only: may increase CPU usage or slightly reduce average FPS.");
        /// <summary>
        /// "Test on your PC first: one metric improved while another became worse."
        /// </summary>
        public static LocalisableString PerformanceImpactGuidanceMixed => new TranslatableString(getKey(@"performance_impact_guidance_mixed"), @"Test on your PC first: one metric improved while another became worse.");
        /// <summary>
        /// "Enable only when the described hardware or display use case applies."
        /// </summary>
        public static LocalisableString PerformanceImpactGuidanceContextual => new TranslatableString(getKey(@"performance_impact_guidance_contextual"), @"Enable only when the described hardware or display use case applies.");
        /// <summary>
        /// "Leave disabled: this isolated test became slower."
        /// </summary>
        public static LocalisableString PerformanceImpactGuidanceNegative => new TranslatableString(getKey(@"performance_impact_guidance_negative"), @"Leave disabled: this isolated test became slower.");
        /// <summary>
        /// "{0}: {1}{3}Morasooma controlled benchmark result: {2}"
        /// </summary>
        public static LocalisableString PerformanceImpactDescription(LocalisableString impact, LocalisableString guidance, LocalisableString evidence) =>
            new TranslatableString(getKey(@"performance_impact_description"), @"{0}: {1}{3}Morasooma controlled benchmark result: {2}", impact, guidance, evidence, "\n");
        /// <summary>
        /// "average FPS -1.0%; the slowest Update frames improved by 12%."
        /// </summary>
        public static LocalisableString PerformanceEvidenceWindowsUltra => new TranslatableString(getKey(@"performance_evidence_windows_ultra"), @"average FPS -1.0%; the slowest Update frames improved by 12%.");
        /// <summary>
        /// "average FPS -0.8%; the slowest input-thread frames improved by 13% in the replay workload."
        /// </summary>
        public static LocalisableString PerformanceEvidenceInput8k => new TranslatableString(getKey(@"performance_evidence_input_8k"), @"average FPS -0.8%; the slowest input-thread frames improved by 13% in the replay workload.");
        /// <summary>
        /// "with map card previews disabled: average FPS +126%, slowest-1% FPS +232%, Draw p99 -75%, and Update p99 -77% in the controlled Arrow Right carousel benchmark."
        /// </summary>
        public static LocalisableString PerformanceEvidenceCarousel => new TranslatableString(getKey(@"performance_evidence_carousel"), @"with map card previews disabled: average FPS +126%, slowest-1% FPS +232%, Draw p99 -75%, and Update p99 -77% in the controlled Arrow Right carousel benchmark.");
        /// <summary>
        /// "average FPS -0.5%, with several severe frame-time spikes on the tested integrated GPU."
        /// </summary>
        public static LocalisableString PerformanceEvidenceAtlas4096 => new TranslatableString(getKey(@"performance_evidence_atlas_4096"), @"average FPS -0.5%, with several severe frame-time spikes on the tested integrated GPU.");
        /// <summary>
        /// "average FPS -1.8%; the slowest Draw and Update frames were about 28% worse."
        /// </summary>
        public static LocalisableString PerformanceEvidenceDeferredVertexBatching => new TranslatableString(getKey(@"performance_evidence_deferred_vertex_batching"), @"average FPS -1.8%; the slowest Draw and Update frames were about 28% worse.");
        /// <summary>
        /// "average FPS -4.0%, while the slowest Draw and Update frames improved by about 13%."
        /// </summary>
        public static LocalisableString PerformanceEvidenceDeferredDirectVertex => new TranslatableString(getKey(@"performance_evidence_deferred_direct_vertex"), @"average FPS -4.0%, while the slowest Draw and Update frames improved by about 13%.");
        /// <summary>
        /// "average FPS -0.7%; the slowest Update frames improved by 5%."
        /// </summary>
        public static LocalisableString PerformanceEvidenceDeferredDirectUniform => new TranslatableString(getKey(@"performance_evidence_deferred_direct_uniform"), @"average FPS -0.7%; the slowest Update frames improved by 5%.");
        /// <summary>
        /// "average FPS -1.6%; the slowest Draw frames improved by 14%, but Update frames became 20% worse."
        /// </summary>
        public static LocalisableString PerformanceEvidencePipelineCache => new TranslatableString(getKey(@"performance_evidence_pipeline_cache"), @"average FPS -1.6%; the slowest Draw frames improved by 14%, but Update frames became 20% worse.");
        /// <summary>
        /// "child lifetime-check stalls improved by 78%, but average FPS fell by 4.2% in this scene."
        /// </summary>
        public static LocalisableString PerformanceEvidenceStaticLifetime => new TranslatableString(getKey(@"performance_evidence_static_lifetime"), @"child lifetime-check stalls improved by 78%, but average FPS fell by 4.2% in this scene.");
        /// <summary>
        /// "not yet benchmarked in this fork; intended to cut atlas resets and texture count on workloads with many short-lived textures."
        /// </summary>
        public static LocalisableString PerformanceEvidenceAtlasRegionReuse => new TranslatableString(getKey(@"performance_evidence_atlas_region_reuse"), @"not yet benchmarked in this fork; intended to cut atlas resets and texture count on workloads with many short-lived textures.");
        /// <summary>
        /// "required for uncapped presentation in borderless mode; the fullscreen replay result was inconclusive."
        /// </summary>
        public static LocalisableString PerformanceEvidenceAllowTearing => new TranslatableString(getKey(@"performance_evidence_allow_tearing"), @"required for uncapped presentation in borderless mode; the fullscreen replay result was inconclusive.");
        /// <summary>
        /// "average FPS was unchanged; the slowest Update and input-thread frames improved by 8% and 12%."
        /// </summary>
        public static LocalisableString PerformanceEvidenceSpinWait => new TranslatableString(getKey(@"performance_evidence_spin_wait"), @"average FPS was unchanged; the slowest Update and input-thread frames improved by 8% and 12%.");
        /// <summary>
        /// "combined mode improved the slowest Draw frames by 21%; individual components previously reached double-digit FPS gains."
        /// </summary>
        public static LocalisableString PerformanceEvidenceSkinMaster => new TranslatableString(getKey(@"performance_evidence_skin_master"), @"combined mode improved the slowest Draw frames by 21%; individual components previously reached double-digit FPS gains.");
        /// <summary>
        /// "average FPS with the classic skin was unchanged; the slowest Draw frames improved by 10%."
        /// </summary>
        public static LocalisableString PerformanceEvidenceSkinFreezeAnimations => new TranslatableString(getKey(@"performance_evidence_skin_freeze_animations"), @"average FPS with the classic skin was unchanged; the slowest Draw frames improved by 10%.");
        /// <summary>
        /// "average FPS stayed within normal variance; slowest-1% FPS improved by 1.6%."
        /// </summary>
        public static LocalisableString PerformanceEvidenceSkinSimplifyEffects => new TranslatableString(getKey(@"performance_evidence_skin_simplify_effects"), @"average FPS stayed within normal variance; slowest-1% FPS improved by 1.6%.");
        /// <summary>
        /// "average FPS with the classic skin was unchanged; the slowest Draw and Update frames improved by 21% and 31%."
        /// </summary>
        public static LocalisableString PerformanceEvidenceSkinOptimiseTextures => new TranslatableString(getKey(@"performance_evidence_skin_optimise_textures"), @"average FPS with the classic skin was unchanged; the slowest Draw and Update frames improved by 21% and 31%.");
        /// <summary>
        /// "the slowest Draw and Update frames improved by 24% and 21%; slowest-1% FPS improved by 1.8%."
        /// </summary>
        public static LocalisableString PerformanceEvidenceSkinSimplifyHud => new TranslatableString(getKey(@"performance_evidence_skin_simplify_hud"), @"the slowest Draw and Update frames improved by 24% and 21%; slowest-1% FPS improved by 1.8%.");
        /// <summary>
        /// "average FPS stayed within normal variance; slowest-1% FPS improved by 1.4%."
        /// </summary>
        public static LocalisableString PerformanceEvidenceSkinSimplifyCounters => new TranslatableString(getKey(@"performance_evidence_skin_simplify_counters"), @"average FPS stayed within normal variance; slowest-1% FPS improved by 1.4%.");
        /// <summary>
        /// "the slowest Update frames with the classic skin improved by 29%; slowest-1% FPS improved by 3.1%."
        /// </summary>
        public static LocalisableString PerformanceEvidenceSkinDisableKiai => new TranslatableString(getKey(@"performance_evidence_skin_disable_kiai"), @"the slowest Update frames with the classic skin improved by 29%; slowest-1% FPS improved by 3.1%.");
        /// <summary>
        /// "slowest-1% FPS improved by 5.3% in this run; a prior controlled integrated-GPU test measured 14-16% higher average FPS."
        /// </summary>
        public static LocalisableString PerformanceEvidenceSkinBlackBackground => new TranslatableString(getKey(@"performance_evidence_skin_black_background"), @"slowest-1% FPS improved by 5.3% in this run; a prior controlled integrated-GPU test measured 14-16% higher average FPS.");
        /// <summary>
        /// "average FPS +1.1%, slowest-1% FPS +5.9%, and the slowest Draw frames improved by 18%."
        /// </summary>
        public static LocalisableString PerformanceEvidenceArgonFollowRing => new TranslatableString(getKey(@"performance_evidence_argon_follow_ring"), @"average FPS +1.1%, slowest-1% FPS +5.9%, and the slowest Draw frames improved by 18%.");
        /// <summary>
        /// "{0} · {1} impact"
        /// </summary>
        public static LocalisableString PerformanceImpactCaption(LocalisableString caption, LocalisableString impact) =>
            new TranslatableString(getKey(@"performance_impact_caption"), @"{0} · {1} impact", caption, impact);
        /// <summary>
        /// "Isolated replay benchmark (2026-07-27): {0}"
        /// </summary>
        public static LocalisableString PerformanceImpactEvidence(LocalisableString evidence) =>
            new TranslatableString(getKey(@"performance_impact_evidence"), @"Isolated replay benchmark (2026-07-27): {0}", evidence);
        /// <summary>
        /// "Skin performance optimisation"
        /// </summary>
        public static LocalisableString SkinPerfSettingsHeader => new TranslatableString(getKey(@"skin_perf_settings_header"), @"Skin performance optimisation");
        /// <summary>
        /// "Choose which optimisations are used while skin performance mode is enabled. Changes take effect immediately."
        /// </summary>
        public static LocalisableString SkinPerfSettingsDescription => new TranslatableString(getKey(@"skin_perf_settings_description"), @"Choose which optimisations are used while skin performance mode is enabled. Changes take effect immediately.");
        /// <summary>
        /// "Freeze legacy skin animations"
        /// </summary>
        public static LocalisableString SkinPerfFreezeAnimationsCaption => new TranslatableString(getKey(@"skin_perf_freeze_animations_caption"), @"Freeze legacy skin animations");
        /// <summary>
        /// "Shows the first frame of animated legacy skin elements instead of playing their animations."
        /// </summary>
        public static LocalisableString SkinPerfFreezeAnimationsHint => new TranslatableString(getKey(@"skin_perf_freeze_animations_hint"), @"Shows the first frame of animated legacy skin elements instead of playing their animations.");
        /// <summary>
        /// "Simplify judgement effects"
        /// </summary>
        public static LocalisableString SkinPerfSimplifyEffectsCaption => new TranslatableString(getKey(@"skin_perf_simplify_effects_caption"), @"Simplify judgement effects");
        /// <summary>
        /// "Disables judgement particles and uses a shorter fade to reduce animation work."
        /// </summary>
        public static LocalisableString SkinPerfSimplifyEffectsHint => new TranslatableString(getKey(@"skin_perf_simplify_effects_hint"), @"Disables judgement particles and uses a shorter fade to reduce animation work.");
        /// <summary>
        /// "Optimise legacy skin textures"
        /// </summary>
        public static LocalisableString SkinPerfOptimiseTexturesCaption => new TranslatableString(getKey(@"skin_perf_optimise_textures_caption"), @"Optimise legacy skin textures");
        /// <summary>
        /// "Uses lower-resolution non-critical legacy textures when available and reduces their upload size."
        /// </summary>
        public static LocalisableString SkinPerfOptimiseTexturesHint => new TranslatableString(getKey(@"skin_perf_optimise_textures_hint"), @"Uses lower-resolution non-critical legacy textures when available and reduces their upload size.");
        /// <summary>
        /// "Simplify gameplay HUD animations"
        /// </summary>
        public static LocalisableString SkinPerfSimplifyHudCaption => new TranslatableString(getKey(@"skin_perf_simplify_hud_caption"), @"Simplify gameplay HUD animations");
        /// <summary>
        /// "Removes HUD transition animations and expensive visual updates during gameplay."
        /// </summary>
        public static LocalisableString SkinPerfSimplifyHudHint => new TranslatableString(getKey(@"skin_perf_simplify_hud_hint"), @"Removes HUD transition animations and expensive visual updates during gameplay.");
        /// <summary>
        /// "Simplify rolling counters"
        /// </summary>
        public static LocalisableString SkinPerfSimplifyCountersCaption => new TranslatableString(getKey(@"skin_perf_simplify_counters_caption"), @"Simplify rolling counters");
        /// <summary>
        /// "Updates score, pp and unstable-rate counters without rolling-number animations."
        /// </summary>
        public static LocalisableString SkinPerfSimplifyCountersHint => new TranslatableString(getKey(@"skin_perf_simplify_counters_hint"), @"Updates score, pp and unstable-rate counters without rolling-number animations.");
        /// <summary>
        /// "Disable legacy Kiai flashing"
        /// </summary>
        public static LocalisableString SkinPerfDisableKiaiFlashingCaption => new TranslatableString(getKey(@"skin_perf_disable_kiai_flashing_caption"), @"Disable legacy Kiai flashing");
        /// <summary>
        /// "Prevents duplicated legacy skin textures and beat-synchronised flashing during Kiai sections."
        /// </summary>
        public static LocalisableString SkinPerfDisableKiaiFlashingHint => new TranslatableString(getKey(@"skin_perf_disable_kiai_flashing_hint"), @"Prevents duplicated legacy skin textures and beat-synchronised flashing during Kiai sections.");

        /// <summary>
        /// "Black background at high dim"
        /// </summary>
        public static LocalisableString SkinPerfBlackBackgroundCaption => new TranslatableString(getKey(@"skin_perf_black_background_caption"), @"Black background at high dim");
        /// <summary>
        /// "Skips drawing the beatmap background entirely when background dim is 75% or higher. The largest single FPS gain on integrated GPUs (about +14% measured)."
        /// </summary>
        public static LocalisableString SkinPerfBlackBackgroundHint => new TranslatableString(getKey(@"skin_perf_black_background_hint"), @"Skips drawing the beatmap background entirely when background dim is 75% or higher. The largest single FPS gain on integrated GPUs (about +14% measured).");
        /// <summary>
        /// "Ring-only slider follow circle"
        /// </summary>
        public static LocalisableString SkinPerfArgonFollowRingCaption => new TranslatableString(getKey(@"skin_perf_argon_follow_ring_caption"), @"Ring-only slider follow circle");
        /// <summary>
        /// "Draws the Argon slider follow circle as a ring without its translucent interior fill, reducing GPU load while tracking sliders. Takes effect on the next gameplay start."
        /// </summary>
        public static LocalisableString SkinPerfArgonFollowRingHint => new TranslatableString(getKey(@"skin_perf_argon_follow_ring_hint"), @"Draws the Argon slider follow circle as a ring without its translucent interior fill, reducing GPU load while tracking sliders. Takes effect on the next gameplay start.");
        /// <summary>
        /// "Use a separate skin for each game mode"
        /// </summary>
        public static LocalisableString SeparateSkinsPerRulesetCaption => new TranslatableString(getKey(@"separate_skins_per_ruleset_caption"), @"Use a separate skin for each game mode");
        /// <summary>
        /// "Selects and remembers a different skin for Standard, Taiko, Catch and Mania."
        /// </summary>
        public static LocalisableString SeparateSkinsPerRulesetHint => new TranslatableString(getKey(@"separate_skins_per_ruleset_hint"), @"Selects and remembers a different skin for Standard, Taiko, Catch and Mania.");
        /// <summary>
        /// "Standard skin"
        /// </summary>
        public static LocalisableString OsuSkinCaption => new TranslatableString(getKey(@"osu_skin_caption"), @"Standard skin");
        /// <summary>
        /// "Taiko skin"
        /// </summary>
        public static LocalisableString TaikoSkinCaption => new TranslatableString(getKey(@"taiko_skin_caption"), @"Taiko skin");
        /// <summary>
        /// "Catch skin"
        /// </summary>
        public static LocalisableString CatchSkinCaption => new TranslatableString(getKey(@"catch_skin_caption"), @"Catch skin");
        /// <summary>
        /// "Mania skin"
        /// </summary>
        public static LocalisableString ManiaSkinCaption => new TranslatableString(getKey(@"mania_skin_caption"), @"Mania skin");
        /// <summary>
        /// "Dodge skin"
        /// </summary>
        public static LocalisableString DodgeSkinCaption => new TranslatableString(getKey(@"dodge_skin_caption"), @"Dodge skin");
        /// <summary>
        /// "Large texture atlas (4096)"
        /// </summary>
        public static LocalisableString LargeTextureAtlasCaption => new TranslatableString(getKey(@"large_texture_atlas_caption"), @"Large texture atlas (4096)");
        /// <summary>
        /// "Packs more UI and gameplay textures into each GPU page, reducing texture switches and draw calls. Requires a restart. Disable if an older GPU or OpenGL driver shows texture corruption or excessive memory usage."
        /// </summary>
        public static LocalisableString LargeTextureAtlasHint => new TranslatableString(getKey(@"large_texture_atlas_hint"), @"Packs more UI and gameplay textures into each GPU page, reducing texture switches and draw calls. Requires a restart. Disable if an older GPU or OpenGL driver shows texture corruption or excessive memory usage.");
        /// <summary>
        /// "Experimental atlas region reuse"
        /// </summary>
        public static LocalisableString AtlasRegionAllocatorCaption => new TranslatableString(getKey(@"atlas_region_allocator_caption"), @"Experimental atlas region reuse");
        /// <summary>
        /// "Frees and reuses texture atlas regions when textures are purged, instead of only appending new ones. Reduces atlas resets and total GPU texture count on workloads with many short-lived textures. Experimental; results may vary by GPU and driver."
        /// </summary>
        public static LocalisableString AtlasRegionAllocatorHint => new TranslatableString(getKey(@"atlas_region_allocator_hint"), @"Frees and reuses texture atlas regions when textures are purged, instead of only appending new ones. Reduces atlas resets and total GPU texture count on workloads with many short-lived textures. Experimental; results may vary by GPU and driver.");
        /// <summary>
        /// "Gameplay render scale"
        /// </summary>
        public static LocalisableString GameplayRenderScaleCaption => new TranslatableString(getKey(@"gameplay_render_scale_caption"), @"Gameplay render scale");
        /// <summary>
        /// "Renders the osu! playfield at a lower resolution while keeping the cursor and interface at native resolution. Very low values heavily reduce visual clarity. At 100% the offscreen rendering path is fully disabled."
        /// </summary>
        public static LocalisableString GameplayRenderScaleHint => new TranslatableString(getKey(@"gameplay_render_scale_hint"), @"Renders the osu! playfield at a lower resolution while keeping the cursor and interface at native resolution. Very low values heavily reduce visual clarity. At 100% the offscreen rendering path is fully disabled.");
        /// <summary>
        /// "Apply recommended settings"
        /// </summary>
        public static LocalisableString RecommendedRendererPresetBtn => new TranslatableString(getKey(@"recommended_renderer_preset_btn"), @"Apply recommended settings");
        /// <summary>
        /// "Experimental Deferred vertex batching"
        /// </summary>
        public static LocalisableString DeferredVertexBatchingCaption => new TranslatableString(getKey(@"deferred_vertex_batching_caption"), @"Experimental Deferred vertex batching");
        /// <summary>
        /// "Combines adjacent vertex upload events in the Deferred renderer. Results may vary by GPU and driver; leave disabled unless benchmarks show an improvement."
        /// </summary>
        public static LocalisableString DeferredVertexBatchingHint => new TranslatableString(getKey(@"deferred_vertex_batching_hint"), @"Combines adjacent vertex upload events in the Deferred renderer. Results may vary by GPU and driver; leave disabled unless benchmarks show an improvement.");
        /// <summary>
        /// "Experimental direct Deferred vertex upload"
        /// </summary>
        public static LocalisableString DeferredDirectVertexUploadCaption => new TranslatableString(getKey(@"deferred_direct_vertex_upload_caption"), @"Experimental direct Deferred vertex upload");
        /// <summary>
        /// "Writes completed primitives straight into mapped GPU vertex buffers, bypassing the intermediate CPU copy and upload-event stream. Results may vary by graphics backend and driver."
        /// </summary>
        public static LocalisableString DeferredDirectVertexUploadHint => new TranslatableString(getKey(@"deferred_direct_vertex_upload_hint"), @"Writes completed primitives straight into mapped GPU vertex buffers, bypassing the intermediate CPU copy and upload-event stream. Results may vary by graphics backend and driver.");
        /// <summary>
        /// "Experimental direct Deferred uniform upload"
        /// </summary>
        public static LocalisableString DeferredDirectUniformUploadCaption => new TranslatableString(getKey(@"deferred_direct_uniform_upload_caption"), @"Experimental direct Deferred uniform upload");
        /// <summary>
        /// "Writes uniform values straight into mapped GPU buffers and skips the Deferred upload pre-pass when possible. Results may vary by graphics backend and driver."
        /// </summary>
        public static LocalisableString DeferredDirectUniformUploadHint => new TranslatableString(getKey(@"deferred_direct_uniform_upload_hint"), @"Writes uniform values straight into mapped GPU buffers and skips the Deferred upload pre-pass when possible. Results may vary by graphics backend and driver.");
        /// <summary>
        /// "Experimental Veldrid pipeline lookup cache"
        /// </summary>
        public static LocalisableString VeldridPipelineLookupCacheCaption => new TranslatableString(getKey(@"veldrid_pipeline_lookup_cache_caption"), @"Experimental Veldrid pipeline lookup cache");
        /// <summary>
        /// "Reuses the resolved graphics pipeline while shader, layout, framebuffer and fixed-function state are unchanged, avoiding a full descriptor hash-table lookup for every draw call."
        /// </summary>
        public static LocalisableString VeldridPipelineLookupCacheHint => new TranslatableString(getKey(@"veldrid_pipeline_lookup_cache_hint"), @"Reuses the resolved graphics pipeline while shader, layout, framebuffer and fixed-function state are unchanged, avoiding a full descriptor hash-table lookup for every draw call.");
        /// <summary>
        /// "Experimental predictive scene lifetime cache"
        /// </summary>
        public static LocalisableString StaticChildLifetimeCacheCaption => new TranslatableString(getKey(@"static_child_lifetime_cache_caption"), @"Experimental predictive scene lifetime cache");
        /// <summary>
        /// "Skips repeated child-life checks until the next known LifetimeStart/LifetimeEnd boundary. Disable if a custom drawable appears or disappears incorrectly."
        /// </summary>
        public static LocalisableString StaticChildLifetimeCacheHint => new TranslatableString(getKey(@"static_child_lifetime_cache_hint"), @"Skips repeated child-life checks until the next known LifetimeStart/LifetimeEnd boundary. Disable if a custom drawable appears or disappears incorrectly.");
        /// <summary>
        /// "Performance logging"
        /// </summary>
        public static LocalisableString PerfLoggingCaption => new TranslatableString(getKey(@"perf_logging_caption"), @"Performance logging");
        /// <summary>
        /// "When enabled, writes FPS, frame time, CPU, memory, and gameplay state samples to the performance folder."
        /// </summary>
        public static LocalisableString PerfLoggingHint => new TranslatableString(getKey(@"perf_logging_hint"), @"When enabled, writes FPS, frame time, CPU, memory, and gameplay state samples to the performance folder.");
        /// <summary>
        /// "Allow screen tearing (DXGI Tearing)"
        /// </summary>
        public static LocalisableString AllowTearingCaption => new TranslatableString(getKey(@"allow_tearing_caption"), @"Allow screen tearing (DXGI Tearing)");
        /// <summary>
        /// "Enables DXGI Present Allow Tearing support to unlock frame rate in windowed/borderless modes and activate G-Sync/FreeSync without VSync."
        /// </summary>
        public static LocalisableString AllowTearingHint => new TranslatableString(getKey(@"allow_tearing_hint"), @"Enables DXGI Present Allow Tearing support to unlock frame rate in windowed/borderless modes and activate G-Sync/FreeSync without VSync.");
        /// <summary>
        /// "Spin-Wait mode for Update thread"
        /// </summary>
        public static LocalisableString UpdateSpinWaitCaption => new TranslatableString(getKey(@"update_spin_wait_caption"), @"Spin-Wait mode for Update thread");
        /// <summary>
        /// "Completely disables sleeping for the Update thread, forcing it to wait for the next frame in a busy loop (100% load on one core). Reduces latency and increases timing stability."
        /// </summary>
        public static LocalisableString UpdateSpinWaitHint => new TranslatableString(getKey(@"update_spin_wait_hint"), @"Completely disables sleeping for the Update thread, forcing it to wait for the next frame in a busy loop (100% load on one core). Reduces latency and increases timing stability.");
        /// <summary>
        /// "Performance Diagnostics"
        /// </summary>
        public static LocalisableString DiagnosticsBenchmarkBtn => new TranslatableString(getKey(@"diagnostics_benchmark_btn"), @"Performance Diagnostics");
        /// <summary>
        /// "Performance Diagnostics"
        /// </summary>
        public static LocalisableString DiagnosticsOverlayTitle => new TranslatableString(getKey(@"diagnostics_overlay_title"), @"Performance Diagnostics");
        /// <summary>
        /// "Every stage plays the complete replay once for warm-up and twice for measurement. Full diagnostics covers every top-level performance setting; settings that cannot apply to the current platform or renderer are listed explicitly instead of disappearing."
        /// </summary>
        public static LocalisableString DiagnosticsOverlayDescription => new TranslatableString(getKey(@"diagnostics_overlay_description"), @"Every stage plays the complete replay once for warm-up and twice for measurement. Full diagnostics covers every top-level performance setting; settings that cannot apply to the current platform or renderer are listed explicitly instead of disappearing.");
        /// <summary>
        /// "Test mode"
        /// </summary>
        public static LocalisableString DiagnosticsModeSelectionTitle => new TranslatableString(getKey(@"diagnostics_mode_selection_title"), @"Test mode");
        /// <summary>
        /// "Quick check · about 4–5 minutes"
        /// </summary>
        public static LocalisableString DiagnosticsQuickModeButton => new TranslatableString(getKey(@"diagnostics_quick_mode_button"), @"Quick check · about 4–5 minutes");
        /// <summary>
        /// "Full diagnostics · about 24–27 minutes"
        /// </summary>
        public static LocalisableString DiagnosticsDeepModeButton => new TranslatableString(getKey(@"diagnostics_deep_mode_button"), @"Full diagnostics · about 24–27 minutes");
        /// <summary>
        /// "Legacy full diagnostics · about 24–27 minutes"
        /// </summary>
        public static LocalisableString DiagnosticsExtendedModeButton => new TranslatableString(getKey(@"diagnostics_extended_mode_button"), @"Legacy full diagnostics · about 24–27 minutes");
        /// <summary>
        /// "Renderer comparison · about 2 minutes each"
        /// </summary>
        public static LocalisableString DiagnosticsRendererModeButton => new TranslatableString(getKey(@"diagnostics_renderer_mode_button"), @"Renderer comparison · about 2 minutes each");
        /// <summary>
        /// "Compares the current and Morasooma recommended profiles on the selected renderer. Each stage plays the complete replay once for warm-up and twice for measurement."
        /// </summary>
        public static LocalisableString DiagnosticsQuickModeDescription => new TranslatableString(getKey(@"diagnostics_quick_mode_description"), @"Compares the current and Morasooma recommended profiles on the selected renderer. Each stage plays the complete replay once for warm-up and twice for measurement.");
        /// <summary>
        /// "Tests every top-level renderer, latency and skin-performance setting in isolation, then the complete recommended profile and a final baseline for drift correction. Skin performance is one combined package. Inapplicable settings remain visible with an explicit reason."
        /// </summary>
        public static LocalisableString DiagnosticsDeepModeDescription => new TranslatableString(getKey(@"diagnostics_deep_mode_description"), @"Tests every top-level renderer, latency and skin-performance setting in isolation, then the complete recommended profile and a final baseline for drift correction. Skin performance is one combined package. Inapplicable settings remain visible with an explicit reason.");
        /// <summary>
        /// "Legacy name for the same complete diagnostics plan."
        /// </summary>
        public static LocalisableString DiagnosticsExtendedModeDescription => new TranslatableString(getKey(@"diagnostics_extended_mode_description"), @"Legacy name for the same complete diagnostics plan.");
        /// <summary>
        /// "Runs the complete Morasooma recommended profile on each available renderer. Each renderer plays the complete replay once for warm-up and twice for measurement."
        /// </summary>
        public static LocalisableString DiagnosticsRendererModeDescription => new TranslatableString(getKey(@"diagnostics_renderer_mode_description"), @"Runs the complete Morasooma recommended profile on each available renderer. Each renderer plays the complete replay once for warm-up and twice for measurement.");
        /// <summary>
        /// "Use default skin"
        /// </summary>
        public static LocalisableString DiagnosticsUseDefaultSkinCaption => new TranslatableString(getKey(@"diagnostics_use_default_skin_caption"), @"Use default skin");
        /// <summary>
        /// "If enabled, the built-in Argon skin is used consistently for every stage. If disabled, the currently selected skin is kept. Your original skin is restored when diagnostics ends."
        /// </summary>
        public static LocalisableString DiagnosticsUseDefaultSkinHint => new TranslatableString(getKey(@"diagnostics_use_default_skin_hint"), @"If enabled, the built-in Argon skin is used consistently for every stage. If disabled, the currently selected skin is kept. Your original skin is restored when diagnostics ends.");
        /// <summary>
        /// "Test with skin performance mode"
        /// </summary>
        public static LocalisableString DiagnosticsSkinPerfCaption => new TranslatableString(getKey(@"diagnostics_skin_perf_caption"), @"Test with skin performance mode");
        /// <summary>
        /// "Enables ForkSkinPerformanceMode only for the duration of the diagnostics."
        /// </summary>
        public static LocalisableString DiagnosticsSkinPerfHint => new TranslatableString(getKey(@"diagnostics_skin_perf_hint"), @"Enables ForkSkinPerformanceMode only for the duration of the diagnostics.");
        /// <summary>
        /// "Export results to ZIP"
        /// </summary>
        public static LocalisableString DiagnosticsExportZipCaption => new TranslatableString(getKey(@"diagnostics_export_zip_caption"), @"Export results to ZIP");
        /// <summary>
        /// "Creates a support-ready ZIP with a readable report, build and machine information, stutters, raw CSVs, manifests, and runtime logs. The folder opens automatically after completion."
        /// </summary>
        public static LocalisableString DiagnosticsExportZipHint => new TranslatableString(getKey(@"diagnostics_export_zip_hint"), @"Creates a support-ready ZIP with a readable report, build and machine information, stutters, raw CSVs, manifests, and runtime logs. The folder opens automatically after completion.");
        /// <summary>
        /// "Start diagnostics"
        /// </summary>
        public static LocalisableString DiagnosticsStartButton => new TranslatableString(getKey(@"diagnostics_start_button"), @"Start diagnostics");
        /// <summary>
        /// "Resume diagnostics"
        /// </summary>
        public static LocalisableString DiagnosticsResumeButton => new TranslatableString(getKey(@"diagnostics_resume_button"), @"Resume diagnostics");
        /// <summary>
        /// "Cancel diagnostics"
        /// </summary>
        public static LocalisableString DiagnosticsCancelButton => new TranslatableString(getKey(@"diagnostics_cancel_button"), @"Cancel diagnostics");
        /// <summary>
        /// "Close"
        /// </summary>
        public static LocalisableString DiagnosticsCloseButton => new TranslatableString(getKey(@"diagnostics_close_button"), @"Close");
        /// <summary>
        /// "Quick"
        /// </summary>
        public static LocalisableString DiagnosticsModeQuick => new TranslatableString(getKey(@"diagnostics_mode_quick"), @"Quick");
        /// <summary>
        /// "Full diagnostics"
        /// </summary>
        public static LocalisableString DiagnosticsModeDeep => new TranslatableString(getKey(@"diagnostics_mode_deep"), @"Full diagnostics");
        /// <summary>
        /// "Extended research"
        /// </summary>
        public static LocalisableString DiagnosticsModeExtended => new TranslatableString(getKey(@"diagnostics_mode_extended"), @"Extended research");
        /// <summary>
        /// "Renderer comparison"
        /// </summary>
        public static LocalisableString DiagnosticsModeRenderers => new TranslatableString(getKey(@"diagnostics_mode_renderers"), @"Renderer comparison");
        /// <summary>
        /// "Diagnostics in progress"
        /// </summary>
        public static LocalisableString DiagnosticsProgressTitle => new TranslatableString(getKey(@"diagnostics_progress_title"), @"Diagnostics in progress");
        /// <summary>
        /// "{2} mode · completed {0} of {1} stages"
        /// </summary>
        public static LocalisableString DiagnosticsProgressSummary(LocalisableString completed, LocalisableString total, LocalisableString mode) =>
            new TranslatableString(getKey(@"diagnostics_progress_summary"), @"{2} mode · completed {0} of {1} stages", completed, total, mode);
        /// <summary>
        /// "Current stage"
        /// </summary>
        public static LocalisableString DiagnosticsCurrentStepTitle => new TranslatableString(getKey(@"diagnostics_current_step_title"), @"Current stage");
        /// <summary>
        /// "{0} · {1} · stage {2}/{3}"
        /// </summary>
        public static LocalisableString DiagnosticsCurrentStep(LocalisableString renderer, LocalisableString profile, LocalisableString current, LocalisableString total) =>
            new TranslatableString(getKey(@"diagnostics_current_step"), @"{0} · {1} · stage {2}/{3}", renderer, profile, current, total);
        /// <summary>
        /// "The client may restart between stages. Do not change settings or run heavy applications until the results screen appears."
        /// </summary>
        public static LocalisableString DiagnosticsProgressHint => new TranslatableString(getKey(@"diagnostics_progress_hint"), @"The client may restart between stages. Do not change settings or run heavy applications until the results screen appears.");
        /// <summary>
        /// "Estimated time remaining: about {0} min"
        /// </summary>
        public static LocalisableString DiagnosticsProgressRemaining(LocalisableString minutes) =>
            new TranslatableString(getKey(@"diagnostics_progress_remaining"), @"Estimated time remaining: about {0} min", minutes);
        /// <summary>
        /// "Diagnostics paused"
        /// </summary>
        public static LocalisableString DiagnosticsPausedTitle => new TranslatableString(getKey(@"diagnostics_paused_title"), @"Diagnostics paused");
        /// <summary>
        /// "The saved session was kept. {0} of {1} stages are complete. Resume from the current stage or explicitly cancel the diagnostics."
        /// </summary>
        public static LocalisableString DiagnosticsPausedDescription(LocalisableString completed, LocalisableString total) =>
            new TranslatableString(getKey(@"diagnostics_paused_description"), @"The saved session was kept. {0} of {1} stages are complete. Resume from the current stage or explicitly cancel the diagnostics.", completed, total);
        /// <summary>
        /// "Stage to resume"
        /// </summary>
        public static LocalisableString DiagnosticsPausedStageTitle => new TranslatableString(getKey(@"diagnostics_paused_stage_title"), @"Stage to resume");
        /// <summary>
        /// "Diagnostics Results"
        /// </summary>
        public static LocalisableString DiagnosticsResultsTitle => new TranslatableString(getKey(@"diagnostics_results_title"), @"Diagnostics Results");
        /// <summary>
        /// "Session {0}: {2} completed tests across {1} renderer(s)."
        /// </summary>
        public static LocalisableString DiagnosticsSessionSummary(LocalisableString sessionId, LocalisableString rendererCount, LocalisableString completedCount) =>
            new TranslatableString(getKey(@"diagnostics_session_summary"), @"Session {0}: {2} completed tests across {1} renderer(s).", sessionId, rendererCount, completedCount);
        /// <summary>
        /// "Stutters ({0} events)"
        /// </summary>
        public static LocalisableString DiagnosticsStuttersHeader(LocalisableString count) => new TranslatableString(getKey(@"diagnostics_stutters_header"), @"Stutters ({0} events)", count);
        /// <summary>
        /// "... and {0} more events (see ZIP/CSV)."
        /// </summary>
        public static LocalisableString DiagnosticsStuttersTruncated(LocalisableString remaining) => new TranslatableString(getKey(@"diagnostics_stutters_truncated"), @"... and {0} more events (see ZIP/CSV).", remaining);
        /// <summary>
        /// "ZIP export: {0}"
        /// </summary>
        public static LocalisableString DiagnosticsExportPath(LocalisableString path) => new TranslatableString(getKey(@"diagnostics_export_path"), @"ZIP export: {0}", path);
        /// <summary>
        /// "Open folder with ZIP"
        /// </summary>
        public static LocalisableString DiagnosticsOpenExportFolder => new TranslatableString(getKey(@"diagnostics_open_export_folder"), @"Open folder with ZIP");
        /// <summary>
        /// "No reliable recommendation"
        /// </summary>
        public static LocalisableString DiagnosticsNoRecommendationTitle => new TranslatableString(getKey(@"diagnostics_no_recommendation_title"), @"No reliable recommendation");
        /// <summary>
        /// "Not enough stable Current and Recommended runs were found. Repeat the diagnostics."
        /// </summary>
        public static LocalisableString DiagnosticsNoRecommendationBody => new TranslatableString(getKey(@"diagnostics_no_recommendation_body"), @"Not enough stable Current and Recommended runs were found. Repeat the diagnostics.");
        /// <summary>
        /// "Morasooma recommended profile performs better"
        /// </summary>
        public static LocalisableString DiagnosticsRecommendedWinsTitle => new TranslatableString(getKey(@"diagnostics_recommended_wins_title"), @"Morasooma recommended profile performs better");
        /// <summary>
        /// "Keep your current settings"
        /// </summary>
        public static LocalisableString DiagnosticsCurrentWinsTitle => new TranslatableString(getKey(@"diagnostics_current_wins_title"), @"Keep your current settings");
        /// <summary>
        /// "Both profiles perform similarly"
        /// </summary>
        public static LocalisableString DiagnosticsSimilarTitle => new TranslatableString(getKey(@"diagnostics_similar_title"), @"Both profiles perform similarly");
        /// <summary>
        /// "Renderer: {0}. Average FPS: {1}; slowest 1% FPS: {2}; stutter difference: {3}. Measurement quality: {4}."
        /// </summary>
        public static LocalisableString DiagnosticsComparisonSummary(LocalisableString renderer, LocalisableString avgDelta, LocalisableString p1Delta, LocalisableString stutterDelta, LocalisableString quality) =>
            new TranslatableString(getKey(@"diagnostics_comparison_summary"), @"Renderer: {0}. Average FPS: {1}; slowest 1% FPS: {2}; stutter difference: {3}. Measurement quality: {4}.", renderer, avgDelta, p1Delta, stutterDelta, quality);
        /// <summary>
        /// "Apply recommended profile"
        /// </summary>
        public static LocalisableString DiagnosticsApplyRecommendedButton => new TranslatableString(getKey(@"diagnostics_apply_recommended_button"), @"Apply recommended profile");
        /// <summary>
        /// "Current settings"
        /// </summary>
        public static LocalisableString DiagnosticsProfileCurrent => new TranslatableString(getKey(@"diagnostics_profile_current"), @"Current settings");
        /// <summary>
        /// "Complete Morasooma recommended profile"
        /// </summary>
        public static LocalisableString DiagnosticsProfileRecommended => new TranslatableString(getKey(@"diagnostics_profile_recommended"), @"Complete Morasooma recommended profile");
        /// <summary>
        /// "Clean baseline"
        /// </summary>
        public static LocalisableString DiagnosticsProfileBaseline => new TranslatableString(getKey(@"diagnostics_profile_baseline"), @"Clean baseline");
        /// <summary>
        /// "Classic skin baseline"
        /// </summary>
        public static LocalisableString DiagnosticsProfileClassicBaseline => new TranslatableString(getKey(@"diagnostics_profile_classic_baseline"), @"Classic skin baseline");
        /// <summary>
        /// "Complete skin performance package"
        /// </summary>
        public static LocalisableString DiagnosticsProfileSkinPackage => new TranslatableString(getKey(@"diagnostics_profile_skin_package"), @"Complete skin performance package");
        /// <summary>
        /// "Final clean baseline"
        /// </summary>
        public static LocalisableString DiagnosticsProfileVerificationBaseline => new TranslatableString(getKey(@"diagnostics_profile_verification_baseline"), @"Final clean baseline");
        /// <summary>
        /// "{0}: test failed"
        /// </summary>
        public static LocalisableString DiagnosticsProfileFailed(LocalisableString profile) =>
            new TranslatableString(getKey(@"diagnostics_profile_failed"), @"{0}: test failed", profile);
        /// <summary>
        /// "Not applicable on this system ({0})"
        /// </summary>
        public static LocalisableString DiagnosticsSkippedProfilesTitle(LocalisableString count) =>
            new TranslatableString(getKey(@"diagnostics_skipped_profiles_title"), @"Not applicable on this system ({0})", count);
        /// <summary>
        /// "{0} on {1}: {2}"
        /// </summary>
        public static LocalisableString DiagnosticsSkippedProfile(LocalisableString profile, LocalisableString renderer, LocalisableString reason) =>
            new TranslatableString(getKey(@"diagnostics_skipped_profile"), @"{0} on {1}: {2}", profile, renderer, reason);
        /// <summary>
        /// "requires Windows"
        /// </summary>
        public static LocalisableString DiagnosticsSkipRequiresWindows => new TranslatableString(getKey(@"diagnostics_skip_requires_windows"), @"requires Windows");
        /// <summary>
        /// "only affects the Deferred renderer"
        /// </summary>
        public static LocalisableString DiagnosticsSkipRequiresDeferredRenderer => new TranslatableString(getKey(@"diagnostics_skip_requires_deferred_renderer"), @"only affects the Deferred renderer");
        /// <summary>
        /// "only affects the non-Deferred Veldrid renderer"
        /// </summary>
        public static LocalisableString DiagnosticsSkipRequiresNonDeferredRenderer => new TranslatableString(getKey(@"diagnostics_skip_requires_non_deferred_renderer"), @"only affects the non-Deferred Veldrid renderer");
        /// <summary>
        /// "Scope: measures the CPU and input-thread cost of 8000 Hz scheduling during replay playback; it does not measure mouse sensor or USB latency."
        /// </summary>
        public static LocalisableString DiagnosticsInputPollingScope => new TranslatableString(getKey(@"diagnostics_input_polling_scope"), @"Scope: measures the CPU and input-thread cost of 8000 Hz scheduling during replay playback; it does not measure mouse sensor or USB latency.");
        /// <summary>
        /// "Scope: measures this presentation option in the current Windows display mode; its effect can depend on window mode, driver and VRR support."
        /// </summary>
        public static LocalisableString DiagnosticsTearingScope => new TranslatableString(getKey(@"diagnostics_tearing_scope"), @"Scope: measures this presentation option in the current Windows display mode; its effect can depend on window mode, driver and VRR support.");
        /// <summary>
        /// "{0}: average {1} FPS · slowest 1% {2} FPS · stutters {3} · {4}"
        /// </summary>
        public static LocalisableString DiagnosticsProfileResult(LocalisableString profile, LocalisableString avgFps, LocalisableString p1Fps, LocalisableString stutters, LocalisableString quality) =>
            new TranslatableString(getKey(@"diagnostics_profile_result"), @"{0}: average {1} FPS · slowest 1% {2} FPS · stutters {3} · {4}", profile, avgFps, p1Fps, stutters, quality);
        /// <summary>
        /// "reliable"
        /// </summary>
        public static LocalisableString DiagnosticsQualityReliable => new TranslatableString(getKey(@"diagnostics_quality_reliable"), @"reliable");
        /// <summary>
        /// "one complete measurement"
        /// </summary>
        public static LocalisableString DiagnosticsQualitySingleRun => new TranslatableString(getKey(@"diagnostics_quality_single_run"), @"one complete measurement");
        /// <summary>
        /// "variable"
        /// </summary>
        public static LocalisableString DiagnosticsQualityVariable => new TranslatableString(getKey(@"diagnostics_quality_variable"), @"variable");
        /// <summary>
        /// "high run-to-run variation"
        /// </summary>
        public static LocalisableString DiagnosticsQualityUnstable => new TranslatableString(getKey(@"diagnostics_quality_unstable"), @"high run-to-run variation");
        /// <summary>
        /// "failed"
        /// </summary>
        public static LocalisableString DiagnosticsQualityFailed => new TranslatableString(getKey(@"diagnostics_quality_failed"), @"failed");
        /// <summary>
        /// "{0}; measured-run spread: average {1}, slowest 1% {2}"
        /// </summary>
        public static LocalisableString DiagnosticsQualityDetails(LocalisableString quality, LocalisableString avgSpread, LocalisableString p1Spread) =>
            new TranslatableString(getKey(@"diagnostics_quality_details"), @"{0}; measured-run spread: average {1}, slowest 1% {2}", quality, avgSpread, p1Spread);
        /// <summary>
        /// "The clean baseline did not produce usable metrics. Repeat the deep test."
        /// </summary>
        public static LocalisableString DiagnosticsDeepBaselineMissing => new TranslatableString(getKey(@"diagnostics_deep_baseline_missing"), @"The clean baseline did not produce usable metrics. Repeat the deep test.");
        /// <summary>
        /// "The Classic skin baseline did not produce usable metrics. Legacy-only setting results are unavailable."
        /// </summary>
        public static LocalisableString DiagnosticsDeepClassicBaselineMissing => new TranslatableString(getKey(@"diagnostics_deep_classic_baseline_missing"), @"The Classic skin baseline did not produce usable metrics. Legacy-only setting results are unavailable.");
        /// <summary>
        /// "Clean baseline"
        /// </summary>
        public static LocalisableString DiagnosticsDeepBaselineTitle => new TranslatableString(getKey(@"diagnostics_deep_baseline_title"), @"Clean baseline");
        /// <summary>
        /// "Classic skin baseline"
        /// </summary>
        public static LocalisableString DiagnosticsDeepClassicBaselineTitle => new TranslatableString(getKey(@"diagnostics_deep_classic_baseline_title"), @"Classic skin baseline");
        /// <summary>
        /// "Performance drift check"
        /// </summary>
        public static LocalisableString DiagnosticsDeepVerificationBaselineTitle => new TranslatableString(getKey(@"diagnostics_deep_verification_baseline_title"), @"Performance drift check");
        /// <summary>
        /// "Final clean baseline: average {0} FPS · drift from the initial baseline {1} · {2}. Results between both baselines are adjusted gradually for this drift."
        /// </summary>
        public static LocalisableString DiagnosticsDeepVerificationBaselineResult(LocalisableString avgFps, LocalisableString drift, LocalisableString quality) =>
            new TranslatableString(getKey(@"diagnostics_deep_verification_baseline_result"), @"Final clean baseline: average {0} FPS · drift from the initial baseline {1} · {2}. Results between both baselines are adjusted gradually for this drift.", avgFps, drift, quality);
        /// <summary>
        /// "Baseline measurement variation"
        /// </summary>
        public static LocalisableString DiagnosticsDeepBaselineVariationTitle => new TranslatableString(getKey(@"diagnostics_deep_baseline_variation_title"), @"Baseline measurement variation");
        /// <summary>
        /// "Initial all-off baseline run spread: average {0}, slowest 1% {1}. Final baseline: average {2}, slowest 1% {3}. This mainly reduces confidence in small differences; it is reported once instead of marking every tested profile as unstable."
        /// </summary>
        public static LocalisableString DiagnosticsDeepBaselineVariation(
            LocalisableString initialAvgSpread,
            LocalisableString initialP1Spread,
            LocalisableString finalAvgSpread,
            LocalisableString finalP1Spread) =>
            new TranslatableString(
                getKey(@"diagnostics_deep_baseline_variation"),
                @"Initial all-off baseline run spread: average {0}, slowest 1% {1}. Final baseline: average {2}, slowest 1% {3}. This mainly reduces confidence in small differences; it is reported once instead of marking every tested profile as unstable.",
                initialAvgSpread,
                initialP1Spread,
                finalAvgSpread,
                finalP1Spread);
        /// <summary>
        /// "All tested optimisations disabled: average {0} FPS · slowest 1% {1} FPS · {2}"
        /// </summary>
        public static LocalisableString DiagnosticsDeepBaselineResult(LocalisableString avgFps, LocalisableString p1Fps, LocalisableString quality) =>
            new TranslatableString(getKey(@"diagnostics_deep_baseline_result"), @"All tested optimisations disabled: average {0} FPS · slowest 1% {1} FPS · {2}", avgFps, p1Fps, quality);
        /// <summary>
        /// "Every result is compared with the time-adjusted clean all-off baseline. The complete recommended profile is shown first as the aggregate result. Other independent optimisations are tested alone; skin performance is one package containing its master switch and every skin optimisation. A plus sign means an improvement."
        /// </summary>
        public static LocalisableString DiagnosticsDeepResultsHint => new TranslatableString(getKey(@"diagnostics_deep_results_hint"), @"Every result is compared with the time-adjusted clean all-off baseline. The complete recommended profile is shown first as the aggregate result. Other independent optimisations are tested alone; skin performance is one package containing its master switch and every skin optimisation. A plus sign means an improvement.");
        /// <summary>
        /// "Complete recommended profile vs all optimisations disabled"
        /// </summary>
        public static LocalisableString DiagnosticsDeepRecommendedComparisonTitle => new TranslatableString(getKey(@"diagnostics_deep_recommended_comparison_title"), @"Complete recommended profile vs all optimisations disabled");
        /// <summary>
        /// "{0} · average {1} vs {2} FPS ({3}) · slowest 1% {4} vs {5} FPS ({6}) · Draw work p99 {7} · Update work p99 {8} · Input p99 {9} · stutter difference {10} · candidate measurement: {11}"
        /// </summary>
        public static LocalisableString DiagnosticsDeepProfileResult(
            LocalisableString impact,
            LocalisableString avgFps,
            LocalisableString baselineAvgFps,
            LocalisableString avgDelta,
            LocalisableString p1Fps,
            LocalisableString baselineP1Fps,
            LocalisableString p1Delta,
            LocalisableString drawDelta,
            LocalisableString updateDelta,
            LocalisableString inputDelta,
            LocalisableString stutterDelta,
            LocalisableString quality) =>
            new TranslatableString(
                getKey(@"diagnostics_deep_profile_result"),
                @"{0} · average {1} vs {2} FPS ({3}) · slowest 1% {4} vs {5} FPS ({6}) · Draw work p99 {7} · Update work p99 {8} · Input p99 {9} · stutter difference {10} · candidate measurement: {11}",
                impact,
                avgFps,
                baselineAvgFps,
                avgDelta,
                p1Fps,
                baselineP1Fps,
                p1Delta,
                drawDelta,
                updateDelta,
                inputDelta,
                stutterDelta,
                quality);
        /// <summary>
        /// "Best renderer for the recommended profile"
        /// </summary>
        public static LocalisableString DiagnosticsRendererWinnerTitle => new TranslatableString(getKey(@"diagnostics_renderer_winner_title"), @"Best renderer for the recommended profile");
        /// <summary>
        /// "{0}: average {1} FPS · slowest 1% {2} FPS · stutters {3} · {4}"
        /// </summary>
        public static LocalisableString DiagnosticsRendererWinnerResult(
            LocalisableString renderer,
            LocalisableString avgFps,
            LocalisableString p1Fps,
            LocalisableString stutters,
            LocalisableString quality) =>
            new TranslatableString(getKey(@"diagnostics_renderer_winner_result"), @"{0}: average {1} FPS · slowest 1% {2} FPS · stutters {3} · {4}", renderer, avgFps, p1Fps, stutters, quality);
        /// <summary>
        /// "Worst event: {0} thread, {1} ms. The complete event list is available in the ZIP export."
        /// </summary>
        public static LocalisableString DiagnosticsStutterSummary(LocalisableString thread, LocalisableString frameMs) =>
            new TranslatableString(getKey(@"diagnostics_stutter_summary"), @"Worst event: {0} thread, {1} ms. The complete event list is available in the ZIP export.", thread, frameMs);
        /// <summary>
        /// "NO CLEAR CHANGE"
        /// </summary>
        public static LocalisableString DiagnosticsImpactNeutral => new TranslatableString(getKey(@"diagnostics_impact_neutral"), @"NO CLEAR CHANGE");
        /// <summary>
        /// "Do not send scores/replays to server"
        /// </summary>
        public static LocalisableString DisableOnlineRecordsCaption => new TranslatableString(getKey(@"disable_online_records_caption"), @"Do not send scores/replays to server");
        /// <summary>
        /// "Disables spectator and online submission for new plays. Local scores will still be saved."
        /// </summary>
        public static LocalisableString DisableOnlineRecordsHint => new TranslatableString(getKey(@"disable_online_records_hint"), @"Disables spectator and online submission for new plays. Local scores will still be saved.");
        /// <summary>
        /// "Visual OD11"
        /// </summary>
        public static LocalisableString VisualOD11Caption => new TranslatableString(getKey(@"visual_od11_caption"), @"Visual OD11");
        /// <summary>
        /// "The error meter and hit judgements will be displayed visually as if playing OD11."
        /// </summary>
        public static LocalisableString VisualOD11Hint => new TranslatableString(getKey(@"visual_od11_hint"), @"The error meter and hit judgements will be displayed visually as if playing OD11.");
        /// <summary>
        /// "Show failed taps on hit error meter"
        /// </summary>
        public static LocalisableString HitErrorMeterPositionalMissesCaption => new TranslatableString(getKey(@"hit_error_meter_positional_misses_caption"), @"Show failed taps on hit error meter");
        /// <summary>
        /// "Displays taps made outside a hit object as red markers on the hit error meter, similar to danser."
        /// </summary>
        public static LocalisableString HitErrorMeterPositionalMissesHint => new TranslatableString(getKey(@"hit_error_meter_positional_misses_hint"), @"Displays taps made outside a hit object as red markers on the hit error meter, similar to danser.");
        /// <summary>
        /// "Show mod icons under preset names"
        /// </summary>
        public static LocalisableString ShowModsInPresetListCaption => new TranslatableString(getKey(@"show_mods_in_preset_list_caption"), @"Show mod icons under preset names");
        /// <summary>
        /// "Replaces each preset description in the mod selection list with icons for the mods it contains."
        /// </summary>
        public static LocalisableString ShowModsInPresetListHint => new TranslatableString(getKey(@"show_mods_in_preset_list_hint"), @"Replaces each preset description in the mod selection list with icons for the mods it contains.");
        /// <summary>
        /// "Enhanced ranking rows"
        /// </summary>
        public static LocalisableString EnhancedRankingRowsCaption => new TranslatableString(getKey(@"enhanced_ranking_rows_caption"), @"Enhanced ranking rows");
        /// <summary>
        /// "Shows player avatars and uses slightly larger rows and text in ranking tables."
        /// </summary>
        public static LocalisableString EnhancedRankingRowsHint => new TranslatableString(getKey(@"enhanced_ranking_rows_hint"), @"Shows player avatars and uses slightly larger rows and text in ranking tables.");
        /// <summary>
        /// "Use osu!stable directory directly (Experimental)"
        /// </summary>
        public static LocalisableString UseStableDirectoryCaption => new TranslatableString(getKey(@"use_stable_directory_caption"), @"Use osu!stable directory directly (Experimental)");
        /// <summary>
        /// "Reads beatmaps directly from osu!stable without copying files. Changes are applied without restarting the game."
        /// </summary>
        public static LocalisableString UseStableDirectoryHint => new TranslatableString(getKey(@"use_stable_directory_hint"), @"Reads beatmaps directly from osu!stable without copying files. Changes are applied without restarting the game.");
        /// <summary>
        /// "Custom osu!stable directory"
        /// </summary>
        public static LocalisableString CustomStableDirectoryCaption => new TranslatableString(getKey(@"custom_stable_directory_caption"), @"Custom osu!stable directory");
        /// <summary>
        /// "Leave empty to auto-detect. The path is applied after pressing Enter or leaving the field."
        /// </summary>
        public static LocalisableString CustomStableDirectoryHint => new TranslatableString(getKey(@"custom_stable_directory_hint"), @"Leave empty to auto-detect. The path is applied after pressing Enter or leaving the field.");
        /// <summary>
        /// "C:\Users\Username\AppData\Local\osu!"
        /// </summary>
        public static LocalisableString CustomStableDirectoryPlaceholder => new TranslatableString(getKey(@"custom_stable_directory_placeholder"), @"C:\Users\Username\AppData\Local\osu!");
        /// <summary>
        /// "Custom username"
        /// </summary>
        public static LocalisableString CustomUsernameCaption => new TranslatableString(getKey(@"custom_username_caption"), @"Custom username");
        /// <summary>
        /// "Displayed in the menu and in locally saved scores. Leave empty to use account username."
        /// </summary>
        public static LocalisableString CustomUsernameHint => new TranslatableString(getKey(@"custom_username_hint"), @"Displayed in the menu and in locally saved scores. Leave empty to use account username.");
        /// <summary>
        /// "Empty = account username"
        /// </summary>
        public static LocalisableString CustomUsernamePlaceholder => new TranslatableString(getKey(@"custom_username_placeholder"), @"Empty = account username");

        /// <summary>
        /// "Leave a Morasooma smoke tag after maps"
        /// </summary>
        public static LocalisableString MorasoomaEndTagCaption => new TranslatableString(getKey(@"morasooma_end_tag_caption"), @"Leave a Morasooma smoke tag after maps");

        /// <summary>
        /// "Quickly draws MORASOOMA with cursor smoke after the final judgement. The movement and smoke input are saved in the replay."
        /// </summary>
        public static LocalisableString MorasoomaEndTagHint => new TranslatableString(getKey(@"morasooma_end_tag_hint"), @"Quickly draws MORASOOMA with cursor smoke after the final judgement. The movement and smoke input are saved in the replay.");
        /// <summary>
        /// "Override recommended difficulty"
        /// </summary>
        public static LocalisableString OverrideRecDiffCaption => new TranslatableString(getKey(@"override_rec_diff_caption"), @"Override recommended difficulty");
        /// <summary>
        /// "If enabled, the client uses this exact star rating directly, disregarding PP and other parameters."
        /// </summary>
        public static LocalisableString OverrideRecDiffHint => new TranslatableString(getKey(@"override_rec_diff_hint"), @"If enabled, the client uses this exact star rating directly, disregarding PP and other parameters.");
        /// <summary>
        /// "Custom recommended difficulty"
        /// </summary>
        public static LocalisableString CustomRecDiffCaption => new TranslatableString(getKey(@"custom_rec_diff_caption"), @"Custom recommended difficulty");
        /// <summary>
        /// "Legacy previews in beatmap carousel"
        /// </summary>
        public static LocalisableString OldCarouselPreviewCaption => new TranslatableString(getKey(@"old_carousel_preview_caption"), @"Legacy previews in beatmap carousel");
        /// <summary>
        /// "Switches the cards in song select to an older layout: the preview is fixed to a 16:9 block on the left edge, and extra background effects are removed."
        /// </summary>
        public static LocalisableString OldCarouselPreviewHint => new TranslatableString(getKey(@"old_carousel_preview_hint"), @"Switches the cards in song select to an older layout: the preview is fixed to a 16:9 block on the left edge, and extra background effects are removed.");
        /// <summary>
        /// "Skin-based legacy beatmap carousel"
        /// </summary>
        public static LocalisableString SkinnedLegacyCarouselCaption => new TranslatableString(getKey(@"skinned_legacy_carousel_caption"), @"Skin-based legacy beatmap carousel");
        /// <summary>
        /// "Uses the current skin&apos;s menu-button-background for beatmap cards and enables the classic preview layout."
        /// </summary>
        public static LocalisableString SkinnedLegacyCarouselHint => new TranslatableString(getKey(@"skinned_legacy_carousel_hint"), @"Uses the current skin's menu-button-background for beatmap cards and enables the classic preview layout.");
        /// <summary>
        /// "2024 (v1) song select"
        /// </summary>
        public static LocalisableString V1CarouselCaption => new TranslatableString(getKey(@"v1_song_select_caption"), @"2024 (v1) song select");
        /// <summary>
        /// "Restores the 2024-era song select screen: expanding carousel panels, beatmap info wedge, leaderboard and details tabs, classic footer and search bar. Applies immediately."
        /// </summary>
        public static LocalisableString V1CarouselHint => new TranslatableString(getKey(@"v1_song_select_hint"), @"Restores the 2024-era song select screen: expanding carousel panels, beatmap info wedge, leaderboard and details tabs, classic footer and search bar. Applies immediately.");
        /// <summary>
        /// "Song select style"
        /// </summary>
        public static LocalisableString SongSelectStyleCaption => new TranslatableString(getKey(@"song_select_style_caption"), @"Song select style");
        /// <summary>
        /// "Which visual style song select uses. Applies immediately."
        /// </summary>
        public static LocalisableString SongSelectStyleHint => new TranslatableString(getKey(@"song_select_style_hint"), @"Which visual style song select uses. Applies immediately.");
        /// <summary>
        /// "Modern"
        /// </summary>
        public static LocalisableString SongSelectStyleModern => new TranslatableString(getKey(@"song_select_style_modern"), @"Modern");
        /// <summary>
        /// "2024 (classic)"
        /// </summary>
        public static LocalisableString SongSelectStyleClassic2024 => new TranslatableString(getKey(@"song_select_style_classic_2024"), @"2024 (classic)");
        /// <summary>
        /// "Legacy (skinned, stable-style)"
        /// </summary>
        public static LocalisableString SongSelectStyleLegacySkinned => new TranslatableString(getKey(@"song_select_style_legacy_skinned"), @"Legacy (skinned, stable-style)");
        /// <summary>
        /// "Infinite Glass (zoomable map)"
        /// </summary>
        public static LocalisableString SongSelectStyleInfiniteGlass => new TranslatableString(getKey(@"song_select_style_infinite_glass"), @"Infinite Glass (zoomable map)");
        /// <summary>
        /// "Auto-hide top toolbar"
        /// </summary>
        public static LocalisableString AutoHideToolbarCaption => new TranslatableString(getKey(@"auto_hide_toolbar_caption"), @"Auto-hide top toolbar");
        /// <summary>
        /// "Keeps the toolbar hidden, reveals it at the top edge, then hides it again after the cursor leaves."
        /// </summary>
        public static LocalisableString AutoHideToolbarHint => new TranslatableString(getKey(@"auto_hide_toolbar_hint"), @"Keeps the toolbar hidden, reveals it when the cursor touches the top edge, then hides it again after the cursor leaves.");
        /// <summary>
        /// "Carousel background dim"
        /// </summary>
        public static LocalisableString CarouselBgDim => new TranslatableString(getKey(@"carousel_bg_dim"), @"Carousel background dim");
        /// <summary>
        /// "Storyboard/video background in song select"
        /// </summary>
        public static LocalisableString StoryboardBgCaption => new TranslatableString(getKey(@"storyboard_bg_caption"), @"Storyboard/video background in song select");
        /// <summary>
        /// "Shows the storyboard or video background of the selected beatmap on the song select screen. Only works if the beatmap has a storyboard or video."
        /// </summary>
        public static LocalisableString StoryboardBgHint => new TranslatableString(getKey(@"storyboard_bg_hint"), @"Shows the storyboard or video background of the selected beatmap on the song select screen. Only works if the beatmap has a storyboard or video.");
        /// <summary>
        /// "Render quality preset"
        /// </summary>
        public static LocalisableString ReplayQualityPresetCaption => new TranslatableString(getKey(@"replay_quality_preset_caption"), @"Render quality preset");
        /// <summary>
        /// "Default profile for replay render: affects target bitrate and ffmpeg preset. Fast = faster rendering, Quality = higher quality but slower."
        /// </summary>
        public static LocalisableString ReplayQualityPresetHint => new TranslatableString(getKey(@"replay_quality_preset_hint"), @"Default profile for replay render: affects target bitrate and ffmpeg preset. Fast = faster rendering, Quality = higher quality but slower.");
        /// <summary>
        /// "Render debug trace mode"
        /// </summary>
        public static LocalisableString ReplayTraceModeCaption => new TranslatableString(getKey(@"replay_trace_mode_caption"), @"Render debug trace mode");
        /// <summary>
        /// "Disabled does not create a debug file. Summary writes a short session summary and final stats. Full enables frame-by-frame tracing."
        /// </summary>
        public static LocalisableString ReplayTraceModeHint => new TranslatableString(getKey(@"replay_trace_mode_hint"), @"Disabled does not create a debug file. Summary writes a short session summary and final stats. Full enables frame-by-frame tracing.");
        /// <summary>
        /// "Export replay with clicks only"
        /// </summary>
        public static LocalisableString ExportClicksOnlyCaption => new TranslatableString(getKey(@"export_clicks_only_caption"), @"Export replay with clicks only");
        /// <summary>
        /// "When exporting to .osr, cursor movements without clicks are not saved (empty frames are removed)."
        /// </summary>
        public static LocalisableString ExportClicksOnlyHint => new TranslatableString(getKey(@"export_clicks_only_hint"), @"When exporting to .osr, cursor movements without clicks are not saved (empty frames are removed).");
        /// <summary>
        /// "Export all beatmaps"
        /// </summary>
        public static LocalisableString ExportAllBeatmapsBtn => new TranslatableString(getKey(@"export_all_beatmaps_btn"), @"Export all beatmaps");
        /// <summary>
        /// "This will export {0} beatmap sets."
        /// </summary>
        public static LocalisableString ExportAllBeatmapsConfirm(LocalisableString count) => new TranslatableString(getKey(@"export_all_beatmaps_confirm"), @"This will export {0} beatmap sets.", count);
        /// <summary>
        /// "Export beatmaps from collection"
        /// </summary>
        public static LocalisableString ExportCollectionBtn => new TranslatableString(getKey(@"export_collection_btn"), @"Export beatmaps from collection");
        /// <summary>
        /// "You do not have any collections created."
        /// </summary>
        public static LocalisableString ExportCollectionEmpty => new TranslatableString(getKey(@"export_collection_empty"), @"You do not have any collections created.");
        /// <summary>
        /// "There are no beatmaps to export in the selected collection."
        /// </summary>
        public static LocalisableString ExportCollectionMatchEmpty => new TranslatableString(getKey(@"export_collection_match_empty"), @"There are no beatmaps to export in the selected collection.");
        /// <summary>
        /// "Custom UI font"
        /// </summary>
        public static LocalisableString CustomUIFontCaption => new TranslatableString(getKey(@"custom_ui_font_caption"), @"Custom UI font");
        /// <summary>
        /// "Select a BMFont (.fnt + .png) to use for the user interface. Place font files in the Fonts folder."
        /// </summary>
        public static LocalisableString CustomUIFontHint => new TranslatableString(getKey(@"custom_ui_font_hint"), @"Select a BMFont (.fnt + .png) to use for the user interface. Place font files in the Fonts folder.");
        /// <summary>
        /// "Russian font fix"
        /// </summary>
        public static LocalisableString RussianFontFixCaption => new TranslatableString(getKey(@"russian_font_fix_caption"), @"Russian font fix");
        /// <summary>
        /// "Uses Comfortaa Regular for Cyrillic characters instead of the selected custom UI font."
        /// </summary>
        public static LocalisableString RussianFontFixHint => new TranslatableString(getKey(@"russian_font_fix_hint"), @"Uses Comfortaa Regular for Cyrillic characters instead of the selected custom UI font.");
        /// <summary>
        /// "Open fonts folder"
        /// </summary>
        public static LocalisableString FontsFolderBtn => new TranslatableString(getKey(@"fonts_folder_btn"), @"Open fonts folder");
        /// <summary>
        /// "Interface theme"
        /// </summary>
        public static LocalisableString ThemeModeCaption => new TranslatableString(getKey(@"theme_mode_caption"), @"Interface theme");
        /// <summary>
        /// "Changes the global color scheme of the game immediately."
        /// </summary>
        public static LocalisableString ThemeModeHint => new TranslatableString(getKey(@"theme_mode_hint"), @"Changes the global color scheme of the game immediately.");
        /// <summary>
        /// "Transparent overlays"
        /// </summary>
        public static LocalisableString OverlayTransparencyCaption => new TranslatableString(getKey(@"overlay_transparency_caption"), @"Transparent overlays");
        /// <summary>
        /// "Overlay backgrounds become see-through, with the game behind them blurred and tinted while an overlay is open."
        /// </summary>
        public static LocalisableString OverlayTransparencyHint => new TranslatableString(getKey(@"overlay_transparency_hint"), @"Overlay backgrounds become see-through, with the game behind them blurred and tinted while an overlay is open.");
        /// <summary>
        /// "Backdrop blur"
        /// </summary>
        public static LocalisableString OverlayBlurStrengthCaption => new TranslatableString(getKey(@"overlay_blur_strength_caption"), @"Backdrop blur");
        /// <summary>
        /// "How strongly the game content behind transparent overlays is blurred."
        /// </summary>
        public static LocalisableString OverlayBlurStrengthHint => new TranslatableString(getKey(@"overlay_blur_strength_hint"), @"How strongly the game content behind transparent overlays is blurred.");
        /// <summary>
        /// "Backdrop tint"
        /// </summary>
        public static LocalisableString OverlayDimCaption => new TranslatableString(getKey(@"overlay_dim_caption"), @"Backdrop tint");
        /// <summary>
        /// "Darkens (right) or lightens (left) the game content behind transparent overlays."
        /// </summary>
        public static LocalisableString OverlayDimHint => new TranslatableString(getKey(@"overlay_dim_hint"), @"Darkens (right) or lightens (left) the game content behind transparent overlays.");
        /// <summary>
        /// "Main menu logo"
        /// </summary>
        public static LocalisableString MenuLogoCaption => new TranslatableString(getKey(@"menu_logo_caption"), @"Main menu logo");
        /// <summary>
        /// "Selects the Morasooma logo shown in the main menu and other branded client surfaces."
        /// </summary>
        public static LocalisableString MenuLogoHint => new TranslatableString(getKey(@"menu_logo_hint"), @"Selects the Morasooma logo shown in the main menu and other branded client surfaces.");
        /// <summary>
        /// "mora text"
        /// </summary>
        public static LocalisableString MenuLogoMora => new TranslatableString(getKey(@"menu_logo_mora"), @"mora text");
        /// <summary>
        /// "Morasooma character"
        /// </summary>
        public static LocalisableString MenuLogoCharacter => new TranslatableString(getKey(@"menu_logo_character"), @"Morasooma character");
        /// <summary>
        /// "Random on every screen"
        /// </summary>
        public static LocalisableString MenuLogoRandom => new TranslatableString(getKey(@"menu_logo_random"), @"Random on every screen");
        /// <summary>
        /// "Logo circle gradient"
        /// </summary>
        public static LocalisableString MenuLogoGradientCaption => new TranslatableString(getKey(@"menu_logo_gradient_caption"), @"Logo circle gradient");
        /// <summary>
        /// "Selects the gradient inside the Morasooma logo circle. Random chooses a different gradient on every screen."
        /// </summary>
        public static LocalisableString MenuLogoGradientHint => new TranslatableString(getKey(@"menu_logo_gradient_hint"), @"Selects the gradient inside the Morasooma logo circle. Random chooses a different gradient on every screen.");
        /// <summary>
        /// "Classic plum"
        /// </summary>
        public static LocalisableString MenuLogoGradientClassic => new TranslatableString(getKey(@"menu_logo_gradient_classic"), @"Classic plum");
        /// <summary>
        /// "Morasooma Web"
        /// </summary>
        public static LocalisableString MenuLogoGradientWeb => new TranslatableString(getKey(@"menu_logo_gradient_web"), @"Morasooma Web");
        /// <summary>
        /// "Sunset"
        /// </summary>
        public static LocalisableString MenuLogoGradientSunset => new TranslatableString(getKey(@"menu_logo_gradient_sunset"), @"Sunset");
        /// <summary>
        /// "Ocean"
        /// </summary>
        public static LocalisableString MenuLogoGradientOcean => new TranslatableString(getKey(@"menu_logo_gradient_ocean"), @"Ocean");
        /// <summary>
        /// "Aurora"
        /// </summary>
        public static LocalisableString MenuLogoGradientAurora => new TranslatableString(getKey(@"menu_logo_gradient_aurora"), @"Aurora");
        /// <summary>
        /// "Triangles inside logo"
        /// </summary>
        public static LocalisableString MenuLogoTrianglesCaption => new TranslatableString(getKey(@"menu_logo_triangles_caption"), @"Triangles inside logo");
        /// <summary>
        /// "Shows animated triangles over the selected logo gradient."
        /// </summary>
        public static LocalisableString MenuLogoTrianglesHint => new TranslatableString(getKey(@"menu_logo_triangles_hint"), @"Shows animated triangles over the selected logo gradient.");
        /// <summary>
        /// "Select a preset..."
        /// </summary>
        public static LocalisableString PresetNone => new TranslatableString(getKey(@"preset_none"), @"Select a preset...");
        /// <summary>
        /// "Select one of the profiles to instantly apply a set of aim and relax settings."
        /// </summary>
        public static LocalisableString PresetNoneSummary => new TranslatableString(getKey(@"preset_none_summary"), @"Select one of the profiles to instantly apply a set of aim and relax settings.");
        /// <summary>
        /// "Soft"
        /// </summary>
        public static LocalisableString PresetSoft => new TranslatableString(getKey(@"preset_soft"), @"Soft");
        /// <summary>
        /// "Gives more freedom to the hand, weaker aim lock, soft humanized relax."
        /// </summary>
        public static LocalisableString PresetSoftSummary => new TranslatableString(getKey(@"preset_soft_summary"), @"Gives more freedom to the hand, weaker aim lock, soft humanized relax.");
        /// <summary>
        /// "Balanced"
        /// </summary>
        public static LocalisableString PresetBalanced => new TranslatableString(getKey(@"preset_balanced"), @"Balanced");
        /// <summary>
        /// "Universal profile: moderate assist and smooth relax."
        /// </summary>
        public static LocalisableString PresetBalancedSummary => new TranslatableString(getKey(@"preset_balanced_summary"), @"Universal profile: moderate assist and smooth relax.");
        /// <summary>
        /// "Sticky"
        /// </summary>
        public static LocalisableString PresetSticky => new TranslatableString(getKey(@"preset_sticky"), @"Sticky");
        /// <summary>
        /// "Holds the target strongly, triggers earlier, confidently hits jumps."
        /// </summary>
        public static LocalisableString PresetStickySummary => new TranslatableString(getKey(@"preset_sticky_summary"), @"Holds the target strongly, triggers earlier, confidently hits jumps.");
        /// <summary>
        /// "Preset &apos;{0}&apos; applied. {1}"
        /// </summary>
        public static LocalisableString PresetNoteApplied(LocalisableString name, LocalisableString summary) => new TranslatableString(getKey(@"preset_note_applied"), @"Preset '{0}' applied. {1}", name, summary);
        /// <summary>
        /// "Ready-made Presets"
        /// </summary>
        public static LocalisableString PresetHeader => new TranslatableString(getKey(@"preset_header"), @"Ready-made Presets");
        /// <summary>
        /// "Relax + Aim Assist Preset"
        /// </summary>
        public static LocalisableString PresetDropdownCaption => new TranslatableString(getKey(@"preset_dropdown_caption"), @"Relax + Aim Assist Preset");
        /// <summary>
        /// "Selecting a preset instantly applies a predefined set of settings for relax and aim assist."
        /// </summary>
        public static LocalisableString PresetDropdownHint => new TranslatableString(getKey(@"preset_dropdown_hint"), @"Selecting a preset instantly applies a predefined set of settings for relax and aim assist.");
        /// <summary>
        /// "Community"
        /// </summary>
        public static LocalisableString CommunityHeader => new TranslatableString(getKey(@"community_header"), @"Community");
        /// <summary>
        /// "Morasooma Telegram Channel"
        /// </summary>
        public static LocalisableString CommunityTgChannel => new TranslatableString(getKey(@"community_tg_channel"), @"Morasooma Telegram Channel");
        /// <summary>
        /// "Discord Server"
        /// </summary>
        public static LocalisableString CommunityDiscordServer => new TranslatableString(getKey(@"community_discord_server"), @"Discord Server");
        /// <summary>
        /// "Relax"
        /// </summary>
        public static LocalisableString RelaxHeader => new TranslatableString(getKey(@"relax_header"), @"Relax");
        /// <summary>
        /// "Enable relax"
        /// </summary>
        public static LocalisableString RelaxEnableCaption => new TranslatableString(getKey(@"relax_enable_caption"), @"Enable relax");
        /// <summary>
        /// "Enables automatic clicking. If disabled, the player clicks."
        /// </summary>
        public static LocalisableString RelaxEnableHint => new TranslatableString(getKey(@"relax_enable_hint"), @"Enables automatic clicking. If disabled, the player clicks.");
        /// <summary>
        /// "Base offset"
        /// </summary>
        public static LocalisableString RelaxBaseOffsetCaption => new TranslatableString(getKey(@"relax_base_offset_caption"), @"Base offset");
        /// <summary>
        /// "Timing shift from the ideal hit. Positive — later, negative — earlier."
        /// </summary>
        public static LocalisableString RelaxBaseOffsetHint => new TranslatableString(getKey(@"relax_base_offset_hint"), @"Timing shift from the ideal hit. Positive — later, negative — earlier.");
        /// <summary>
        /// "Timing variance"
        /// </summary>
        public static LocalisableString RelaxVarianceCaption => new TranslatableString(getKey(@"relax_variance_caption"), @"Timing variance");
        /// <summary>
        /// "Random timing scatter. Higher value looks more human."
        /// </summary>
        public static LocalisableString RelaxVarianceHint => new TranslatableString(getKey(@"relax_variance_hint"), @"Random timing scatter. Higher value looks more human.");
        /// <summary>
        /// "Hold time"
        /// </summary>
        public static LocalisableString RelaxHoldTimeCaption => new TranslatableString(getKey(@"relax_hold_time_caption"), @"Hold time");
        /// <summary>
        /// "Average key hold time. Affects the duration of presses."
        /// </summary>
        public static LocalisableString RelaxHoldTimeHint => new TranslatableString(getKey(@"relax_hold_time_hint"), @"Average key hold time. Affects the duration of presses.");
        /// <summary>
        /// "Sync radius"
        /// </summary>
        public static LocalisableString RelaxSyncRadiusCaption => new TranslatableString(getKey(@"relax_sync_radius_caption"), @"Sync radius");
        /// <summary>
        /// "Area around the note where relax can wait for the cursor to arrive before clicking."
        /// </summary>
        public static LocalisableString RelaxSyncRadiusHint => new TranslatableString(getKey(@"relax_sync_radius_hint"), @"Area around the note where relax can wait for the cursor to arrive before clicking.");
        /// <summary>
        /// "Max sync delay"
        /// </summary>
        public static LocalisableString RelaxMaxSyncDelayCaption => new TranslatableString(getKey(@"relax_max_sync_delay_caption"), @"Max sync delay");
        /// <summary>
        /// "Maximum time relax will wait for cursor arrival before forcing a click anyway."
        /// </summary>
        public static LocalisableString RelaxMaxSyncDelayHint => new TranslatableString(getKey(@"relax_max_sync_delay_hint"), @"Maximum time relax will wait for cursor arrival before forcing a click anyway.");
        /// <summary>
        /// "Max stable BPM"
        /// </summary>
        public static LocalisableString RelaxStableBpmCaption => new TranslatableString(getKey(@"relax_stable_bpm_caption"), @"Max stable BPM");
        /// <summary>
        /// "Comfortable stream speed. Above this speed, relax starts losing stamina."
        /// </summary>
        public static LocalisableString RelaxStableBpmHint => new TranslatableString(getKey(@"relax_stable_bpm_hint"), @"Comfortable stream speed. Above this speed, relax starts losing stamina.");

        /// <summary>
        /// "Relax PP system"
        /// </summary>
        public static LocalisableString RelaxPpSystemCaption => new TranslatableString(getKey(@"relax_pp_system_caption"), @"Relax PP system");
        /// <summary>
        /// "How relax performance points are calculated locally. Switching recalculates cached values in the background; each system keeps its own cache and server values are unaffected."
        /// </summary>
        public static LocalisableString RelaxPpSystemHint => new TranslatableString(getKey(@"relax_pp_system_hint"), @"How relax performance points are calculated locally. Switching recalculates cached values in the background; each system keeps its own cache and server values are unaffected.");
        /// <summary>
        /// "Mosu"
        /// </summary>
        public static LocalisableString RelaxPpSystemMosuRealistik => new TranslatableString(getKey(@"relax_pp_system_mosu_realistik"), @"Mosu");
        /// <summary>
        /// "Lazer (vanilla)"
        /// </summary>
        public static LocalisableString RelaxPpSystemLazerVanilla => new TranslatableString(getKey(@"relax_pp_system_lazer_vanilla"), @"Lazer (vanilla)");

        /// <summary>
        /// "Dynamic drift"
        /// </summary>
        public static LocalisableString RelaxDynamicDriftCaption => new TranslatableString(getKey(@"relax_dynamic_drift_caption"), @"Dynamic drift");
        /// <summary>
        /// "Smoothly shifts the base timing throughout the map."
        /// </summary>
        public static LocalisableString RelaxDynamicDriftHint => new TranslatableString(getKey(@"relax_dynamic_drift_hint"), @"Smoothly shifts the base timing throughout the map.");
        /// <summary>
        /// "Slider tail offset"
        /// </summary>
        public static LocalisableString RelaxSliderTailOffsetCaption => new TranslatableString(getKey(@"relax_slider_tail_offset_caption"), @"Slider tail offset");
        /// <summary>
        /// "Timing shift for releasing the slider tail."
        /// </summary>
        public static LocalisableString RelaxSliderTailOffsetHint => new TranslatableString(getKey(@"relax_slider_tail_offset_hint"), @"Timing shift for releasing the slider tail.");
        /// <summary>
        /// "Alternate threshold"
        /// </summary>
        public static LocalisableString RelaxAlternateThresholdCaption => new TranslatableString(getKey(@"relax_alternate_threshold_caption"), @"Alternate threshold");
        /// <summary>
        /// "BPM above which relax begins alternating keys."
        /// </summary>
        public static LocalisableString RelaxAlternateThresholdHint => new TranslatableString(getKey(@"relax_alternate_threshold_hint"), @"BPM above which relax begins alternating keys.");
        /// <summary>
        /// "Misalt probability"
        /// </summary>
        public static LocalisableString RelaxMisaltProbabilityCaption => new TranslatableString(getKey(@"relax_misalt_probability_caption"), @"Misalt probability");
        /// <summary>
        /// "Chance of accidental misalt during streams."
        /// </summary>
        public static LocalisableString RelaxMisaltProbabilityHint => new TranslatableString(getKey(@"relax_misalt_probability_hint"), @"Chance of accidental misalt during streams.");
        /// <summary>
        /// "Stream blind mode"
        /// </summary>
        public static LocalisableString RelaxStreamBlindModeCaption => new TranslatableString(getKey(@"relax_stream_blind_mode_caption"), @"Stream blind mode");
        /// <summary>
        /// "Enables blind mode specifically for streams."
        /// </summary>
        public static LocalisableString RelaxStreamBlindModeHint => new TranslatableString(getKey(@"relax_stream_blind_mode_hint"), @"Enables blind mode specifically for streams.");
        /// <summary>
        /// "Blind tap"
        /// </summary>
        public static LocalisableString RelaxBlindTapEnabledCaption => new TranslatableString(getKey(@"relax_blind_tap_enabled_caption"), @"Blind tap");
        /// <summary>
        /// "Tap blindly when the cursor is too far from the note."
        /// </summary>
        public static LocalisableString RelaxBlindTapEnabledHint => new TranslatableString(getKey(@"relax_blind_tap_enabled_hint"), @"Tap blindly when the cursor is too far from the note.");

        /// <summary>
        /// "Practical Room Audit"
        /// </summary>
        public static LocalisableString SecurityHeader => new TranslatableString(getKey(@"security_header"), @"Practical Room Audit");
        /// <summary>
        /// "API endpoint"
        /// </summary>
        public static LocalisableString SecurityApiEndpoint => new TranslatableString(getKey(@"security_api_endpoint"), @"API endpoint");
        /// <summary>
        /// "Taken from client settings. Cannot be changed manually in single-server builds."
        /// </summary>
        public static LocalisableString SecurityApiEndpointHint => new TranslatableString(getKey(@"security_api_endpoint_hint"), @"Taken from client settings. Cannot be changed manually in single-server builds.");
        /// <summary>
        /// "local_user"
        /// </summary>
        public static LocalisableString SecurityLocalUser => new TranslatableString(getKey(@"security_local_user"), @"local_user");
        /// <summary>
        /// "Current client user. Used as default for target_user_id."
        /// </summary>
        public static LocalisableString SecurityLocalUserHint => new TranslatableString(getKey(@"security_local_user_hint"), @"Current client user. Used as default for target_user_id.");
        /// <summary>
        /// "access_token"
        /// </summary>
        public static LocalisableString SecurityAccessToken => new TranslatableString(getKey(@"security_access_token"), @"access_token");
        /// <summary>
        /// "Automatically filled from client authorization. Hidden for security."
        /// </summary>
        public static LocalisableString SecurityAccessTokenHint => new TranslatableString(getKey(@"security_access_token_hint"), @"Automatically filled from client authorization. Hidden for security.");
        /// <summary>
        /// "Refresh fields from client"
        /// </summary>
        public static LocalisableString SecurityRefreshBtn => new TranslatableString(getKey(@"security_refresh_btn"), @"Refresh fields from client");
        /// <summary>
        /// "Re-fetches user, token, and current multiplayer room data."
        /// </summary>
        public static LocalisableString SecurityRefreshTooltip => new TranslatableString(getKey(@"security_refresh_tooltip"), @"Re-fetches user, token, and current multiplayer room data.");
        /// <summary>
        /// "room_id"
        /// </summary>
        public static LocalisableString SecurityRoomId => new TranslatableString(getKey(@"security_room_id"), @"room_id");
        /// <summary>
        /// "Room ID for tests. Auto-filled if you are in a room."
        /// </summary>
        public static LocalisableString SecurityRoomIdHint => new TranslatableString(getKey(@"security_room_id_hint"), @"Room ID for tests. Auto-filled if you are in a room.");
        /// <summary>
        /// "target_user_id"
        /// </summary>
        public static LocalisableString SecurityTargetUserId => new TranslatableString(getKey(@"security_target_user_id"), @"target_user_id");
        /// <summary>
        /// "User ID for join/part tests. Defaults to your ID."
        /// </summary>
        public static LocalisableString SecurityTargetUserIdHint => new TranslatableString(getKey(@"security_target_user_id_hint"), @"User ID for join/part tests. Defaults to your ID.");
        /// <summary>
        /// "playlist_id"
        /// </summary>
        public static LocalisableString SecurityPlaylistId => new TranslatableString(getKey(@"security_playlist_id"), @"playlist_id");
        /// <summary>
        /// "Playlist item ID for score token test. Auto-filled in room."
        /// </summary>
        public static LocalisableString SecurityPlaylistIdHint => new TranslatableString(getKey(@"security_playlist_id_hint"), @"Playlist item ID for score token test. Auto-filled in room.");
        /// <summary>
        /// "beatmap_id"
        /// </summary>
        public static LocalisableString SecurityBeatmapId => new TranslatableString(getKey(@"security_beatmap_id"), @"beatmap_id");
        /// <summary>
        /// "Beatmap ID for score token test. Filled from current playlist."
        /// </summary>
        public static LocalisableString SecurityBeatmapIdHint => new TranslatableString(getKey(@"security_beatmap_id_hint"), @"Beatmap ID for score token test. Filled from current playlist.");
        /// <summary>
        /// "beatmap_hash"
        /// </summary>
        public static LocalisableString SecurityBeatmapHash => new TranslatableString(getKey(@"security_beatmap_hash"), @"beatmap_hash");
        /// <summary>
        /// "MD5 hash of beatmap for token test. Filled from playlist or local DB."
        /// </summary>
        public static LocalisableString SecurityBeatmapHashHint => new TranslatableString(getKey(@"security_beatmap_hash_hint"), @"MD5 hash of beatmap for token test. Filled from playlist or local DB.");
        /// <summary>
        /// "ruleset_id"
        /// </summary>
        public static LocalisableString SecurityRulesetId => new TranslatableString(getKey(@"security_ruleset_id"), @"ruleset_id");
        /// <summary>
        /// "0 = Standard, 1 = Taiko, 2 = Catch, 3 = Mania. Auto-filled."
        /// </summary>
        public static LocalisableString SecurityRulesetIdHint => new TranslatableString(getKey(@"security_ruleset_id_hint"), @"0 = Standard, 1 = Taiko, 2 = Catch, 3 = Mania. Auto-filled.");
        /// <summary>
        /// "ruleset_hash"
        /// </summary>
        public static LocalisableString SecurityRulesetHash => new TranslatableString(getKey(@"security_ruleset_hash"), @"ruleset_hash");
        /// <summary>
        /// "Optional ruleset hash. Leave empty if not needed."
        /// </summary>
        public static LocalisableString SecurityRulesetHashHint => new TranslatableString(getKey(@"security_ruleset_hash_hint"), @"Optional ruleset hash. Leave empty if not needed.");
        /// <summary>
        /// "Room count in burst test"
        /// </summary>
        public static LocalisableString SecurityBurstCount => new TranslatableString(getKey(@"security_burst_count"), @"Room count in burst test");
        /// <summary>
        /// "Checks if server restricts user to a single room."
        /// </summary>
        public static LocalisableString SecurityBurstCountHint => new TranslatableString(getKey(@"security_burst_count_hint"), @"Checks if server restricts user to a single room.");
        /// <summary>
        /// "Test: Close another&apos;s room by room_id"
        /// </summary>
        public static LocalisableString SecurityActionCloseRoom => new TranslatableString(getKey(@"security_action_close_room"), @"Test: Close another's room by room_id");
        /// <summary>
        /// "Sends DELETE /api/v2/rooms/{room_id}. Checks owner authorization."
        /// </summary>
        public static LocalisableString SecurityActionCloseRoomHint => new TranslatableString(getKey(@"security_action_close_room_hint"), @"Sends DELETE /api/v2/rooms/{room_id}. Checks owner authorization.");
        /// <summary>
        /// "Test: Force join target_user_id into room_id"
        /// </summary>
        public static LocalisableString SecurityActionForceJoin => new TranslatableString(getKey(@"security_action_force_join"), @"Test: Force join target_user_id into room_id");
        /// <summary>
        /// "Sends PUT /api/v2/rooms/{room_id}/users/{target_user_id}. Checks if server trusts user_id in URL."
        /// </summary>
        public static LocalisableString SecurityActionForceJoinHint => new TranslatableString(getKey(@"security_action_force_join_hint"), @"Sends PUT /api/v2/rooms/{room_id}/users/{target_user_id}. Checks if server trusts user_id in URL.");
        /// <summary>
        /// "Test: Force part target_user_id from room_id"
        /// </summary>
        public static LocalisableString SecurityActionForcePart => new TranslatableString(getKey(@"security_action_force_part"), @"Test: Force part target_user_id from room_id");
        /// <summary>
        /// "Sends DELETE /api/v2/rooms/{room_id}/users/{target_user_id}. Checks authorization."
        /// </summary>
        public static LocalisableString SecurityActionForcePartHint => new TranslatableString(getKey(@"security_action_force_part_hint"), @"Sends DELETE /api/v2/rooms/{room_id}/users/{target_user_id}. Checks authorization.");
        /// <summary>
        /// "Test: Repeated join into same room"
        /// </summary>
        public static LocalisableString SecurityActionRepeatSelfJoin => new TranslatableString(getKey(@"security_action_repeat_self_join"), @"Test: Repeated join into same room");
        /// <summary>
        /// "Sends two PUT requests to join. Checks participant_count abuse."
        /// </summary>
        public static LocalisableString SecurityActionRepeatSelfJoinHint => new TranslatableString(getKey(@"security_action_repeat_self_join_hint"), @"Sends two PUT requests to join. Checks participant_count abuse.");
        /// <summary>
        /// "Test: Get score token without joining room"
        /// </summary>
        public static LocalisableString SecurityActionForeignScoreToken => new TranslatableString(getKey(@"security_action_foreign_score_token"), @"Test: Get score token without joining room");
        /// <summary>
        /// "Sends POST /api/v2/rooms/.../scores. Checks if participation is required."
        /// </summary>
        public static LocalisableString SecurityActionForeignScoreTokenHint => new TranslatableString(getKey(@"security_action_foreign_score_token_hint"), @"Sends POST /api/v2/rooms/.../scores. Checks if participation is required.");
        /// <summary>
        /// "Test: Create multiple rooms in burst"
        /// </summary>
        public static LocalisableString SecurityActionBurstCreate => new TranslatableString(getKey(@"security_action_burst_create"), @"Test: Create multiple rooms in burst");
        /// <summary>
        /// "Creates several rooms in a row. Checks for rate limits."
        /// </summary>
        public static LocalisableString SecurityActionBurstCreateHint => new TranslatableString(getKey(@"security_action_burst_create_hint"), @"Creates several rooms in a row. Checks for rate limits.");
        /// <summary>
        /// "Test: Create room with forged fields"
        /// </summary>
        public static LocalisableString SecurityActionForgedRoom => new TranslatableString(getKey(@"security_action_forged_room"), @"Test: Create room with forged fields");
        /// <summary>
        /// "Creates room with custom participant_count, status, type. Checks server normalization."
        /// </summary>
        public static LocalisableString SecurityActionForgedRoomHint => new TranslatableString(getKey(@"security_action_forged_room_hint"), @"Creates room with custom participant_count, status, type. Checks server normalization.");
        /// <summary>
        /// "Join/Part request count"
        /// </summary>
        public static LocalisableString SecurityMutationCount => new TranslatableString(getKey(@"security_mutation_count"), @"Join/Part request count");
        /// <summary>
        /// "How many times to run force-join, force-part, and repeat join tests."
        /// </summary>
        public static LocalisableString SecurityMutationCountHint => new TranslatableString(getKey(@"security_mutation_count_hint"), @"How many times to run force-join, force-part, and repeat join tests.");
        /// <summary>
        /// "Authorization required"
        /// </summary>
        public static LocalisableString SecurityNotAuthorizedTitle => new TranslatableString(getKey(@"security_not_authorized_title"), @"Authorization required");
        /// <summary>
        /// "An authorized account and valid token are required for audit."
        /// </summary>
        public static LocalisableString SecurityNotAuthorizedBody => new TranslatableString(getKey(@"security_not_authorized_body"), @"An authorized account and valid token are required for audit.");
        /// <summary>
        /// "Invalid number"
        /// </summary>
        public static LocalisableString SecurityInvalidNumberTitle => new TranslatableString(getKey(@"security_invalid_number_title"), @"Invalid number");
        /// <summary>
        /// "Field {0} must be an integer."
        /// </summary>
        public static LocalisableString SecurityInvalidNumberBody(LocalisableString fieldName) => new TranslatableString(getKey(@"security_invalid_number_body"), @"Field {0} must be an integer.", fieldName);
        /// <summary>
        /// " (token not received yet)"
        /// </summary>
        public static LocalisableString SecurityTokenReceivedError => new TranslatableString(getKey(@"security_token_received_error"), @" (token not received yet)");
        /// <summary>
        /// "Not authorized"
        /// </summary>
        public static LocalisableString SecurityNotAuthorizedText => new TranslatableString(getKey(@"security_not_authorized_text"), @"Not authorized");
        /// <summary>
        /// "Will be obtained from current session after login"
        /// </summary>
        public static LocalisableString SecurityTokenSessionWait => new TranslatableString(getKey(@"security_token_session_wait"), @"Will be obtained from current session after login");
        /// <summary>
        /// "Room closed"
        /// </summary>
        public static LocalisableString SecurityCloseRoomSuccessTitle => new TranslatableString(getKey(@"security_close_room_success_title"), @"Room closed");
        /// <summary>
        /// "DELETE /api/v2/rooms/{0} completed without errors.\n\nServer returned: id={1}, status={2}, ends_at={3}.\n\nCheck server logs for owner rights validation."
        /// </summary>
        public static LocalisableString SecurityCloseRoomSuccessBody(LocalisableString roomId, LocalisableString status, LocalisableString endsAt) => new TranslatableString(getKey(@"security_close_room_success_body"), @"DELETE /api/v2/rooms/{0} completed without errors.\n\nServer returned: id={1}, status={2}, ends_at={3}.\n\nCheck server logs for owner rights validation.", roomId, status, endsAt);
        /// <summary>
        /// "Room close failed"
        /// </summary>
        public static LocalisableString SecurityCloseRoomFailedTitle => new TranslatableString(getKey(@"security_close_room_failed_title"), @"Room close failed");
        /// <summary>
        /// "Force join successful"
        /// </summary>
        public static LocalisableString SecurityForceJoinSuccessTitle => new TranslatableString(getKey(@"security_force_join_success_title"), @"Force join successful");
        /// <summary>
        /// "PUT /api/v2/rooms/{0}/users/{1} completed without errors.\n\nServer returned: id={2}, participant_count={3}, status={4}.\n\nCheck server logs for authorization validation."
        /// </summary>
        public static LocalisableString SecurityForceJoinSuccessBody(LocalisableString roomId, LocalisableString userId, LocalisableString returnedRoomId, LocalisableString participantCount, LocalisableString status) => new TranslatableString(getKey(@"security_force_join_success_body"), @"PUT /api/v2/rooms/{0}/users/{1} completed without errors.\n\nServer returned: id={2}, participant_count={3}, status={4}.\n\nCheck server logs for authorization validation.", roomId, userId, returnedRoomId, participantCount, status);
        /// <summary>
        /// "Force join failed"
        /// </summary>
        public static LocalisableString SecurityForceJoinFailedTitle => new TranslatableString(getKey(@"security_force_join_failed_title"), @"Force join failed");
        /// <summary>
        /// "Force part successful"
        /// </summary>
        public static LocalisableString SecurityForcePartSuccessTitle => new TranslatableString(getKey(@"security_force_part_success_title"), @"Force part successful");
        /// <summary>
        /// "DELETE /api/v2/rooms/{0}/users/{1} completed without errors.\n\nServer returned: participant_count={2}, status={3}.\n\nCheck server logs for authorization validation."
        /// </summary>
        public static LocalisableString SecurityForcePartSuccessBody(LocalisableString roomId, LocalisableString userId, LocalisableString participantCount, LocalisableString status) => new TranslatableString(getKey(@"security_force_part_success_body"), @"DELETE /api/v2/rooms/{0}/users/{1} completed without errors.\n\nServer returned: participant_count={2}, status={3}.\n\nCheck server logs for authorization validation.", roomId, userId, participantCount, status);
        /// <summary>
        /// "Force part failed"
        /// </summary>
        public static LocalisableString SecurityForcePartFailedTitle => new TranslatableString(getKey(@"security_force_part_failed_title"), @"Force part failed");
        /// <summary>
        /// "Repeat join successful"
        /// </summary>
        public static LocalisableString SecurityRepeatJoinSuccessTitle => new TranslatableString(getKey(@"security_repeat_join_success_title"), @"Repeat join successful");
        /// <summary>
        /// "First PUT returned participant_count={0}.\nSecond PUT returned participant_count={1}.\n\nIf count increased, participant_count can be abused by repeated joins."
        /// </summary>
        public static LocalisableString SecurityRepeatJoinSuccessBody(LocalisableString count1, LocalisableString count2) => new TranslatableString(getKey(@"security_repeat_join_success_body"), @"First PUT returned participant_count={0}.\nSecond PUT returned participant_count={1}.\n\nIf count increased, participant_count can be abused by repeated joins.", count1, count2);
        /// <summary>
        /// "Repeat join failed"
        /// </summary>
        public static LocalisableString SecurityRepeatJoinFailedTitle => new TranslatableString(getKey(@"security_repeat_join_failed_title"), @"Repeat join failed");
        /// <summary>
        /// "Score token received"
        /// </summary>
        public static LocalisableString SecurityScoreTokenSuccessTitle => new TranslatableString(getKey(@"security_score_token_success_title"), @"Score token received");
        /// <summary>
        /// "POST /api/v2/rooms/{0}/playlist/{1}/scores completed without errors, token id={2}.\n\nIf you did not join the room, this confirms lack of participation check before issuing token.\n\nCheck server logs."
        /// </summary>
        public static LocalisableString SecurityScoreTokenSuccessBody(LocalisableString roomId, LocalisableString playlistId, LocalisableString tokenId) => new TranslatableString(getKey(@"security_score_token_success_body"), @"POST /api/v2/rooms/{0}/playlist/{1}/scores completed without errors, token id={2}.\n\nIf you did not join the room, this confirms lack of participation check before issuing token.\n\nCheck server logs.", roomId, playlistId, tokenId);
        /// <summary>
        /// "Score token failed"
        /// </summary>
        public static LocalisableString SecurityScoreTokenFailedTitle => new TranslatableString(getKey(@"security_score_token_failed_title"), @"Score token failed");
        /// <summary>
        /// "beatmap_hash empty"
        /// </summary>
        public static LocalisableString SecurityScoreTokenMissingHashTitle => new TranslatableString(getKey(@"security_score_token_missing_hash_title"), @"beatmap_hash empty");
        /// <summary>
        /// "A valid beatmap_hash is required for this test."
        /// </summary>
        public static LocalisableString SecurityScoreTokenMissingHashBody => new TranslatableString(getKey(@"security_score_token_missing_hash_body"), @"A valid beatmap_hash is required for this test.");
        /// <summary>
        /// "Burst room creation successful"
        /// </summary>
        public static LocalisableString SecurityBurstCreateSuccessTitle => new TranslatableString(getKey(@"security_burst_create_success_title"), @"Burst room creation successful");
        /// <summary>
        /// "Rooms created: {0}.\nIDs: {1}.\n\nChecking for missing room limit per user.\n\nVerify in server logs that all POST /rooms succeeded."
        /// </summary>
        public static LocalisableString SecurityBurstCreateSuccessBody(LocalisableString count, LocalisableString ids) => new TranslatableString(getKey(@"security_burst_create_success_body"), @"Rooms created: {0}.\nIDs: {1}.\n\nChecking for missing room limit per user.\n\nVerify in server logs that all POST /rooms succeeded.", count, ids);
        /// <summary>
        /// "Burst creation failed"
        /// </summary>
        public static LocalisableString SecurityBurstCreateFailedTitle => new TranslatableString(getKey(@"security_burst_create_failed_title"), @"Burst creation failed");
        /// <summary>
        /// "Forged room created"
        /// </summary>
        public static LocalisableString SecurityForgedRoomSuccessTitle => new TranslatableString(getKey(@"security_forged_room_success_title"), @"Forged room created");
        /// <summary>
        /// "Server returned: id={0}, participant_count={1}, status={2}, type={3}, queue_mode={4}.\n\nChecking if server trusts client fields on creation.\n\nCheck server logs to see which fields were accepted without normalization."
        /// </summary>
        public static LocalisableString SecurityForgedRoomSuccessBody(LocalisableString roomId, LocalisableString participantCount, LocalisableString status, LocalisableString matchType, LocalisableString queueMode) => new TranslatableString(getKey(@"security_forged_room_success_body"), @"Server returned: id={0}, participant_count={1}, status={2}, type={3}, queue_mode={4}.\n\nChecking if server trusts client fields on creation.\n\nCheck server logs to see which fields were accepted without normalization.", roomId, participantCount, status, matchType, queueMode);
        /// <summary>
        /// "Forged room creation failed"
        /// </summary>
        public static LocalisableString SecurityForgedRoomFailedTitle => new TranslatableString(getKey(@"security_forged_room_failed_title"), @"Forged room creation failed");
        /// <summary>
        /// "Request failed with error: {0}\n\nIf this is a local server, check its logs around the time of error."
        /// </summary>
        public static LocalisableString SecurityRequestFailedLogTip(LocalisableString error) => new TranslatableString(getKey(@"security_request_failed_log_tip"), @"Request failed with error: {0}\n\nIf this is a local server, check its logs around the time of error.", error);
        /// <summary>
        /// "Additional difficulty info"
        /// </summary>
        public static LocalisableString DifficultyAdditionalInfoCaption => new TranslatableString(getKey(@"difficulty_additional_info_caption"), @"Additional difficulty info");
        /// <summary>
        /// "Displays max combo, max PP, and detailed PP breakdown for each difficulty."
        /// </summary>
        public static LocalisableString DifficultyAdditionalInfoHint => new TranslatableString(getKey(@"difficulty_additional_info_hint"), @"Displays max combo, max PP, and detailed PP breakdown for each difficulty.");
        /// <summary>
        /// "Disable beatmap status overwrite"
        /// </summary>
        public static LocalisableString ForkDisableBeatmapStatusOverwriteCaption => new TranslatableString(getKey(@"fork_disable_beatmap_status_overwrite_caption"), @"Disable beatmap status overwrite");
        /// <summary>
        /// "If enabled, the client will not overwrite map status (Ranked/Loved) when playing on other servers. Doesn&apos;t apply to Morasooma."
        /// </summary>
        public static LocalisableString ForkDisableBeatmapStatusOverwriteHint => new TranslatableString(getKey(@"fork_disable_beatmap_status_overwrite_hint"), @"If enabled, the client will not overwrite map status (Ranked/Loved) when playing on other servers. Doesn't apply to Morasooma.");
        /// <summary>
        /// "Enable Classic mod by default"
        /// </summary>
        public static LocalisableString ForkClassicModDefaultCaption => new TranslatableString(getKey(@"fork_classic_mod_default_caption"), @"Enable Classic mod by default");
        /// <summary>
        /// "Disable visual slider misses (crosses)"
        /// </summary>
        public static LocalisableString ForkHideVisualSliderMissesCaption => new TranslatableString(getKey(@"fork_hide_visual_slider_misses_caption"), @"Disable visual slider misses (crosses)");
        /// <summary>
        /// "Server Profiles"
        /// </summary>
        public static LocalisableString ServerProfilesHeader => new TranslatableString(getKey(@"server_profiles_header"), @"Server Profiles");
        /// <summary>
        /// "Select server"
        /// </summary>
        public static LocalisableString ServerProfilesDropdownCaption => new TranslatableString(getKey(@"server_profiles_dropdown_caption"), @"Select server");
        /// <summary>
        /// "Connect to server"
        /// </summary>
        public static LocalisableString ServerProfilesConnectBtn => new TranslatableString(getKey(@"server_profiles_connect_btn"), @"Connect to server");
        /// <summary>
        /// "Проверить подключение"
        /// </summary>
        public static LocalisableString ServerProfilesTestConnectionBtn => new TranslatableString(getKey(@"server_profiles_test_connection_btn"), @"Проверить подключение");
        /// <summary>
        /// "Проверяет текущий адрес Stable Bancho и данные входа без перезапуска игры."
        /// </summary>
        public static LocalisableString ServerProfilesTestConnectionHint => new TranslatableString(getKey(@"server_profiles_test_connection_hint"), @"Проверяет текущий адрес Stable Bancho и данные входа без перезапуска игры.");
        /// <summary>
        /// "Проверка подключения..."
        /// </summary>
        public static LocalisableString ServerProfilesTestingConnectionBtn => new TranslatableString(getKey(@"server_profiles_testing_connection_btn"), @"Проверка подключения...");
        /// <summary>
        /// "Подключение работает"
        /// </summary>
        public static LocalisableString ServerProfilesTestSuccessTitle => new TranslatableString(getKey(@"server_profiles_test_success_title"), @"Подключение работает");
        /// <summary>
        /// "Bancho принял пользователя {0} (ID: {1}).\n\nАдрес: {2}"
        /// </summary>
        public static LocalisableString ServerProfilesTestSuccessBody(LocalisableString username, LocalisableString userId, LocalisableString endpoint) => new TranslatableString(getKey(@"server_profiles_test_success_body"), "Bancho принял пользователя {0} (ID: {1}).\n\nАдрес: {2}", username, userId, endpoint);
        /// <summary>
        /// "Подключение не удалось"
        /// </summary>
        public static LocalisableString ServerProfilesTestFailureTitle => new TranslatableString(getKey(@"server_profiles_test_failure_title"), @"Подключение не удалось");
        /// <summary>
        /// "Адрес: {0}\n\n{1}"
        /// </summary>
        public static LocalisableString ServerProfilesTestFailureBody(LocalisableString endpoint, LocalisableString error) => new TranslatableString(getKey(@"server_profiles_test_failure_body"), "Адрес: {0}\n\n{1}", endpoint, error);
        /// <summary>
        /// "Add profile"
        /// </summary>
        public static LocalisableString ServerProfilesAddBtn => new TranslatableString(getKey(@"server_profiles_add_btn"), @"Add profile");
        /// <summary>
        /// "Delete profile"
        /// </summary>
        public static LocalisableString ServerProfilesDeleteBtn => new TranslatableString(getKey(@"server_profiles_delete_btn"), @"Delete profile");
        /// <summary>
        /// "Profile Name"
        /// </summary>
        public static LocalisableString ServerProfilesNameCaption => new TranslatableString(getKey(@"server_profiles_name_caption"), @"Profile Name");
        /// <summary>
        /// "API URL"
        /// </summary>
        public static LocalisableString ServerProfilesApiUrlCaption => new TranslatableString(getKey(@"server_profiles_api_url_caption"), @"API URL");
        /// <summary>
        /// "Website URL"
        /// </summary>
        public static LocalisableString ServerProfilesWebsiteUrlCaption => new TranslatableString(getKey(@"server_profiles_website_url_caption"), @"Website URL");
        /// <summary>
        /// "Username"
        /// </summary>
        public static LocalisableString ServerProfilesUsernameCaption => new TranslatableString(getKey(@"server_profiles_username_caption"), @"Username");
        /// <summary>
        /// "Stable private server protocol"
        /// </summary>
        public static LocalisableString ServerProfilesStableProtocolCaption => new TranslatableString(getKey(@"server_profiles_stable_protocol_caption"), @"Stable private server protocol");
        /// <summary>
        /// "Uses legacy Bancho login and osu-submit-modular-selector.php instead of lazer OAuth score submission."
        /// </summary>
        public static LocalisableString ServerProfilesStableProtocolHint => new TranslatableString(getKey(@"server_profiles_stable_protocol_hint"), @"Uses legacy Bancho login and osu-submit-modular-selector.php instead of lazer OAuth score submission.");
        /// <summary>
        /// "Bancho URL"
        /// </summary>
        public static LocalisableString ServerProfilesBanchoUrlCaption => new TranslatableString(getKey(@"server_profiles_bancho_url_caption"), @"Bancho URL");
        /// <summary>
        /// "Stable server password"
        /// </summary>
        public static LocalisableString ServerProfilesStablePasswordCaption => new TranslatableString(getKey(@"server_profiles_stable_password_caption"), @"Stable server password");
        /// <summary>
        /// "Enter a new password to replace the stored MD5 login secret. The plaintext password is never saved."
        /// </summary>
        public static LocalisableString ServerProfilesStablePasswordHint => new TranslatableString(getKey(@"server_profiles_stable_password_hint"), @"Enter a new password to replace the stored MD5 login secret. The plaintext password is never saved.");
        /// <summary>
        /// "Stable Bancho connection"
        /// </summary>
        public static LocalisableString ServerProfilesStableStatusCaption => new TranslatableString(getKey(@"server_profiles_stable_status_caption"), @"Stable Bancho connection");
        /// <summary>
        /// "Client ID"
        /// </summary>
        public static LocalisableString ServerProfilesClientIdCaption => new TranslatableString(getKey(@"server_profiles_client_id_caption"), @"Client ID");
        /// <summary>
        /// "Client Secret"
        /// </summary>
        public static LocalisableString ServerProfilesClientSecretCaption => new TranslatableString(getKey(@"server_profiles_client_secret_caption"), @"Client Secret");
        /// <summary>
        /// "Client Version"
        /// </summary>
        public static LocalisableString ServerProfilesVersionCaption => new TranslatableString(getKey(@"server_profiles_version_caption"), @"Client Version");
        /// <summary>
        /// "Version Hash"
        /// </summary>
        public static LocalisableString ServerProfilesVersionHashCaption => new TranslatableString(getKey(@"server_profiles_version_hash_caption"), @"Version Hash");
        /// <summary>
        /// "Enable Relax and Autopilot support"
        /// </summary>
        public static LocalisableString ServerProfilesSupportsSpecialRulesetsCaption => new TranslatableString(getKey(@"server_profiles_supports_special_rulesets_caption"), @"Enable Relax and Autopilot support");
        /// <summary>
        /// "Displays leaderboards, stats, and Relax/Autopilot mode selection on this server. Doesn&apos;t apply to Morasooma."
        /// </summary>
        public static LocalisableString ServerProfilesSupportsSpecialRulesetsHint => new TranslatableString(getKey(@"server_profiles_supports_special_rulesets_hint"), @"Displays leaderboards, stats, and Relax/Autopilot mode selection on this server. Doesn't apply to Morasooma.");
        /// <summary>
        /// "Server change"
        /// </summary>
        public static LocalisableString ServerProfilesRestartTitle => new TranslatableString(getKey(@"server_profiles_restart_title"), @"Server change");
        /// <summary>
        /// "The game will be restarted to connect to the new server."
        /// </summary>
        public static LocalisableString ServerProfilesRestartBody => new TranslatableString(getKey(@"server_profiles_restart_body"), @"The game will be restarted to connect to the new server.");
        /// <summary>
        /// "Connection prohibited"
        /// </summary>
        public static LocalisableString ServerProfilesForbiddenTitle => new TranslatableString(getKey(@"server_profiles_forbidden_title"), @"Connection prohibited");
        /// <summary>
        /// "Connecting to {0} from this client is prohibited."
        /// </summary>
        public static LocalisableString ServerProfilesForbiddenBody(LocalisableString serverName) => new TranslatableString(getKey(@"server_profiles_forbidden_body"), @"Connecting to {0} from this client is prohibited.", serverName);

        /// <summary>
        /// "Disable interface shear"
        /// </summary>
        public static LocalisableString DisableInterfaceShearCaption => new TranslatableString(getKey(@"disable_interface_shear_caption"), @"Disable interface shear");
        /// <summary>
        /// "Disables the slant effect on all interface elements."
        /// </summary>
        public static LocalisableString DisableInterfaceShearHint => new TranslatableString(getKey(@"disable_interface_shear_hint"), @"Disables the slant effect on all interface elements.");

        /// <summary>
        /// "Supporter Quests"
        /// </summary>
        public static LocalisableString SupporterQuestTitle => new TranslatableString(getKey(@"supporter_quest_title"), @"Supporter Quests");
        /// <summary>
        /// "Complete quests to earn a month of supporter!"
        /// </summary>
        public static LocalisableString SupporterQuestDescription => new TranslatableString(getKey(@"supporter_quest_description"), @"Complete quests to earn a month of supporter!");
        /// <summary>
        /// "Until 1 month of supporter"
        /// </summary>
        public static LocalisableString SupporterQuestHeader => new TranslatableString(getKey(@"supporter_quest_header"), @"Until 1 month of supporter");
        /// <summary>
        /// "Саппортер можно получить, только внеся вклад в сообщество."
        /// </summary>
        public static LocalisableString SupporterQuestContributionNote => new TranslatableString(getKey(@"supporter_quest_contribution_note"), @"Саппортер можно получить, только внеся вклад в сообщество.");
        /// <summary>
        /// "Поддержать проект"
        /// </summary>
        public static LocalisableString SupporterQuestDonateBtn => new TranslatableString(getKey(@"supporter_quest_donate_btn"), @"Поддержать проект");
        /// <summary>
        /// "+ {0} more locked"
        /// </summary>
        public static LocalisableString SupporterQuestRemaining(int count) => new TranslatableString(getKey(@"supporter_quest_remaining"), @"+ {0} more locked", count);
        /// <summary>
        /// "Quest completed: &quot;{0}&quot;!"
        /// </summary>
        public static LocalisableString SupporterQuestCompletedNotification(LocalisableString name) => new TranslatableString(getKey(@"supporter_quest_completed_notification"), @"Quest completed: ""{0}""!", name);

        /// <summary>
        /// "Supporter benefits:"
        /// </summary>
        public static LocalisableString SupporterQuestFeaturesHeader => new TranslatableString(getKey(@"supporter_quest_features_header"), @"Supporter benefits:");
        /// <summary>
        /// "• Ability to change your username"
        /// </summary>
        public static LocalisableString SupporterQuestFeature1 => new TranslatableString(getKey(@"supporter_quest_feature_1"), @"• Ability to change your username");
        /// <summary>
        /// "• Download up to 8 exclusive beatmaps/day"
        /// </summary>
        public static LocalisableString SupporterQuestFeature2 => new TranslatableString(getKey(@"supporter_quest_feature_2"), @"• Download up to 8 exclusive beatmaps/day");
        /// <summary>
        /// "• Supporter badge next to your name"
        /// </summary>
        public static LocalisableString SupporterQuestFeature3 => new TranslatableString(getKey(@"supporter_quest_feature_3"), @"• Supporter badge next to your name");
        /// <summary>
        /// "• Ability to create teams"
        /// </summary>
        public static LocalisableString SupporterQuestFeature4 => new TranslatableString(getKey(@"supporter_quest_feature_4"), @"• Ability to create teams");
        /// <summary>
        /// "• All Morasooma Supporter features"
        /// </summary>
        public static LocalisableString SupporterQuestFeature5 => new TranslatableString(getKey(@"supporter_quest_feature_5"), @"• All Morasooma Supporter features");

        /// <summary>
        /// "Play 5 maps"
        /// </summary>
        public static LocalisableString QuestName1 => new TranslatableString(getKey(@"quest_name_1"), @"Play 5 maps");
        /// <summary>
        /// "Play any 5 maps (with ranked mods if using mods)."
        /// </summary>
        public static LocalisableString QuestDesc1 => new TranslatableString(getKey(@"quest_desc_1"), @"Play any 5 maps (with ranked mods if using mods).");
        /// <summary>
        /// "Change profile background"
        /// </summary>
        public static LocalisableString QuestName2 => new TranslatableString(getKey(@"quest_name_2"), @"Change profile background");
        /// <summary>
        /// "Set a background image in your profile."
        /// </summary>
        public static LocalisableString QuestDesc2 => new TranslatableString(getKey(@"quest_desc_2"), @"Set a background image in your profile.");
        /// <summary>
        /// "Play in two playlists"
        /// </summary>
        public static LocalisableString QuestName3 => new TranslatableString(getKey(@"quest_name_3"), @"Play in two playlists");
        /// <summary>
        /// "Play in two different multiplayer playlists."
        /// </summary>
        public static LocalisableString QuestDesc3 => new TranslatableString(getKey(@"quest_desc_3"), @"Play in two different multiplayer playlists.");
        /// <summary>
        /// "Play 10 minutes of Standard Relax"
        /// </summary>
        public static LocalisableString QuestName4 => new TranslatableString(getKey(@"quest_name_4"), @"Play 10 minutes of Standard Relax");
        /// <summary>
        /// "Play a total of 10 minutes in Standard Relax mode."
        /// </summary>
        public static LocalisableString QuestDesc4 => new TranslatableString(getKey(@"quest_desc_4"), @"Play a total of 10 minutes in Standard Relax mode.");
        /// <summary>
        /// "Play 5 maps in Mania with A rank"
        /// </summary>
        public static LocalisableString QuestName5 => new TranslatableString(getKey(@"quest_name_5"), @"Play 5 maps in Mania with A rank");
        /// <summary>
        /// "Pass 5 maps in Mania mode with an A rank or higher."
        /// </summary>
        public static LocalisableString QuestDesc5 => new TranslatableString(getKey(@"quest_desc_5"), @"Pass 5 maps in Mania mode with an A rank or higher.");
        /// <summary>
        /// "Play two maps longer than 5 minutes"
        /// </summary>
        public static LocalisableString QuestName6 => new TranslatableString(getKey(@"quest_name_6"), @"Play two maps longer than 5 minutes");
        /// <summary>
        /// "Play any 2 maps with a duration of more than 5 minutes."
        /// </summary>
        public static LocalisableString QuestDesc6 => new TranslatableString(getKey(@"quest_desc_6"), @"Play any 2 maps with a duration of more than 5 minutes.");
        /// <summary>
        /// "Pass 10 maps in any playlist"
        /// </summary>
        public static LocalisableString QuestName7 => new TranslatableString(getKey(@"quest_name_7"), @"Pass 10 maps in any playlist");
        /// <summary>
        /// "Pass 10 maps within multiplayer playlists."
        /// </summary>
        public static LocalisableString QuestDesc7 => new TranslatableString(getKey(@"quest_desc_7"), @"Pass 10 maps within multiplayer playlists.");
        /// <summary>
        /// "Spend 60 minutes in game"
        /// </summary>
        public static LocalisableString QuestName8 => new TranslatableString(getKey(@"quest_name_8"), @"Spend 60 minutes in game");
        /// <summary>
        /// "Spend 60 minutes playing on beatmaps."
        /// </summary>
        public static LocalisableString QuestDesc8 => new TranslatableString(getKey(@"quest_desc_8"), @"Spend 60 minutes playing on beatmaps.");
        /// <summary>
        /// "Add someone as a friend"
        /// </summary>
        public static LocalisableString QuestName9 => new TranslatableString(getKey(@"quest_name_9"), @"Add someone as a friend");
        /// <summary>
        /// "Add any other player to your friend list."
        /// </summary>
        public static LocalisableString QuestDesc9 => new TranslatableString(getKey(@"quest_desc_9"), @"Add any other player to your friend list.");
        /// <summary>
        /// "Play 5 server-exclusive maps"
        /// </summary>
        public static LocalisableString QuestName10 => new TranslatableString(getKey(@"quest_name_10"), @"Play 5 server-exclusive maps");
        /// <summary>
        /// "Play 5 maps marked with the server-exclusive tag."
        /// </summary>
        public static LocalisableString QuestDesc10 => new TranslatableString(getKey(@"quest_desc_10"), @"Play 5 maps marked with the server-exclusive tag.");
        /// <summary>
        /// "Pass 3 maps with 6 mods"
        /// </summary>
        public static LocalisableString QuestName11 => new TranslatableString(getKey(@"quest_name_11"), @"Pass 3 maps with 6 mods");
        /// <summary>
        /// "Successfully pass 3 maps using at least 6 mods simultaneously."
        /// </summary>
        public static LocalisableString QuestDesc11 => new TranslatableString(getKey(@"quest_desc_11"), @"Successfully pass 3 maps using at least 6 mods simultaneously.");
        /// <summary>
        /// "Pass 5 maps in Catch with A rank or higher"
        /// </summary>
        public static LocalisableString QuestName12 => new TranslatableString(getKey(@"quest_name_12"), @"Pass 5 maps in Catch with A rank or higher");
        /// <summary>
        /// "Pass 5 maps in Catch mode with an A rank or higher."
        /// </summary>
        public static LocalisableString QuestDesc12 => new TranslatableString(getKey(@"quest_desc_12"), @"Pass 5 maps in Catch mode with an A rank or higher.");
        /// <summary>
        /// "Pass the Daily Challenge map"
        /// </summary>
        public static LocalisableString QuestName13 => new TranslatableString(getKey(@"quest_name_13"), @"Pass the Daily Challenge map");
        /// <summary>
        /// "Pass today&apos;s map in the Daily Challenge."
        /// </summary>
        public static LocalisableString QuestDesc13 => new TranslatableString(getKey(@"quest_desc_13"), @"Pass today's map in the Daily Challenge.");
        /// <summary>
        /// "Pass 50 maps"
        /// </summary>
        public static LocalisableString QuestName14 => new TranslatableString(getKey(@"quest_name_14"), @"Pass 50 maps");
        /// <summary>
        /// "Successfully pass any 50 maps with a B rank or higher."
        /// </summary>
        public static LocalisableString QuestDesc14 => new TranslatableString(getKey(@"quest_desc_14"), @"Successfully pass any 50 maps with a B rank or higher.");
        /// <summary>
        /// "Pass 10 maps in Taiko with B rank or higher"
        /// </summary>
        public static LocalisableString QuestName15 => new TranslatableString(getKey(@"quest_name_15"), @"Pass 10 maps in Taiko with B rank or higher");
        /// <summary>
        /// "Pass 10 maps in Taiko mode with a B rank or higher."
        /// </summary>
        public static LocalisableString QuestDesc15 => new TranslatableString(getKey(@"quest_desc_15"), @"Pass 10 maps in Taiko mode with a B rank or higher.");
        /// <summary>
        /// "Pass a 10-minute map"
        /// </summary>
        public static LocalisableString QuestName16 => new TranslatableString(getKey(@"quest_name_16"), @"Pass a 10-minute map");
        /// <summary>
        /// "Successfully pass a map with a duration of 10 minutes or more with a B rank or higher."
        /// </summary>
        public static LocalisableString QuestDesc16 => new TranslatableString(getKey(@"quest_desc_16"), @"Successfully pass a map with a duration of 10 minutes or more with a B rank or higher.");
        /// <summary>
        /// "Pass 5 five-minute maps"
        /// </summary>
        public static LocalisableString QuestName17 => new TranslatableString(getKey(@"quest_name_17"), @"Pass 5 five-minute maps");
        /// <summary>
        /// "Successfully pass 5 maps with a duration of 5 minutes or more with a B rank or higher."
        /// </summary>
        public static LocalisableString QuestDesc17 => new TranslatableString(getKey(@"quest_desc_17"), @"Successfully pass 5 maps with a duration of 5 minutes or more with a B rank or higher.");
        /// <summary>
        /// "Try all client mods of Morasooma"
        /// </summary>
        public static LocalisableString QuestName18 => new TranslatableString(getKey(@"quest_name_18"), @"Try all client mods of Morasooma");
        /// <summary>
        /// "Play once with each Morasooma client mod (AA, AA!, MSQ, MRX, SB, TS)."
        /// </summary>
        public static LocalisableString QuestDesc18 => new TranslatableString(getKey(@"quest_desc_18"), @"Play once with each Morasooma client mod (AA, AA!, MSQ, MRX, SB, TS).");
        /// <summary>
        /// "Pass 50 maps with A rank"
        /// </summary>
        public static LocalisableString QuestName19 => new TranslatableString(getKey(@"quest_name_19"), @"Pass 50 maps with A rank");
        /// <summary>
        /// "Pass 50 maps of any type with an A rank or higher."
        /// </summary>
        public static LocalisableString QuestDesc19 => new TranslatableString(getKey(@"quest_desc_19"), @"Pass 50 maps of any type with an A rank or higher.");
        /// <summary>
        /// "Spend 50 hours in game"
        /// </summary>
        public static LocalisableString QuestName20 => new TranslatableString(getKey(@"quest_name_20"), @"Spend 50 hours in game");
        /// <summary>
        /// "Accumulate 50 hours of total play time on maps."
        /// </summary>
        public static LocalisableString QuestDesc20 => new TranslatableString(getKey(@"quest_desc_20"), @"Accumulate 50 hours of total play time on maps.");
        /// <summary>
        /// "Pass 5 maps with Anti Aim Assist"
        /// </summary>
        public static LocalisableString QuestName21 => new TranslatableString(getKey(@"quest_name_21"), @"Pass 5 maps with Anti Aim Assist");
        /// <summary>
        /// "Pass 5 maps with the Anti Aim Assist (AA!) mod enabled with a B rank or higher."
        /// </summary>
        public static LocalisableString QuestDesc21 => new TranslatableString(getKey(@"quest_desc_21"), @"Pass 5 maps with the Anti Aim Assist (AA!) mod enabled with a B rank or higher.");
        /// <summary>
        /// "Add a map to favorites"
        /// </summary>
        public static LocalisableString QuestName22 => new TranslatableString(getKey(@"quest_name_22"), @"Add a map to favorites");
        /// <summary>
        /// "Add any map to your favorite list."
        /// </summary>
        public static LocalisableString QuestDesc22 => new TranslatableString(getKey(@"quest_desc_22"), @"Add any map to your favorite list.");
        /// <summary>
        /// "Pass 10 maps in each mode"
        /// </summary>
        public static LocalisableString QuestName23 => new TranslatableString(getKey(@"quest_name_23"), @"Pass 10 maps in each mode");
        /// <summary>
        /// "Pass at least 10 maps in each of the 4 main modes (Standard, Taiko, Catch, Mania) with a B rank or higher. Total 40 maps."
        /// </summary>
        public static LocalisableString QuestDesc23 => new TranslatableString(getKey(@"quest_desc_23"), @"Pass at least 10 maps in each of the 4 main modes (Standard, Taiko, Catch, Mania) with a B rank or higher. Total 40 maps.");
        /// <summary>
        /// "Spend 100 hours in game"
        /// </summary>
        public static LocalisableString QuestName24 => new TranslatableString(getKey(@"quest_name_24"), @"Spend 100 hours in game");
        /// <summary>
        /// "Accumulate 100 hours of total play time on maps."
        /// </summary>
        public static LocalisableString QuestDesc24 => new TranslatableString(getKey(@"quest_desc_24"), @"Accumulate 100 hours of total play time on maps.");
        /// <summary>
        /// "Pass 10 ten-minute maps"
        /// </summary>
        public static LocalisableString QuestName25 => new TranslatableString(getKey(@"quest_name_25"), @"Pass 10 ten-minute maps");
        /// <summary>
        /// "Successfully pass 10 maps with a duration of 10 minutes or more with a B rank or higher."
        /// </summary>
        public static LocalisableString QuestDesc25 => new TranslatableString(getKey(@"quest_desc_25"), @"Successfully pass 10 maps with a duration of 10 minutes or more with a B rank or higher.");

        /// <summary>
        /// Gets the localised name for a quest.
        /// </summary>
        public static LocalisableString GetQuestName(int id, string defaultName)
        {
            switch (id)
            {
                case 1: return QuestName1;
                case 2: return QuestName2;
                case 3: return QuestName3;
                case 4: return QuestName4;
                case 5: return QuestName5;
                case 6: return QuestName6;
                case 7: return QuestName7;
                case 8: return QuestName8;
                case 9: return QuestName9;
                case 10: return QuestName10;
                case 11: return QuestName11;
                case 12: return QuestName12;
                case 13: return QuestName13;
                case 14: return QuestName14;
                case 15: return QuestName15;
                case 16: return QuestName16;
                case 17: return QuestName17;
                case 18: return QuestName18;
                case 19: return QuestName19;
                case 20: return QuestName20;
                case 21: return QuestName21;
                case 22: return QuestName22;
                case 23: return QuestName23;
                case 24: return QuestName24;
                case 25: return QuestName25;
                default: return defaultName;
            }
        }

        /// <summary>
        /// Gets the localised description for a quest.
        /// </summary>
        public static LocalisableString GetQuestDesc(int id, string defaultDesc)
        {
            switch (id)
            {
                case 1: return QuestDesc1;
                case 2: return QuestDesc2;
                case 3: return QuestDesc3;
                case 4: return QuestDesc4;
                case 5: return QuestDesc5;
                case 6: return QuestDesc6;
                case 7: return QuestDesc7;
                case 8: return QuestDesc8;
                case 9: return QuestDesc9;
                case 10: return QuestDesc10;
                case 11: return QuestDesc11;
                case 12: return QuestDesc12;
                case 13: return QuestDesc13;
                case 14: return QuestDesc14;
                case 15: return QuestDesc15;
                case 16: return QuestDesc16;
                case 17: return QuestDesc17;
                case 18: return QuestDesc18;
                case 19: return QuestDesc19;
                case 20: return QuestDesc20;
                case 21: return QuestDesc21;
                case 22: return QuestDesc22;
                case 23: return QuestDesc23;
                case 24: return QuestDesc24;
                case 25: return QuestDesc25;
                default: return defaultDesc;
            }
        }

        public static LocalisableString ConfigBackupHeader => new TranslatableString(getKey(@"config_backup_header"), @"Server configuration backup");

        public static LocalisableString ConfigBackupSave => new TranslatableString(getKey(@"config_backup_save"), @"Save configurations");

        public static LocalisableString ConfigBackupSaveHint => new TranslatableString(getKey(@"config_backup_save_hint"), @"Replaces the single server slot with input.json, game.ini, mosu.ini and server_profiles.json.");

        public static LocalisableString ConfigBackupDate => new TranslatableString(getKey(@"config_backup_date"), @"Configuration save date");

        public static LocalisableString ConfigBackupLoad => new TranslatableString(getKey(@"config_backup_load"), @"Load configurations and restart");

        public static LocalisableString ConfigBackupLoadMobile => new TranslatableString(getKey(@"config_backup_load_mobile"), @"Load configurations");

        public static LocalisableString ConfigBackupLoadHint => new TranslatableString(getKey(@"config_backup_load_hint"), @"Replaces local configuration files on the next client launch.");

        public static LocalisableString ConfigBackupNotSaved => new TranslatableString(getKey(@"config_backup_not_saved"), @"Configurations have not been saved");

        public static LocalisableString ConfigBackupLoginRequired => new TranslatableString(getKey(@"config_backup_login_required"), @"Sign in to use server configuration backups");

        public static LocalisableString ConfigBackupSaving => new TranslatableString(getKey(@"config_backup_saving"), @"Saving configurations...");

        public static LocalisableString ConfigBackupDownloading => new TranslatableString(getKey(@"config_backup_downloading"), @"Downloading configurations...");

        public static LocalisableString ConfigBackupRestarting => new TranslatableString(getKey(@"config_backup_restarting"), @"Configuration downloaded. Restarting...");

        public static LocalisableString ConfigBackupRestartManually => new TranslatableString(getKey(@"config_backup_restart_manually"), @"Configuration downloaded. Close and reopen the game to apply it.");

        public static LocalisableString ConfigBackupLocalSaveFailed => new TranslatableString(getKey(@"config_backup_local_save_failed"), @"Local configuration files could not be saved.");

        public static LocalisableString ConfigBackupRestartUnavailable => new TranslatableString(getKey(@"config_backup_restart_unavailable"), @"This client cannot restart automatically; configuration was not replaced.");

        public static LocalisableString ConfigBackupLoadConfirmation => new TranslatableString(getKey(@"config_backup_load_confirmation"), @"Replace local configurations with the server copy and restart the client?");

        public static LocalisableString ConfigBackupLoadConfirmationMobile => new TranslatableString(getKey(@"config_backup_load_confirmation_mobile"), @"Replace local configurations with the server copy? Close and reopen the game afterwards to apply them.");

        public static LocalisableString ConfigBackupError(string error) => new TranslatableString(getKey(@"config_backup_error"), @"Configuration backup error: {0}", error);

        /// <summary>
        /// "Single-threaded mode is enabled. This can substantially reduce gameplay FPS and make the update rate fluctuate with rendering. Use Multi-threaded under Settings > Graphics > Threading mode."
        /// </summary>
        public static LocalisableString SingleThreadedExecutionWarning => new TranslatableString(
            getKey(@"single_threaded_execution_warning"),
            @"Single-threaded mode is enabled. This can substantially reduce gameplay FPS and make the update rate fluctuate with rendering. Use Multi-threaded under Settings > Graphics > Threading mode.");

        public static LocalisableString RestrictionTitle => new TranslatableString(getKey(@"restriction_title"), @"АККАУНТ ОГРАНИЧЕН");

        public static LocalisableString RestrictionGenericBody => new TranslatableString(getKey(@"restriction_generic_body"), @"На аккаунте действует рестрикт.");

        public static LocalisableString RestrictionCheatingBody(string username) => new TranslatableString(
            getKey(@"restriction_cheating_body"),
            @"Рестрикт выдан за читы. Чтобы запросить снятие рестрикта, напишите администратору в Discord: {0}",
            username);

        public static LocalisableString RestrictionCopyDiscord => new TranslatableString(getKey(@"restriction_copy_discord"), @"Скопировать xtillius1");

        public static LocalisableString RestrictionOpenDiscord => new TranslatableString(getKey(@"restriction_open_discord"), @"Открыть Discord");

        private static string getKey(string key) => $@"{prefix}:{key}";

    }
}
