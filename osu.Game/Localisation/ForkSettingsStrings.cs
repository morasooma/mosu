// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ForkSettingsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.ForkSettings";

        public static LocalisableString AimAssistHeader => new TranslatableString(getKey(@"aim_assist_header"), @"Aim Assist");
        public static LocalisableString AimAssistEnableCaption => new TranslatableString(getKey(@"aim_assist_enable_caption"), @"Enable aim assist");
        public static LocalisableString AimAssistEnableHint => new TranslatableString(getKey(@"aim_assist_enable_hint"), @"Enables assistance in moving the cursor to notes and sliders.");
        public static LocalisableString AimAssistShowTargetsCaption => new TranslatableString(getKey(@"aim_assist_show_targets_caption"), @"Show aim assist targets");
        public static LocalisableString AimAssistShowTargetsHint => new TranslatableString(getKey(@"aim_assist_show_targets_hint"), @"Shows debug targets and areas of aim assist operation.");
        public static LocalisableString AimAssistShowFlowCaption => new TranslatableString(getKey(@"aim_assist_show_flow_caption"), @"Show flow-debug assist");
        public static LocalisableString AimAssistShowFlowHint => new TranslatableString(getKey(@"aim_assist_show_flow_hint"), @"Shows how the aim assist annotates pattern, which chain is considered flow, and along which trajectory the cursor is moved.");
        public static LocalisableString AimAssistStrengthCaption => new TranslatableString(getKey(@"aim_assist_strength_caption"), @"Assist strength");
        public static LocalisableString AimAssistStrengthHint => new TranslatableString(getKey(@"aim_assist_strength_hint"), @"How strongly assist pulls the cursor to the current target.");
        public static LocalisableString AimAssistFovCaption => new TranslatableString(getKey(@"aim_assist_fov_caption"), @"FOV radius");
        public static LocalisableString AimAssistFovHint => new TranslatableString(getKey(@"aim_assist_fov_hint"), @"Radius within which the assist begins to confidently lock onto the target.");
        public static LocalisableString AimAssistIntentCaption => new TranslatableString(getKey(@"aim_assist_intent_caption"), @"Intent threshold");
        public static LocalisableString AimAssistIntentHint => new TranslatableString(getKey(@"aim_assist_intent_hint"), @"How clearly the player's movement must be directed towards the target for assist to help.");
        public static LocalisableString AimAssistFrictionCaption => new TranslatableString(getKey(@"aim_assist_friction_caption"), @"Dynamic friction");
        public static LocalisableString AimAssistFrictionHint => new TranslatableString(getKey(@"aim_assist_friction_hint"), @"Additional ""stickiness"" near target. Higher values stick cursor tighter.");
        public static LocalisableString AimAssistJitterCaption => new TranslatableString(getKey(@"aim_assist_jitter_caption"), @"Anti-jitter after click");
        public static LocalisableString AimAssistJitterHint => new TranslatableString(getKey(@"aim_assist_jitter_hint"), @"Smooths tiny cursor shake right after click.");
        public static LocalisableString AimAssistOvershootCaption => new TranslatableString(getKey(@"aim_assist_overshoot_caption"), @"Overshoot allowance");
        public static LocalisableString AimAssistOvershootHint => new TranslatableString(getKey(@"aim_assist_overshoot_hint"), @"How much the assist forgives overshooting the center while still holding the target.");
        public static LocalisableString AimAssistCenterCaption => new TranslatableString(getKey(@"aim_assist_center_caption"), @"Center bias");
        public static LocalisableString AimAssistCenterHint => new TranslatableString(getKey(@"aim_assist_center_hint"), @"How strongly the assist targets the center of the note. Lower values mean more freedom inside the object.");

        public static LocalisableString ForkSettingsHeader => new TranslatableString(getKey(@"fork_settings_header"), @"General");
        public static LocalisableString ConnectionHeader => new TranslatableString(getKey(@"connection_header"), @"Connection");
        public static LocalisableString ConnectionProxyCaption => new TranslatableString(getKey(@"connection_proxy_caption"), @"Use backup connection proxy");
        public static LocalisableString ConnectionProxyHint => new TranslatableString(getKey(@"connection_proxy_hint"), @"Routes Mosu API, website, multiplayer and update traffic through the backup proxy. Use this if the main server is unavailable from your region.");
        public static LocalisableString ConnectionProxyRestartBody => new TranslatableString(getKey(@"connection_proxy_restart_body"), @"The game will restart to apply the new connection route.");
        public static LocalisableString PerformanceHeader => new TranslatableString(getKey(@"performance_header"), @"Performance");
        public static LocalisableString InterfaceHeader => new TranslatableString(getKey(@"interface_header"), @"Interface");
        public static LocalisableString DebugHeader => new TranslatableString(getKey(@"debug_header"), @"Debug");
        public static LocalisableString ShowBeatmapsWithMissingAudioCaption => new TranslatableString(getKey(@"show_beatmaps_with_missing_audio_caption"), @"Show beatmaps without audio");
        public static LocalisableString ShowBeatmapsWithMissingAudioHint => new TranslatableString(getKey(@"show_beatmaps_with_missing_audio_hint"), @"Allows loading beatmaps with missing audio files (silence will be used instead).");
        public static LocalisableString QuickExportLogsBtn => new TranslatableString(getKey(@"quick_export_logs_btn"), @"Quick export logs");
        public static LocalisableString SkinPerfRestartRequired => new TranslatableString(getKey(@"skin_perf_restart_required"), @"Game restart is required for this setting to apply.");
        public static LocalisableString TabletSettingsBtn => new TranslatableString(getKey(@"tablet_settings_btn"), @"Go to tablet settings (Anti-smoothing)");
        public static LocalisableString TabletReconstructorCaption => new TranslatableString(getKey(@"tablet_reconstructor_caption"), @"Enable anti-smoothing (Reconstructor)");
        public static LocalisableString TabletReconstructorWeightCaption => new TranslatableString(getKey(@"tablet_reconstructor_weight_caption"), @"Anti-smoothing strength (Weight)");
        public static LocalisableString TabletChatterCaption => new TranslatableString(getKey(@"tablet_chatter_caption"), @"Enable jitter suppression (CHATTER EXTERMINATOR RAW)");
        public static LocalisableString TabletChatterHint => new TranslatableString(getKey(@"tablet_chatter_hint"), @"The original Kuuube screen filter. Suppresses small cursor movements with minimal latency.");
        public static LocalisableString TabletChatterStrengthCaption => new TranslatableString(getKey(@"tablet_chatter_strength_caption"), @"Jitter suppression strength (2–3 drag, 5–6 hover)");
        public static LocalisableString TabletChatterStrengthHint => new TranslatableString(getKey(@"tablet_chatter_strength_hint"), @"Original filter recommendations: 2–3 while dragging and 5–6 while hovering.");
        public static LocalisableString TabletRadialFollowCaption => new TranslatableString(getKey(@"tablet_radial_follow_caption"), @"Enable dynamic smoothing (Radial Follow)");
        public static LocalisableString TabletRadialFollowHint => new TranslatableString(getKey(@"tablet_radial_follow_hint"), @"Stabilises small movements while preserving responsiveness during fast movements.");
        public static LocalisableString TabletRadialFollowOuterRadiusCaption => new TranslatableString(getKey(@"tablet_radial_follow_outer_radius_caption"), @"Radial Follow: outer radius (px)");
        public static LocalisableString TabletRadialFollowOuterRadiusHint => new TranslatableString(getKey(@"tablet_radial_follow_outer_radius_hint"), @"Maximum cursor lag from the real position. Original value: 5 px.");
        public static LocalisableString TabletRadialFollowInnerRadiusCaption => new TranslatableString(getKey(@"tablet_radial_follow_inner_radius_caption"), @"Radial Follow: inner radius (px)");
        public static LocalisableString TabletRadialFollowInnerRadiusHint => new TranslatableString(getKey(@"tablet_radial_follow_inner_radius_hint"), @"Dead zone within which no movement is created. Original value: 0 px.");
        public static LocalisableString TabletRadialFollowSmoothingCaption => new TranslatableString(getKey(@"tablet_radial_follow_smoothing_caption"), @"Radial Follow: smoothing coefficient");
        public static LocalisableString TabletRadialFollowSmoothingHint => new TranslatableString(getKey(@"tablet_radial_follow_smoothing_hint"), @"Higher values move the cursor more slowly from the outer radius to the inner radius. Original value: 0.95.");
        public static LocalisableString TabletRadialFollowSoftKneeCaption => new TranslatableString(getKey(@"tablet_radial_follow_soft_knee_caption"), @"Radial Follow: transition softness");
        public static LocalisableString TabletRadialFollowSoftKneeHint => new TranslatableString(getKey(@"tablet_radial_follow_soft_knee_hint"), @"Controls transition smoothness at the outer-radius boundary. Original value: 1.");
        public static LocalisableString TabletRadialFollowLeakCaption => new TranslatableString(getKey(@"tablet_radial_follow_leak_caption"), @"Radial Follow: residual smoothing");
        public static LocalisableString TabletRadialFollowLeakHint => new TranslatableString(getKey(@"tablet_radial_follow_leak_hint"), @"Amount of smoothing outside the outer radius. Original value: 0%.");
        public static LocalisableString UnsupportedFontHeader => new TranslatableString(getKey(@"unsupported_font_header"), @"TTF/OTF format is not supported!");
        public static LocalisableString UnsupportedFontBody => new TranslatableString(getKey(@"unsupported_font_body"), @"The font must be converted to BMFont format (.fnt + .png) to work.\nOpen the guide to learn how to do this.");
        public static LocalisableString UnsupportedFontGuideButton => new TranslatableString(getKey(@"unsupported_font_guide_button"), @"Open guide (browser)");
        public static LocalisableString UnsupportedFontCancelButton => new TranslatableString(getKey(@"unsupported_font_cancel_button"), @"Cancel");
        public static LocalisableString DownloadMirrorCaption => new TranslatableString(getKey(@"download_mirror_caption"), @"Beatmap download mirror");
        public static LocalisableString DownloadMirrorAuto => new TranslatableString(getKey(@"download_mirror_auto"), @"Auto-select (fastest)");
        public static LocalisableString DownloadMirrorSayobot => new TranslatableString(getKey(@"download_mirror_sayobot"), @"Sayobot");
        public static LocalisableString DownloadMirrorNerinyan => new TranslatableString(getKey(@"download_mirror_nerinyan"), @"Nerinyan");
        public static LocalisableString DownloadMirrorMino => new TranslatableString(getKey(@"download_mirror_mino"), @"Mino");
        public static LocalisableString DownloadMirrorBeatConnect => new TranslatableString(getKey(@"download_mirror_beatconnect"), @"BeatConnect");
        public static LocalisableString DownloadMirrorChimu => new TranslatableString(getKey(@"download_mirror_chimu"), @"Chimu");
        public static LocalisableString DownloadMirrorOsuDirect => new TranslatableString(getKey(@"download_mirror_osudirect"), @"OsuDirect");
        public static LocalisableString DownloadMirrorServer => new TranslatableString(getKey(@"download_mirror_server"), @"Server (official)");
        public static LocalisableString PlaylistDownloadAll => new TranslatableString(getKey(@"playlist_download_all"), @"Download all beatmaps");
        public static LocalisableString PlaylistNoMapsToDownload => new TranslatableString(getKey(@"playlist_no_maps_to_download"), @"There are no maps to download in the playlist");
        public static LocalisableString PlaylistDownloadProgress(int local, int total, int active, int queued) => new TranslatableString(getKey(@"playlist_download_progress"), @"Download all beatmaps ({0}/{1}, {2} active, {3} queued)", local, total, active, queued);
        public static LocalisableString PlaylistMissingSets(int missing) => new TranslatableString(getKey(@"playlist_missing_sets"), @"Download all beatmaps ({0} sets not downloaded yet)", missing);
        public static LocalisableString PlaylistAllDownloaded => new TranslatableString(getKey(@"playlist_all_downloaded"), @"All playlist beatmaps are already downloaded");
        public static LocalisableString UseOfficialOsuBeatmapServiceCaption => new TranslatableString(getKey(@"use_official_osu_beatmap_service_caption"), @"Download official beatmaps directly from osu!");
        public static LocalisableString UseOfficialOsuBeatmapServiceHint => new TranslatableString(getKey(@"use_official_osu_beatmap_service_hint"), @"Uses the official lazer token from game.ini for beatmap search, metadata and downloads. Scores and realtime services remain connected to Mosu.");
        public static LocalisableString UseOfficialOsuBeatmapServiceWarning => new TranslatableString(getKey(@"use_official_osu_beatmap_service_warning"), @"This feature uses the official osu! account found in game.ini. Mosu is not responsible for possible account restrictions. Account data and the token are not sent to Mosu servers; the client uses them locally and sends the token only to official osu! services for these requests.");
        public static LocalisableString OfficialOsuAccountStatusCaption => new TranslatableString(getKey(@"official_osu_account_status_caption"), @"Official osu! beatmap account");
        public static LocalisableString OfficialOsuAccountStatus(LocalisableString status) => new TranslatableString(getKey(@"official_osu_account_status"), @"Official osu! beatmap account: {0}", status);
        public static LocalisableString OfficialOsuStatusDisabled => new TranslatableString(getKey(@"official_osu_status_disabled"), @"Disabled");
        public static LocalisableString OfficialOsuStatusTokenMissing => new TranslatableString(getKey(@"official_osu_status_token_missing"), @"No token in game.ini");
        public static LocalisableString OfficialOsuStatusConnecting => new TranslatableString(getKey(@"official_osu_status_connecting"), @"Checking account...");
        public static LocalisableString OfficialOsuStatusConnected(LocalisableString username) => new TranslatableString(getKey(@"official_osu_status_connected"), @"Connected: {0}", username);
        public static LocalisableString OfficialOsuStatusAuthenticationFailed => new TranslatableString(getKey(@"official_osu_status_authentication_failed"), @"Authorisation failed");
        public static LocalisableString OfficialOsuStatusNetworkUnavailable => new TranslatableString(getKey(@"official_osu_status_network_unavailable"), @"osu! unavailable");
        public static LocalisableString OfficialOsuRetryButton => new TranslatableString(getKey(@"official_osu_retry_button"), @"Retry");
        public static LocalisableString OfficialOsuTokenMissing => new TranslatableString(getKey(@"official_osu_token_missing"), @"Official osu! beatmap integration: no token was found in game.ini.");
        public static LocalisableString OfficialOsuTokenInvalid => new TranslatableString(getKey(@"official_osu_token_invalid"), @"Official osu! beatmap integration: the token stored in game.ini is invalid.");
        public static LocalisableString OfficialOsuAuthenticationFailed => new TranslatableString(getKey(@"official_osu_authentication_failed"), @"Official osu! beatmap integration could not authorise the account. Open official lazer and sign in again.");
        public static LocalisableString OfficialOsuNetworkUnavailable => new TranslatableString(getKey(@"official_osu_network_unavailable"), @"Official osu! beatmap integration could not reach osu!. Mosu will continue using its normal beatmap source.");
        public static LocalisableString UncappedFpsCaption => new TranslatableString(getKey(@"uncapped_fps_caption"), @"Without FPS limit");
        public static LocalisableString UncappedFpsHint => new TranslatableString(getKey(@"uncapped_fps_hint"), @"Enables uncapped frame rate mode and removes the built-in 1000 FPS ceiling. Usually this only increases heat and system load with no noticeable benefit once low frame times are reached.");
        public static LocalisableString LimitMenuFps2xCaption => new TranslatableString(getKey(@"limit_menu_fps_2x_caption"), @"Limit menu FPS to 2× refresh rate");
        public static LocalisableString LimitMenuFps2xHint => new TranslatableString(getKey(@"limit_menu_fps_2x_hint"), @"Caps the frame rate in menus and song select to twice your monitor's refresh rate (e.g. 120 FPS at 60Hz or 280 FPS at 140Hz) to reduce heat and GPU load. Automatically unlocks during gameplay and replay playback.");
        public static LocalisableString WindowsUltraPerfCaption => new TranslatableString(getKey(@"windows_ultra_perf_caption"), @"Windows ultra latency mode");
        public static LocalisableString WindowsUltraPerfHint => new TranslatableString(getKey(@"windows_ultra_perf_hint"), @"Aggressive mode for Windows: sets process priority to realtime, enables MMCSS/Highest for input, update and draw threads, requests a system timer resolution below 1ms, and automatically applies basic Windows optimizations.");
        public static LocalisableString Use8kPollingRateCaption => new TranslatableString(getKey(@"use_8k_polling_rate_caption"), @"Enable 8000Hz input polling");
        public static LocalisableString Use8kPollingRateHint => new TranslatableString(getKey(@"use_8k_polling_rate_hint"), @"Sets input thread limit to 8000Hz instead of 1000Hz. Increases CPU load.");
        public static LocalisableString SkinPerfCaption => new TranslatableString(getKey(@"skin_perf_caption"), @"Skin performance mode");
        public static LocalisableString SkinPerfHint => new TranslatableString(getKey(@"skin_perf_hint"), @"Freezes animations of legacy skins and disables expensive HUD elements like Argon wireframes, glow, rolling counters, and pop effects. Argon hit circles use a simplified body without the hit flash/explosion animation.");
        public static LocalisableString PerformanceOptimisationSettingsButton => new TranslatableString(getKey(@"performance_optimisation_settings_button"), @"Detailed performance optimisation settings");
        public static LocalisableString PerformanceOptimisationSettingsHeader => new TranslatableString(getKey(@"performance_optimisation_settings_header"), @"Performance optimisation");
        public static LocalisableString PerformanceOptimisationSettingsDescription => new TranslatableString(getKey(@"performance_optimisation_settings_description"), @"Green means recommended, blue means optional, orange means test first, purple means situational, and red means leave disabled.");
        public static LocalisableString RendererOptimisationSettingsHeader => new TranslatableString(getKey(@"renderer_optimisation_settings_header"), @"Renderer and latency");
        public static LocalisableString PerformanceImpactHigh => new TranslatableString(getKey(@"performance_impact_high"), @"HIGH GAIN");
        public static LocalisableString PerformanceImpactMedium => new TranslatableString(getKey(@"performance_impact_medium"), @"MEDIUM GAIN");
        public static LocalisableString PerformanceImpactLow => new TranslatableString(getKey(@"performance_impact_low"), @"SMALL GAIN");
        public static LocalisableString PerformanceImpactLatency => new TranslatableString(getKey(@"performance_impact_latency"), @"LOWER LATENCY");
        public static LocalisableString PerformanceImpactMixed => new TranslatableString(getKey(@"performance_impact_mixed"), @"TRADE-OFF");
        public static LocalisableString PerformanceImpactContextual => new TranslatableString(getKey(@"performance_impact_contextual"), @"SITUATIONAL");
        public static LocalisableString PerformanceImpactNegative => new TranslatableString(getKey(@"performance_impact_negative"), @"SLOWER");
        public static LocalisableString PerformanceImpactGuidanceHigh => new TranslatableString(getKey(@"performance_impact_guidance_high"), @"Recommended: the improvement can be noticeable.");
        public static LocalisableString PerformanceImpactGuidanceMedium => new TranslatableString(getKey(@"performance_impact_guidance_medium"), @"Recommended: mainly improves frame-time stability rather than the average FPS counter.");
        public static LocalisableString PerformanceImpactGuidanceLow => new TranslatableString(getKey(@"performance_impact_guidance_low"), @"Optional: the improvement is small and may not be noticeable.");
        public static LocalisableString PerformanceImpactGuidanceLatency => new TranslatableString(getKey(@"performance_impact_guidance_latency"), @"For minimum latency only: may increase CPU usage or slightly reduce average FPS.");
        public static LocalisableString PerformanceImpactGuidanceMixed => new TranslatableString(getKey(@"performance_impact_guidance_mixed"), @"Test on your PC first: one metric improved while another became worse.");
        public static LocalisableString PerformanceImpactGuidanceContextual => new TranslatableString(getKey(@"performance_impact_guidance_contextual"), @"Enable only when the described hardware or display use case applies.");
        public static LocalisableString PerformanceImpactGuidanceNegative => new TranslatableString(getKey(@"performance_impact_guidance_negative"), @"Leave disabled: this isolated test became slower.");
        public static LocalisableString PerformanceImpactDescription(LocalisableString impact, LocalisableString guidance, LocalisableString evidence) =>
            new TranslatableString(getKey(@"performance_impact_description"), @"{0}: {1}{3}Mosu controlled benchmark result: {2}", impact, guidance, evidence, "\n");
        public static LocalisableString PerformanceEvidenceWindowsUltra => new TranslatableString(getKey(@"performance_evidence_windows_ultra"), @"average FPS -1.0%; the slowest Update frames improved by 12%.");
        public static LocalisableString PerformanceEvidenceInput8k => new TranslatableString(getKey(@"performance_evidence_input_8k"), @"average FPS -0.8%; the slowest input-thread frames improved by 13% in the replay workload.");
        public static LocalisableString PerformanceEvidenceAtlas4096 => new TranslatableString(getKey(@"performance_evidence_atlas_4096"), @"average FPS -0.5%, with several severe frame-time spikes on the tested integrated GPU.");
        public static LocalisableString PerformanceEvidenceDeferredVertexBatching => new TranslatableString(getKey(@"performance_evidence_deferred_vertex_batching"), @"average FPS -1.8%; the slowest Draw and Update frames were about 28% worse.");
        public static LocalisableString PerformanceEvidenceDeferredDirectVertex => new TranslatableString(getKey(@"performance_evidence_deferred_direct_vertex"), @"average FPS -4.0%, while the slowest Draw and Update frames improved by about 13%.");
        public static LocalisableString PerformanceEvidenceDeferredDirectUniform => new TranslatableString(getKey(@"performance_evidence_deferred_direct_uniform"), @"average FPS -0.7%; the slowest Update frames improved by 5%.");
        public static LocalisableString PerformanceEvidencePipelineCache => new TranslatableString(getKey(@"performance_evidence_pipeline_cache"), @"average FPS -1.6%; the slowest Draw frames improved by 14%, but Update frames became 20% worse.");
        public static LocalisableString PerformanceEvidenceStaticLifetime => new TranslatableString(getKey(@"performance_evidence_static_lifetime"), @"child lifetime-check stalls improved by 78%, but average FPS fell by 4.2% in this scene.");
        public static LocalisableString PerformanceEvidenceAllowTearing => new TranslatableString(getKey(@"performance_evidence_allow_tearing"), @"required for uncapped presentation in borderless mode; the fullscreen replay result was inconclusive.");
        public static LocalisableString PerformanceEvidenceSpinWait => new TranslatableString(getKey(@"performance_evidence_spin_wait"), @"average FPS was unchanged; the slowest Update and input-thread frames improved by 8% and 12%.");
        public static LocalisableString PerformanceEvidenceSkinMaster => new TranslatableString(getKey(@"performance_evidence_skin_master"), @"combined mode improved the slowest Draw frames by 21%; individual components previously reached double-digit FPS gains.");
        public static LocalisableString PerformanceEvidenceSkinFreezeAnimations => new TranslatableString(getKey(@"performance_evidence_skin_freeze_animations"), @"average FPS with the classic skin was unchanged; the slowest Draw frames improved by 10%.");
        public static LocalisableString PerformanceEvidenceSkinSimplifyEffects => new TranslatableString(getKey(@"performance_evidence_skin_simplify_effects"), @"average FPS stayed within normal variance; slowest-1% FPS improved by 1.6%.");
        public static LocalisableString PerformanceEvidenceSkinOptimiseTextures => new TranslatableString(getKey(@"performance_evidence_skin_optimise_textures"), @"average FPS with the classic skin was unchanged; the slowest Draw and Update frames improved by 21% and 31%.");
        public static LocalisableString PerformanceEvidenceSkinSimplifyHud => new TranslatableString(getKey(@"performance_evidence_skin_simplify_hud"), @"the slowest Draw and Update frames improved by 24% and 21%; slowest-1% FPS improved by 1.8%.");
        public static LocalisableString PerformanceEvidenceSkinSimplifyCounters => new TranslatableString(getKey(@"performance_evidence_skin_simplify_counters"), @"average FPS stayed within normal variance; slowest-1% FPS improved by 1.4%.");
        public static LocalisableString PerformanceEvidenceSkinDisableKiai => new TranslatableString(getKey(@"performance_evidence_skin_disable_kiai"), @"the slowest Update frames with the classic skin improved by 29%; slowest-1% FPS improved by 3.1%.");
        public static LocalisableString PerformanceEvidenceSkinBlackBackground => new TranslatableString(getKey(@"performance_evidence_skin_black_background"), @"slowest-1% FPS improved by 5.3% in this run; a prior controlled integrated-GPU test measured 14-16% higher average FPS.");
        public static LocalisableString PerformanceEvidenceArgonFollowRing => new TranslatableString(getKey(@"performance_evidence_argon_follow_ring"), @"average FPS +1.1%, slowest-1% FPS +5.9%, and the slowest Draw frames improved by 18%.");
        public static LocalisableString PerformanceImpactCaption(LocalisableString caption, LocalisableString impact) =>
            new TranslatableString(getKey(@"performance_impact_caption"), @"{0} · {1} impact", caption, impact);
        public static LocalisableString PerformanceImpactEvidence(LocalisableString evidence) =>
            new TranslatableString(getKey(@"performance_impact_evidence"), @"Isolated replay benchmark (2026-07-27): {0}", evidence);
        public static LocalisableString SkinPerfSettingsHeader => new TranslatableString(getKey(@"skin_perf_settings_header"), @"Skin performance optimisation");
        public static LocalisableString SkinPerfSettingsDescription => new TranslatableString(getKey(@"skin_perf_settings_description"), @"Choose which optimisations are used while skin performance mode is enabled. Changes take effect immediately.");
        public static LocalisableString SkinPerfFreezeAnimationsCaption => new TranslatableString(getKey(@"skin_perf_freeze_animations_caption"), @"Freeze legacy skin animations");
        public static LocalisableString SkinPerfFreezeAnimationsHint => new TranslatableString(getKey(@"skin_perf_freeze_animations_hint"), @"Shows the first frame of animated legacy skin elements instead of playing their animations.");
        public static LocalisableString SkinPerfSimplifyEffectsCaption => new TranslatableString(getKey(@"skin_perf_simplify_effects_caption"), @"Simplify judgement effects");
        public static LocalisableString SkinPerfSimplifyEffectsHint => new TranslatableString(getKey(@"skin_perf_simplify_effects_hint"), @"Disables judgement particles and uses a shorter fade to reduce animation work.");
        public static LocalisableString SkinPerfOptimiseTexturesCaption => new TranslatableString(getKey(@"skin_perf_optimise_textures_caption"), @"Optimise legacy skin textures");
        public static LocalisableString SkinPerfOptimiseTexturesHint => new TranslatableString(getKey(@"skin_perf_optimise_textures_hint"), @"Uses lower-resolution non-critical legacy textures when available and reduces their upload size.");
        public static LocalisableString SkinPerfSimplifyHudCaption => new TranslatableString(getKey(@"skin_perf_simplify_hud_caption"), @"Simplify gameplay HUD animations");
        public static LocalisableString SkinPerfSimplifyHudHint => new TranslatableString(getKey(@"skin_perf_simplify_hud_hint"), @"Removes HUD transition animations and expensive visual updates during gameplay.");
        public static LocalisableString SkinPerfSimplifyCountersCaption => new TranslatableString(getKey(@"skin_perf_simplify_counters_caption"), @"Simplify rolling counters");
        public static LocalisableString SkinPerfSimplifyCountersHint => new TranslatableString(getKey(@"skin_perf_simplify_counters_hint"), @"Updates score, pp and unstable-rate counters without rolling-number animations.");
        public static LocalisableString SkinPerfDisableKiaiFlashingCaption => new TranslatableString(getKey(@"skin_perf_disable_kiai_flashing_caption"), @"Disable legacy Kiai flashing");
        public static LocalisableString SkinPerfDisableKiaiFlashingHint => new TranslatableString(getKey(@"skin_perf_disable_kiai_flashing_hint"), @"Prevents duplicated legacy skin textures and beat-synchronised flashing during Kiai sections.");

        public static LocalisableString SkinPerfBlackBackgroundCaption => new TranslatableString(getKey(@"skin_perf_black_background_caption"), @"Black background at high dim");
        public static LocalisableString SkinPerfBlackBackgroundHint => new TranslatableString(getKey(@"skin_perf_black_background_hint"), @"Skips drawing the beatmap background entirely when background dim is 75% or higher. The largest single FPS gain on integrated GPUs (about +14% measured).");
        public static LocalisableString SkinPerfArgonFollowRingCaption => new TranslatableString(getKey(@"skin_perf_argon_follow_ring_caption"), @"Ring-only slider follow circle");
        public static LocalisableString SkinPerfArgonFollowRingHint => new TranslatableString(getKey(@"skin_perf_argon_follow_ring_hint"), @"Draws the Argon slider follow circle as a ring without its translucent interior fill, reducing GPU load while tracking sliders. Takes effect on the next gameplay start.");
        public static LocalisableString SeparateSkinsPerRulesetCaption => new TranslatableString(getKey(@"separate_skins_per_ruleset_caption"), @"Use a separate skin for each game mode");
        public static LocalisableString SeparateSkinsPerRulesetHint => new TranslatableString(getKey(@"separate_skins_per_ruleset_hint"), @"Selects and remembers a different skin for osu!, osu!taiko, osu!catch and osu!mania.");
        public static LocalisableString OsuSkinCaption => new TranslatableString(getKey(@"osu_skin_caption"), @"osu! skin");
        public static LocalisableString TaikoSkinCaption => new TranslatableString(getKey(@"taiko_skin_caption"), @"osu!taiko skin");
        public static LocalisableString CatchSkinCaption => new TranslatableString(getKey(@"catch_skin_caption"), @"osu!catch skin");
        public static LocalisableString ManiaSkinCaption => new TranslatableString(getKey(@"mania_skin_caption"), @"osu!mania skin");
        public static LocalisableString DodgeSkinCaption => new TranslatableString(getKey(@"dodge_skin_caption"), @"Dodge skin");
        public static LocalisableString LargeTextureAtlasCaption => new TranslatableString(getKey(@"large_texture_atlas_caption"), @"Large texture atlas (4096)");
        public static LocalisableString LargeTextureAtlasHint => new TranslatableString(getKey(@"large_texture_atlas_hint"), @"Packs more UI and gameplay textures into each GPU page, reducing texture switches and draw calls. Requires a restart. Disable if an older GPU or OpenGL driver shows texture corruption or excessive memory usage.");
        public static LocalisableString RecommendedRendererPresetBtn => new TranslatableString(getKey(@"recommended_renderer_preset_btn"), @"Apply recommended settings");
        public static LocalisableString DeferredVertexBatchingCaption => new TranslatableString(getKey(@"deferred_vertex_batching_caption"), @"Experimental Deferred vertex batching");
        public static LocalisableString DeferredVertexBatchingHint => new TranslatableString(getKey(@"deferred_vertex_batching_hint"), @"Combines adjacent vertex upload events in the Deferred renderer. Results may vary by GPU and driver; leave disabled unless benchmarks show an improvement.");
        public static LocalisableString DeferredDirectVertexUploadCaption => new TranslatableString(getKey(@"deferred_direct_vertex_upload_caption"), @"Experimental direct Deferred vertex upload");
        public static LocalisableString DeferredDirectVertexUploadHint => new TranslatableString(getKey(@"deferred_direct_vertex_upload_hint"), @"Writes completed primitives straight into mapped GPU vertex buffers, bypassing the intermediate CPU copy and upload-event stream. Results may vary by graphics backend and driver.");
        public static LocalisableString DeferredDirectUniformUploadCaption => new TranslatableString(getKey(@"deferred_direct_uniform_upload_caption"), @"Experimental direct Deferred uniform upload");
        public static LocalisableString DeferredDirectUniformUploadHint => new TranslatableString(getKey(@"deferred_direct_uniform_upload_hint"), @"Writes uniform values straight into mapped GPU buffers and skips the Deferred upload pre-pass when possible. Results may vary by graphics backend and driver.");
        public static LocalisableString VeldridPipelineLookupCacheCaption => new TranslatableString(getKey(@"veldrid_pipeline_lookup_cache_caption"), @"Experimental Veldrid pipeline lookup cache");
        public static LocalisableString VeldridPipelineLookupCacheHint => new TranslatableString(getKey(@"veldrid_pipeline_lookup_cache_hint"), @"Reuses the resolved graphics pipeline while shader, layout, framebuffer and fixed-function state are unchanged, avoiding a full descriptor hash-table lookup for every draw call.");
        public static LocalisableString StaticChildLifetimeCacheCaption => new TranslatableString(getKey(@"static_child_lifetime_cache_caption"), @"Experimental predictive scene lifetime cache");
        public static LocalisableString StaticChildLifetimeCacheHint => new TranslatableString(getKey(@"static_child_lifetime_cache_hint"), @"Skips repeated child-life checks until the next known LifetimeStart/LifetimeEnd boundary. Disable if a custom drawable appears or disappears incorrectly.");
        public static LocalisableString PerfLoggingCaption => new TranslatableString(getKey(@"perf_logging_caption"), @"Performance logging");
        public static LocalisableString PerfLoggingHint => new TranslatableString(getKey(@"perf_logging_hint"), @"When enabled, writes FPS, frame time, CPU, memory, and gameplay state samples to the performance folder.");
        public static LocalisableString AllowTearingCaption => new TranslatableString(getKey(@"allow_tearing_caption"), @"Allow screen tearing (DXGI Tearing)");
        public static LocalisableString AllowTearingHint => new TranslatableString(getKey(@"allow_tearing_hint"), @"Enables DXGI Present Allow Tearing support to unlock frame rate in windowed/borderless modes and activate G-Sync/FreeSync without VSync.");
        public static LocalisableString UpdateSpinWaitCaption => new TranslatableString(getKey(@"update_spin_wait_caption"), @"Spin-Wait mode for Update thread");
        public static LocalisableString UpdateSpinWaitHint => new TranslatableString(getKey(@"update_spin_wait_hint"), @"Completely disables sleeping for the Update thread, forcing it to wait for the next frame in a busy loop (100% load on one core). Reduces latency and increases timing stability.");
        public static LocalisableString DiagnosticsBenchmarkBtn => new TranslatableString(getKey(@"diagnostics_benchmark_btn"), @"Performance Diagnostics");
        public static LocalisableString DiagnosticsOverlayTitle => new TranslatableString(getKey(@"diagnostics_overlay_title"), @"Performance Diagnostics");
        public static LocalisableString DiagnosticsOverlayDescription => new TranslatableString(getKey(@"diagnostics_overlay_description"), @"Every stage plays the complete replay once for warm-up and twice for measurement. Full diagnostics covers every top-level performance setting; settings that cannot apply to the current platform or renderer are listed explicitly instead of disappearing.");
        public static LocalisableString DiagnosticsModeSelectionTitle => new TranslatableString(getKey(@"diagnostics_mode_selection_title"), @"Test mode");
        public static LocalisableString DiagnosticsQuickModeButton => new TranslatableString(getKey(@"diagnostics_quick_mode_button"), @"Quick check · about 4–5 minutes");
        public static LocalisableString DiagnosticsDeepModeButton => new TranslatableString(getKey(@"diagnostics_deep_mode_button"), @"Full diagnostics · about 24–27 minutes");
        public static LocalisableString DiagnosticsExtendedModeButton => new TranslatableString(getKey(@"diagnostics_extended_mode_button"), @"Legacy full diagnostics · about 24–27 minutes");
        public static LocalisableString DiagnosticsRendererModeButton => new TranslatableString(getKey(@"diagnostics_renderer_mode_button"), @"Renderer comparison · about 2 minutes each");
        public static LocalisableString DiagnosticsQuickModeDescription => new TranslatableString(getKey(@"diagnostics_quick_mode_description"), @"Compares the current and Mosu recommended profiles on the selected renderer. Each stage plays the complete replay once for warm-up and twice for measurement.");
        public static LocalisableString DiagnosticsDeepModeDescription => new TranslatableString(getKey(@"diagnostics_deep_mode_description"), @"Tests every top-level renderer, latency and skin-performance setting in isolation, then the complete recommended profile and a final baseline for drift correction. Skin performance is one combined package. Inapplicable settings remain visible with an explicit reason.");
        public static LocalisableString DiagnosticsExtendedModeDescription => new TranslatableString(getKey(@"diagnostics_extended_mode_description"), @"Legacy name for the same complete diagnostics plan.");
        public static LocalisableString DiagnosticsRendererModeDescription => new TranslatableString(getKey(@"diagnostics_renderer_mode_description"), @"Runs the complete Mosu recommended profile on each available renderer. Each renderer plays the complete replay once for warm-up and twice for measurement.");
        public static LocalisableString DiagnosticsUseDefaultSkinCaption => new TranslatableString(getKey(@"diagnostics_use_default_skin_caption"), @"Use default skin");
        public static LocalisableString DiagnosticsUseDefaultSkinHint => new TranslatableString(getKey(@"diagnostics_use_default_skin_hint"), @"If enabled, the built-in Argon skin is used consistently for every stage. If disabled, the currently selected skin is kept. Your original skin is restored when diagnostics ends.");
        public static LocalisableString DiagnosticsSkinPerfCaption => new TranslatableString(getKey(@"diagnostics_skin_perf_caption"), @"Test with skin performance mode");
        public static LocalisableString DiagnosticsSkinPerfHint => new TranslatableString(getKey(@"diagnostics_skin_perf_hint"), @"Enables ForkSkinPerformanceMode only for the duration of the diagnostics.");
        public static LocalisableString DiagnosticsExportZipCaption => new TranslatableString(getKey(@"diagnostics_export_zip_caption"), @"Export results to ZIP");
        public static LocalisableString DiagnosticsExportZipHint => new TranslatableString(getKey(@"diagnostics_export_zip_hint"), @"Creates a support-ready ZIP with a readable report, build and machine information, stutters, raw CSVs, manifests, and runtime logs. The folder opens automatically after completion.");
        public static LocalisableString DiagnosticsStartButton => new TranslatableString(getKey(@"diagnostics_start_button"), @"Start diagnostics");
        public static LocalisableString DiagnosticsResumeButton => new TranslatableString(getKey(@"diagnostics_resume_button"), @"Resume diagnostics");
        public static LocalisableString DiagnosticsCancelButton => new TranslatableString(getKey(@"diagnostics_cancel_button"), @"Cancel diagnostics");
        public static LocalisableString DiagnosticsCloseButton => new TranslatableString(getKey(@"diagnostics_close_button"), @"Close");
        public static LocalisableString DiagnosticsModeQuick => new TranslatableString(getKey(@"diagnostics_mode_quick"), @"Quick");
        public static LocalisableString DiagnosticsModeDeep => new TranslatableString(getKey(@"diagnostics_mode_deep"), @"Full diagnostics");
        public static LocalisableString DiagnosticsModeExtended => new TranslatableString(getKey(@"diagnostics_mode_extended"), @"Extended research");
        public static LocalisableString DiagnosticsModeRenderers => new TranslatableString(getKey(@"diagnostics_mode_renderers"), @"Renderer comparison");
        public static LocalisableString DiagnosticsProgressTitle => new TranslatableString(getKey(@"diagnostics_progress_title"), @"Diagnostics in progress");
        public static LocalisableString DiagnosticsProgressSummary(LocalisableString completed, LocalisableString total, LocalisableString mode) =>
            new TranslatableString(getKey(@"diagnostics_progress_summary"), @"{2} mode · completed {0} of {1} stages", completed, total, mode);
        public static LocalisableString DiagnosticsCurrentStepTitle => new TranslatableString(getKey(@"diagnostics_current_step_title"), @"Current stage");
        public static LocalisableString DiagnosticsCurrentStep(LocalisableString renderer, LocalisableString profile, LocalisableString current, LocalisableString total) =>
            new TranslatableString(getKey(@"diagnostics_current_step"), @"{0} · {1} · stage {2}/{3}", renderer, profile, current, total);
        public static LocalisableString DiagnosticsProgressHint => new TranslatableString(getKey(@"diagnostics_progress_hint"), @"The client may restart between stages. Do not change settings or run heavy applications until the results screen appears.");
        public static LocalisableString DiagnosticsProgressRemaining(LocalisableString minutes) =>
            new TranslatableString(getKey(@"diagnostics_progress_remaining"), @"Estimated time remaining: about {0} min", minutes);
        public static LocalisableString DiagnosticsPausedTitle => new TranslatableString(getKey(@"diagnostics_paused_title"), @"Diagnostics paused");
        public static LocalisableString DiagnosticsPausedDescription(LocalisableString completed, LocalisableString total) =>
            new TranslatableString(getKey(@"diagnostics_paused_description"), @"The saved session was kept. {0} of {1} stages are complete. Resume from the current stage or explicitly cancel the diagnostics.", completed, total);
        public static LocalisableString DiagnosticsPausedStageTitle => new TranslatableString(getKey(@"diagnostics_paused_stage_title"), @"Stage to resume");
        public static LocalisableString DiagnosticsResultsTitle => new TranslatableString(getKey(@"diagnostics_results_title"), @"Diagnostics Results");
        public static LocalisableString DiagnosticsSessionSummary(LocalisableString sessionId, LocalisableString rendererCount, LocalisableString completedCount) =>
            new TranslatableString(getKey(@"diagnostics_session_summary"), @"Session {0}: {2} completed tests across {1} renderer(s).", sessionId, rendererCount, completedCount);
        public static LocalisableString DiagnosticsStuttersHeader(LocalisableString count) => new TranslatableString(getKey(@"diagnostics_stutters_header"), @"Stutters ({0} events)", count);
        public static LocalisableString DiagnosticsStuttersTruncated(LocalisableString remaining) => new TranslatableString(getKey(@"diagnostics_stutters_truncated"), @"... and {0} more events (see ZIP/CSV).", remaining);
        public static LocalisableString DiagnosticsExportPath(LocalisableString path) => new TranslatableString(getKey(@"diagnostics_export_path"), @"ZIP export: {0}", path);
        public static LocalisableString DiagnosticsOpenExportFolder => new TranslatableString(getKey(@"diagnostics_open_export_folder"), @"Open folder with ZIP");
        public static LocalisableString DiagnosticsNoRecommendationTitle => new TranslatableString(getKey(@"diagnostics_no_recommendation_title"), @"No reliable recommendation");
        public static LocalisableString DiagnosticsNoRecommendationBody => new TranslatableString(getKey(@"diagnostics_no_recommendation_body"), @"Not enough stable Current and Recommended runs were found. Repeat the diagnostics.");
        public static LocalisableString DiagnosticsRecommendedWinsTitle => new TranslatableString(getKey(@"diagnostics_recommended_wins_title"), @"Mosu recommended profile performs better");
        public static LocalisableString DiagnosticsCurrentWinsTitle => new TranslatableString(getKey(@"diagnostics_current_wins_title"), @"Keep your current settings");
        public static LocalisableString DiagnosticsSimilarTitle => new TranslatableString(getKey(@"diagnostics_similar_title"), @"Both profiles perform similarly");
        public static LocalisableString DiagnosticsComparisonSummary(LocalisableString renderer, LocalisableString avgDelta, LocalisableString p1Delta, LocalisableString stutterDelta, LocalisableString quality) =>
            new TranslatableString(getKey(@"diagnostics_comparison_summary"), @"Renderer: {0}. Average FPS: {1}; slowest 1% FPS: {2}; stutter difference: {3}. Measurement quality: {4}.", renderer, avgDelta, p1Delta, stutterDelta, quality);
        public static LocalisableString DiagnosticsApplyRecommendedButton => new TranslatableString(getKey(@"diagnostics_apply_recommended_button"), @"Apply recommended profile");
        public static LocalisableString DiagnosticsProfileCurrent => new TranslatableString(getKey(@"diagnostics_profile_current"), @"Current settings");
        public static LocalisableString DiagnosticsProfileRecommended => new TranslatableString(getKey(@"diagnostics_profile_recommended"), @"Complete Mosu recommended profile");
        public static LocalisableString DiagnosticsProfileBaseline => new TranslatableString(getKey(@"diagnostics_profile_baseline"), @"Clean baseline");
        public static LocalisableString DiagnosticsProfileClassicBaseline => new TranslatableString(getKey(@"diagnostics_profile_classic_baseline"), @"Classic skin baseline");
        public static LocalisableString DiagnosticsProfileSkinPackage => new TranslatableString(getKey(@"diagnostics_profile_skin_package"), @"Complete skin performance package");
        public static LocalisableString DiagnosticsProfileVerificationBaseline => new TranslatableString(getKey(@"diagnostics_profile_verification_baseline"), @"Final clean baseline");
        public static LocalisableString DiagnosticsProfileFailed(LocalisableString profile) =>
            new TranslatableString(getKey(@"diagnostics_profile_failed"), @"{0}: test failed", profile);
        public static LocalisableString DiagnosticsSkippedProfilesTitle(LocalisableString count) =>
            new TranslatableString(getKey(@"diagnostics_skipped_profiles_title"), @"Not applicable on this system ({0})", count);
        public static LocalisableString DiagnosticsSkippedProfile(LocalisableString profile, LocalisableString renderer, LocalisableString reason) =>
            new TranslatableString(getKey(@"diagnostics_skipped_profile"), @"{0} on {1}: {2}", profile, renderer, reason);
        public static LocalisableString DiagnosticsSkipRequiresWindows => new TranslatableString(getKey(@"diagnostics_skip_requires_windows"), @"requires Windows");
        public static LocalisableString DiagnosticsSkipRequiresDeferredRenderer => new TranslatableString(getKey(@"diagnostics_skip_requires_deferred_renderer"), @"only affects the Deferred renderer");
        public static LocalisableString DiagnosticsSkipRequiresNonDeferredRenderer => new TranslatableString(getKey(@"diagnostics_skip_requires_non_deferred_renderer"), @"only affects the non-Deferred Veldrid renderer");
        public static LocalisableString DiagnosticsInputPollingScope => new TranslatableString(getKey(@"diagnostics_input_polling_scope"), @"Scope: measures the CPU and input-thread cost of 8000 Hz scheduling during replay playback; it does not measure mouse sensor or USB latency.");
        public static LocalisableString DiagnosticsTearingScope => new TranslatableString(getKey(@"diagnostics_tearing_scope"), @"Scope: measures this presentation option in the current Windows display mode; its effect can depend on window mode, driver and VRR support.");
        public static LocalisableString DiagnosticsProfileResult(LocalisableString profile, LocalisableString avgFps, LocalisableString p1Fps, LocalisableString stutters, LocalisableString quality) =>
            new TranslatableString(getKey(@"diagnostics_profile_result"), @"{0}: average {1} FPS · slowest 1% {2} FPS · stutters {3} · {4}", profile, avgFps, p1Fps, stutters, quality);
        public static LocalisableString DiagnosticsQualityReliable => new TranslatableString(getKey(@"diagnostics_quality_reliable"), @"reliable");
        public static LocalisableString DiagnosticsQualitySingleRun => new TranslatableString(getKey(@"diagnostics_quality_single_run"), @"one complete measurement");
        public static LocalisableString DiagnosticsQualityVariable => new TranslatableString(getKey(@"diagnostics_quality_variable"), @"variable");
        public static LocalisableString DiagnosticsQualityUnstable => new TranslatableString(getKey(@"diagnostics_quality_unstable"), @"high run-to-run variation");
        public static LocalisableString DiagnosticsQualityFailed => new TranslatableString(getKey(@"diagnostics_quality_failed"), @"failed");
        public static LocalisableString DiagnosticsQualityDetails(LocalisableString quality, LocalisableString avgSpread, LocalisableString p1Spread) =>
            new TranslatableString(getKey(@"diagnostics_quality_details"), @"{0}; measured-run spread: average {1}, slowest 1% {2}", quality, avgSpread, p1Spread);
        public static LocalisableString DiagnosticsDeepBaselineMissing => new TranslatableString(getKey(@"diagnostics_deep_baseline_missing"), @"The clean baseline did not produce usable metrics. Repeat the deep test.");
        public static LocalisableString DiagnosticsDeepClassicBaselineMissing => new TranslatableString(getKey(@"diagnostics_deep_classic_baseline_missing"), @"The Classic skin baseline did not produce usable metrics. Legacy-only setting results are unavailable.");
        public static LocalisableString DiagnosticsDeepBaselineTitle => new TranslatableString(getKey(@"diagnostics_deep_baseline_title"), @"Clean baseline");
        public static LocalisableString DiagnosticsDeepClassicBaselineTitle => new TranslatableString(getKey(@"diagnostics_deep_classic_baseline_title"), @"Classic skin baseline");
        public static LocalisableString DiagnosticsDeepVerificationBaselineTitle => new TranslatableString(getKey(@"diagnostics_deep_verification_baseline_title"), @"Performance drift check");
        public static LocalisableString DiagnosticsDeepVerificationBaselineResult(LocalisableString avgFps, LocalisableString drift, LocalisableString quality) =>
            new TranslatableString(getKey(@"diagnostics_deep_verification_baseline_result"), @"Final clean baseline: average {0} FPS · drift from the initial baseline {1} · {2}. Results between both baselines are adjusted gradually for this drift.", avgFps, drift, quality);
        public static LocalisableString DiagnosticsDeepBaselineVariationTitle => new TranslatableString(getKey(@"diagnostics_deep_baseline_variation_title"), @"Baseline measurement variation");
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
        public static LocalisableString DiagnosticsDeepBaselineResult(LocalisableString avgFps, LocalisableString p1Fps, LocalisableString quality) =>
            new TranslatableString(getKey(@"diagnostics_deep_baseline_result"), @"All tested optimisations disabled: average {0} FPS · slowest 1% {1} FPS · {2}", avgFps, p1Fps, quality);
        public static LocalisableString DiagnosticsDeepResultsHint => new TranslatableString(getKey(@"diagnostics_deep_results_hint"), @"Every result is compared with the time-adjusted clean all-off baseline. The complete recommended profile is shown first as the aggregate result. Other independent optimisations are tested alone; skin performance is one package containing its master switch and every skin optimisation. A plus sign means an improvement.");
        public static LocalisableString DiagnosticsDeepRecommendedComparisonTitle => new TranslatableString(getKey(@"diagnostics_deep_recommended_comparison_title"), @"Complete recommended profile vs all optimisations disabled");
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
        public static LocalisableString DiagnosticsRendererWinnerTitle => new TranslatableString(getKey(@"diagnostics_renderer_winner_title"), @"Best renderer for the recommended profile");
        public static LocalisableString DiagnosticsRendererWinnerResult(
            LocalisableString renderer,
            LocalisableString avgFps,
            LocalisableString p1Fps,
            LocalisableString stutters,
            LocalisableString quality) =>
            new TranslatableString(getKey(@"diagnostics_renderer_winner_result"), @"{0}: average {1} FPS · slowest 1% {2} FPS · stutters {3} · {4}", renderer, avgFps, p1Fps, stutters, quality);
        public static LocalisableString DiagnosticsStutterSummary(LocalisableString thread, LocalisableString frameMs) =>
            new TranslatableString(getKey(@"diagnostics_stutter_summary"), @"Worst event: {0} thread, {1} ms. The complete event list is available in the ZIP export.", thread, frameMs);
        public static LocalisableString DiagnosticsImpactNeutral => new TranslatableString(getKey(@"diagnostics_impact_neutral"), @"NO CLEAR CHANGE");
        public static LocalisableString VisualOD11Caption => new TranslatableString(getKey(@"visual_od11_caption"), @"Visual OD11");
        public static LocalisableString VisualOD11Hint => new TranslatableString(getKey(@"visual_od11_hint"), @"The error meter and hit judgements will be displayed visually as if playing OD11.");
        public static LocalisableString HitErrorMeterPositionalMissesCaption => new TranslatableString(getKey(@"hit_error_meter_positional_misses_caption"), @"Show failed taps on hit error meter");
        public static LocalisableString HitErrorMeterPositionalMissesHint => new TranslatableString(getKey(@"hit_error_meter_positional_misses_hint"), @"Displays taps made outside a hit object as red markers on the hit error meter, similar to danser.");
        public static LocalisableString ShowModsInPresetListCaption => new TranslatableString(getKey(@"show_mods_in_preset_list_caption"), @"Show mod icons under preset names");
        public static LocalisableString ShowModsInPresetListHint => new TranslatableString(getKey(@"show_mods_in_preset_list_hint"), @"Replaces each preset description in the mod selection list with icons for the mods it contains.");
        public static LocalisableString UseStableDirectoryCaption => new TranslatableString(getKey(@"use_stable_directory_caption"), @"Use osu!stable directory directly (Experimental)");
        public static LocalisableString UseStableDirectoryHint => new TranslatableString(getKey(@"use_stable_directory_hint"), @"Reads beatmaps directly from osu!stable without copying files. Changes are applied without restarting the game.");
        public static LocalisableString CustomStableDirectoryCaption => new TranslatableString(getKey(@"custom_stable_directory_caption"), @"Custom osu!stable directory");
        public static LocalisableString CustomStableDirectoryHint => new TranslatableString(getKey(@"custom_stable_directory_hint"), @"Leave empty to auto-detect. The path is applied after pressing Enter or leaving the field.");
        public static LocalisableString CustomStableDirectoryPlaceholder => new TranslatableString(getKey(@"custom_stable_directory_placeholder"), @"C:\Users\Username\AppData\Local\osu!");
        public static LocalisableString CustomUsernameCaption => new TranslatableString(getKey(@"custom_username_caption"), @"Custom username");
        public static LocalisableString CustomUsernameHint => new TranslatableString(getKey(@"custom_username_hint"), @"Displayed in the menu and in locally saved scores. Leave empty to use account username.");
        public static LocalisableString CustomUsernamePlaceholder => new TranslatableString(getKey(@"custom_username_placeholder"), @"Empty = account username");
        public static LocalisableString OverrideRecDiffCaption => new TranslatableString(getKey(@"override_rec_diff_caption"), @"Override recommended difficulty");
        public static LocalisableString OverrideRecDiffHint => new TranslatableString(getKey(@"override_rec_diff_hint"), @"If enabled, the client uses this exact star rating directly, disregarding PP and other parameters.");
        public static LocalisableString CustomRecDiffCaption => new TranslatableString(getKey(@"custom_rec_diff_caption"), @"Custom recommended difficulty");
        public static LocalisableString OldCarouselPreviewCaption => new TranslatableString(getKey(@"old_carousel_preview_caption"), @"Legacy previews in beatmap carousel");
        public static LocalisableString OldCarouselPreviewHint => new TranslatableString(getKey(@"old_carousel_preview_hint"), @"Switches the cards in song select to an older layout: the preview is fixed to a 16:9 block on the left edge, and extra background effects are removed.");
        public static LocalisableString SkinnedLegacyCarouselCaption => new TranslatableString(getKey(@"skinned_legacy_carousel_caption"), @"Skin-based legacy beatmap carousel");
        public static LocalisableString SkinnedLegacyCarouselHint => new TranslatableString(getKey(@"skinned_legacy_carousel_hint"), @"Uses the current skin's menu-button-background for beatmap cards and enables the classic preview layout.");
        public static LocalisableString CarouselBgDim => new TranslatableString(getKey(@"carousel_bg_dim"), @"Carousel background dim");
        public static LocalisableString StoryboardBgCaption => new TranslatableString(getKey(@"storyboard_bg_caption"), @"Storyboard/video background in song select");
        public static LocalisableString StoryboardBgHint => new TranslatableString(getKey(@"storyboard_bg_hint"), @"Shows the storyboard or video background of the selected beatmap on the song select screen. Only works if the beatmap has a storyboard or video.");
        public static LocalisableString ReplayQualityPresetCaption => new TranslatableString(getKey(@"replay_quality_preset_caption"), @"Render quality preset");
        public static LocalisableString ReplayQualityPresetHint => new TranslatableString(getKey(@"replay_quality_preset_hint"), @"Default profile for replay render: affects target bitrate and ffmpeg preset. Fast = faster rendering, Quality = higher quality but slower.");
        public static LocalisableString ReplayTraceModeCaption => new TranslatableString(getKey(@"replay_trace_mode_caption"), @"Render debug trace mode");
        public static LocalisableString ReplayTraceModeHint => new TranslatableString(getKey(@"replay_trace_mode_hint"), @"Disabled does not create a debug file. Summary writes a short session summary and final stats. Full enables frame-by-frame tracing.");
        public static LocalisableString ExportClicksOnlyCaption => new TranslatableString(getKey(@"export_clicks_only_caption"), @"Export replay with clicks only");
        public static LocalisableString ExportClicksOnlyHint => new TranslatableString(getKey(@"export_clicks_only_hint"), @"When exporting to .osr, cursor movements without clicks are not saved (empty frames are removed).");
        public static LocalisableString ExportAllBeatmapsBtn => new TranslatableString(getKey(@"export_all_beatmaps_btn"), @"Export all beatmaps");
        public static LocalisableString ExportAllBeatmapsConfirm(LocalisableString count) => new TranslatableString(getKey(@"export_all_beatmaps_confirm"), @"This will export {0} beatmap sets.", count);
        public static LocalisableString ExportCollectionBtn => new TranslatableString(getKey(@"export_collection_btn"), @"Export beatmaps from collection");
        public static LocalisableString ExportCollectionEmpty => new TranslatableString(getKey(@"export_collection_empty"), @"You do not have any collections created.");
        public static LocalisableString ExportCollectionMatchEmpty => new TranslatableString(getKey(@"export_collection_match_empty"), @"There are no beatmaps to export in the selected collection.");
        public static LocalisableString CustomUIFontCaption => new TranslatableString(getKey(@"custom_ui_font_caption"), @"Custom UI font");
        public static LocalisableString CustomUIFontHint => new TranslatableString(getKey(@"custom_ui_font_hint"), @"Select a BMFont (.fnt + .png) to use for the user interface. Place font files in the Fonts folder.");
        public static LocalisableString RussianFontFixCaption => new TranslatableString(getKey(@"russian_font_fix_caption"), @"Russian font fix");
        public static LocalisableString RussianFontFixHint => new TranslatableString(getKey(@"russian_font_fix_hint"), @"Uses the bundled Noto font for Cyrillic characters instead of the selected custom UI font.");
        public static LocalisableString FontsFolderBtn => new TranslatableString(getKey(@"fonts_folder_btn"), @"Open fonts folder");
        public static LocalisableString ThemeModeCaption => new TranslatableString(getKey(@"theme_mode_caption"), @"Interface theme");
        public static LocalisableString ThemeModeHint => new TranslatableString(getKey(@"theme_mode_hint"), @"Changes the global color scheme of the game immediately.");
        public static LocalisableString PresetNone => new TranslatableString(getKey(@"preset_none"), @"Select a preset...");
        public static LocalisableString PresetNoneSummary => new TranslatableString(getKey(@"preset_none_summary"), @"Select one of the profiles to instantly apply a set of aim and relax settings.");
        public static LocalisableString PresetSoft => new TranslatableString(getKey(@"preset_soft"), @"Soft");
        public static LocalisableString PresetSoftSummary => new TranslatableString(getKey(@"preset_soft_summary"), @"Gives more freedom to the hand, weaker aim lock, soft humanized relax.");
        public static LocalisableString PresetBalanced => new TranslatableString(getKey(@"preset_balanced"), @"Balanced");
        public static LocalisableString PresetBalancedSummary => new TranslatableString(getKey(@"preset_balanced_summary"), @"Universal profile: moderate assist and smooth relax.");
        public static LocalisableString PresetSticky => new TranslatableString(getKey(@"preset_sticky"), @"Sticky");
        public static LocalisableString PresetStickySummary => new TranslatableString(getKey(@"preset_sticky_summary"), @"Holds the target strongly, triggers earlier, confidently hits jumps.");
        public static LocalisableString PresetNoteApplied(LocalisableString name, LocalisableString summary) => new TranslatableString(getKey(@"preset_note_applied"), @"Preset '{0}' applied. {1}", name, summary);
        public static LocalisableString PresetHeader => new TranslatableString(getKey(@"preset_header"), @"Ready-made Presets");
        public static LocalisableString PresetDropdownCaption => new TranslatableString(getKey(@"preset_dropdown_caption"), @"Relax + Aim Assist Preset");
        public static LocalisableString PresetDropdownHint => new TranslatableString(getKey(@"preset_dropdown_hint"), @"Selecting a preset instantly applies a predefined set of settings for relax and aim assist.");
        public static LocalisableString CommunityHeader => new TranslatableString(getKey(@"community_header"), @"Community");
        public static LocalisableString CommunityTgChannel => new TranslatableString(getKey(@"community_tg_channel"), @"mosu Telegram Channel");
        public static LocalisableString CommunityDiscordServer => new TranslatableString(getKey(@"community_discord_server"), @"Discord Server");
        public static LocalisableString RelaxHeader => new TranslatableString(getKey(@"relax_header"), @"Relax");
        public static LocalisableString RelaxEnableCaption => new TranslatableString(getKey(@"relax_enable_caption"), @"Enable relax");
        public static LocalisableString RelaxEnableHint => new TranslatableString(getKey(@"relax_enable_hint"), @"Enables automatic clicking. If disabled, the player clicks.");
        public static LocalisableString RelaxBaseOffsetCaption => new TranslatableString(getKey(@"relax_base_offset_caption"), @"Base offset");
        public static LocalisableString RelaxBaseOffsetHint => new TranslatableString(getKey(@"relax_base_offset_hint"), @"Timing shift from the ideal hit. Positive — later, negative — earlier.");
        public static LocalisableString RelaxVarianceCaption => new TranslatableString(getKey(@"relax_variance_caption"), @"Timing variance");
        public static LocalisableString RelaxVarianceHint => new TranslatableString(getKey(@"relax_variance_hint"), @"Random timing scatter. Higher value looks more human.");
        public static LocalisableString RelaxHoldTimeCaption => new TranslatableString(getKey(@"relax_hold_time_caption"), @"Hold time");
        public static LocalisableString RelaxHoldTimeHint => new TranslatableString(getKey(@"relax_hold_time_hint"), @"Average key hold time. Affects the duration of presses.");
        public static LocalisableString RelaxSyncRadiusCaption => new TranslatableString(getKey(@"relax_sync_radius_caption"), @"Sync radius");
        public static LocalisableString RelaxSyncRadiusHint => new TranslatableString(getKey(@"relax_sync_radius_hint"), @"Area around the note where relax can wait for the cursor to arrive before clicking.");
        public static LocalisableString RelaxMaxSyncDelayCaption => new TranslatableString(getKey(@"relax_max_sync_delay_caption"), @"Max sync delay");
        public static LocalisableString RelaxMaxSyncDelayHint => new TranslatableString(getKey(@"relax_max_sync_delay_hint"), @"Maximum time relax will wait for cursor arrival before forcing a click anyway.");
        public static LocalisableString RelaxStableBpmCaption => new TranslatableString(getKey(@"relax_stable_bpm_caption"), @"Max stable BPM");
        public static LocalisableString RelaxStableBpmHint => new TranslatableString(getKey(@"relax_stable_bpm_hint"), @"Comfortable stream speed. Above this speed, relax starts losing stamina.");

        public static LocalisableString RelaxDynamicDriftCaption => new TranslatableString(getKey(@"relax_dynamic_drift_caption"), @"Dynamic drift");
        public static LocalisableString RelaxDynamicDriftHint => new TranslatableString(getKey(@"relax_dynamic_drift_hint"), @"Smoothly shifts the base timing throughout the map.");
        public static LocalisableString RelaxSliderTailOffsetCaption => new TranslatableString(getKey(@"relax_slider_tail_offset_caption"), @"Slider tail offset");
        public static LocalisableString RelaxSliderTailOffsetHint => new TranslatableString(getKey(@"relax_slider_tail_offset_hint"), @"Timing shift for releasing the slider tail.");
        public static LocalisableString RelaxAlternateThresholdCaption => new TranslatableString(getKey(@"relax_alternate_threshold_caption"), @"Alternate threshold");
        public static LocalisableString RelaxAlternateThresholdHint => new TranslatableString(getKey(@"relax_alternate_threshold_hint"), @"BPM above which relax begins alternating keys.");
        public static LocalisableString RelaxMisaltProbabilityCaption => new TranslatableString(getKey(@"relax_misalt_probability_caption"), @"Misalt probability");
        public static LocalisableString RelaxMisaltProbabilityHint => new TranslatableString(getKey(@"relax_misalt_probability_hint"), @"Chance of accidental misalt during streams.");
        public static LocalisableString RelaxStreamBlindModeCaption => new TranslatableString(getKey(@"relax_stream_blind_mode_caption"), @"Stream blind mode");
        public static LocalisableString RelaxStreamBlindModeHint => new TranslatableString(getKey(@"relax_stream_blind_mode_hint"), @"Enables blind mode specifically for streams.");
        public static LocalisableString RelaxBlindTapEnabledCaption => new TranslatableString(getKey(@"relax_blind_tap_enabled_caption"), @"Blind tap");
        public static LocalisableString RelaxBlindTapEnabledHint => new TranslatableString(getKey(@"relax_blind_tap_enabled_hint"), @"Tap blindly when the cursor is too far from the note.");

        public static LocalisableString DifficultyAdditionalInfoCaption => new TranslatableString(getKey(@"difficulty_additional_info_caption"), @"Additional difficulty info");
        public static LocalisableString DifficultyAdditionalInfoHint => new TranslatableString(getKey(@"difficulty_additional_info_hint"), @"Displays max combo, max PP, and detailed PP breakdown for each difficulty.");
        public static LocalisableString ForkDisableBeatmapStatusOverwriteCaption => new TranslatableString(getKey(@"fork_disable_beatmap_status_overwrite_caption"), @"Disable beatmap status overwrite");
        public static LocalisableString ForkDisableBeatmapStatusOverwriteHint => new TranslatableString(getKey(@"fork_disable_beatmap_status_overwrite_hint"), @"If enabled, the client will not overwrite map status (Ranked/Loved) when playing on other servers. Doesn't apply to Mosu.");
        public static LocalisableString ForkClassicModDefaultCaption => new TranslatableString(getKey(@"fork_classic_mod_default_caption"), @"Enable Classic mod by default");
        public static LocalisableString ForkHideVisualSliderMissesCaption => new TranslatableString(getKey(@"fork_hide_visual_slider_misses_caption"), @"Disable visual slider misses (crosses)");
        public static LocalisableString DisableInterfaceShearCaption => new TranslatableString(getKey(@"disable_interface_shear_caption"), @"Disable interface shear");
        public static LocalisableString DisableInterfaceShearHint => new TranslatableString(getKey(@"disable_interface_shear_hint"), @"Disables the slant effect on all interface elements.");

        public static LocalisableString SupporterQuestTitle => new TranslatableString(getKey(@"supporter_quest_title"), @"Supporter Quests");
        public static LocalisableString SupporterQuestDescription => new TranslatableString(getKey(@"supporter_quest_description"), @"Complete quests to earn a month of supporter!");
        public static LocalisableString SupporterQuestHeader => new TranslatableString(getKey(@"supporter_quest_header"), @"Until 1 month of supporter");
        public static LocalisableString SupporterQuestSkipBtn => new TranslatableString(getKey(@"supporter_quest_skip_btn"), @"Skip quests");
        public static LocalisableString SupporterQuestRemaining(int count) => new TranslatableString(getKey(@"supporter_quest_remaining"), @"+ {0} more locked", count);
        public static LocalisableString SupporterQuestCompletedNotification(LocalisableString name) => new TranslatableString(getKey(@"supporter_quest_completed_notification"), @"Quest completed: ""{0}""!", name);

        public static LocalisableString SupporterQuestFeaturesHeader => new TranslatableString(getKey(@"supporter_quest_features_header"), @"Supporter benefits:");
        public static LocalisableString SupporterQuestFeature1 => new TranslatableString(getKey(@"supporter_quest_feature_1"), @"• Ability to change your username");
        public static LocalisableString SupporterQuestFeature2 => new TranslatableString(getKey(@"supporter_quest_feature_2"), @"• Download up to 8 exclusive beatmaps/day");
        public static LocalisableString SupporterQuestFeature3 => new TranslatableString(getKey(@"supporter_quest_feature_3"), @"• Supporter badge next to your name");
        public static LocalisableString SupporterQuestFeature4 => new TranslatableString(getKey(@"supporter_quest_feature_4"), @"• Ability to create teams");
        public static LocalisableString SupporterQuestFeature5 => new TranslatableString(getKey(@"supporter_quest_feature_5"), @"• All official osu!supporter features");

        public static LocalisableString QuestName1 => new TranslatableString(getKey(@"quest_name_1"), @"Play 5 maps");
        public static LocalisableString QuestDesc1 => new TranslatableString(getKey(@"quest_desc_1"), @"Play any 5 maps (with ranked mods if using mods).");
        public static LocalisableString QuestName2 => new TranslatableString(getKey(@"quest_name_2"), @"Change profile background");
        public static LocalisableString QuestDesc2 => new TranslatableString(getKey(@"quest_desc_2"), @"Set a background image in your profile.");
        public static LocalisableString QuestName3 => new TranslatableString(getKey(@"quest_name_3"), @"Play in two playlists");
        public static LocalisableString QuestDesc3 => new TranslatableString(getKey(@"quest_desc_3"), @"Play in two different multiplayer playlists.");
        public static LocalisableString QuestName4 => new TranslatableString(getKey(@"quest_name_4"), @"Play 10 minutes of osu!relax");
        public static LocalisableString QuestDesc4 => new TranslatableString(getKey(@"quest_desc_4"), @"Play a total of 10 minutes in osu!relax mode.");
        public static LocalisableString QuestName5 => new TranslatableString(getKey(@"quest_name_5"), @"Play 5 maps in osu!mania with A rank");
        public static LocalisableString QuestDesc5 => new TranslatableString(getKey(@"quest_desc_5"), @"Pass 5 maps in osu!mania mode with an A rank or higher.");
        public static LocalisableString QuestName6 => new TranslatableString(getKey(@"quest_name_6"), @"Play two maps longer than 5 minutes");
        public static LocalisableString QuestDesc6 => new TranslatableString(getKey(@"quest_desc_6"), @"Play any 2 maps with a duration of more than 5 minutes.");
        public static LocalisableString QuestName7 => new TranslatableString(getKey(@"quest_name_7"), @"Pass 10 maps in any playlist");
        public static LocalisableString QuestDesc7 => new TranslatableString(getKey(@"quest_desc_7"), @"Pass 10 maps within multiplayer playlists.");
        public static LocalisableString QuestName8 => new TranslatableString(getKey(@"quest_name_8"), @"Spend 60 minutes in game");
        public static LocalisableString QuestDesc8 => new TranslatableString(getKey(@"quest_desc_8"), @"Spend 60 minutes playing on beatmaps.");
        public static LocalisableString QuestName9 => new TranslatableString(getKey(@"quest_name_9"), @"Add someone as a friend");
        public static LocalisableString QuestDesc9 => new TranslatableString(getKey(@"quest_desc_9"), @"Add any other player to your friend list.");
        public static LocalisableString QuestName10 => new TranslatableString(getKey(@"quest_name_10"), @"Play 5 server-exclusive maps");
        public static LocalisableString QuestDesc10 => new TranslatableString(getKey(@"quest_desc_10"), @"Play 5 maps marked with the server-exclusive tag.");
        public static LocalisableString QuestName11 => new TranslatableString(getKey(@"quest_name_11"), @"Pass 3 maps with 6 mods");
        public static LocalisableString QuestDesc11 => new TranslatableString(getKey(@"quest_desc_11"), @"Successfully pass 3 maps using at least 6 mods simultaneously.");
        public static LocalisableString QuestName12 => new TranslatableString(getKey(@"quest_name_12"), @"Pass 5 maps in osu!catch with A rank or higher");
        public static LocalisableString QuestDesc12 => new TranslatableString(getKey(@"quest_desc_12"), @"Pass 5 maps in osu!catch mode with an A rank or higher.");
        public static LocalisableString QuestName13 => new TranslatableString(getKey(@"quest_name_13"), @"Pass the Daily Challenge map");
        public static LocalisableString QuestDesc13 => new TranslatableString(getKey(@"quest_desc_13"), @"Pass today's map in the Daily Challenge.");
        public static LocalisableString QuestName14 => new TranslatableString(getKey(@"quest_name_14"), @"Pass 50 maps");
        public static LocalisableString QuestDesc14 => new TranslatableString(getKey(@"quest_desc_14"), @"Successfully pass any 50 maps with a B rank or higher.");
        public static LocalisableString QuestName15 => new TranslatableString(getKey(@"quest_name_15"), @"Pass 10 maps in osu!taiko with B rank or higher");
        public static LocalisableString QuestDesc15 => new TranslatableString(getKey(@"quest_desc_15"), @"Pass 10 maps in osu!taiko mode with a B rank or higher.");
        public static LocalisableString QuestName16 => new TranslatableString(getKey(@"quest_name_16"), @"Pass a 10-minute map");
        public static LocalisableString QuestDesc16 => new TranslatableString(getKey(@"quest_desc_16"), @"Successfully pass a map with a duration of 10 minutes or more with a B rank or higher.");
        public static LocalisableString QuestName17 => new TranslatableString(getKey(@"quest_name_17"), @"Pass 5 five-minute maps");
        public static LocalisableString QuestDesc17 => new TranslatableString(getKey(@"quest_desc_17"), @"Successfully pass 5 maps with a duration of 5 minutes or more with a B rank or higher.");
        public static LocalisableString QuestName18 => new TranslatableString(getKey(@"quest_name_18"), @"Try all client mods of Mosu");
        public static LocalisableString QuestDesc18 => new TranslatableString(getKey(@"quest_desc_18"), @"Play once with each Mosu client mod (AA, AA!, MSQ, MRX, SB, TS).");
        public static LocalisableString QuestName19 => new TranslatableString(getKey(@"quest_name_19"), @"Pass 50 maps with A rank");
        public static LocalisableString QuestDesc19 => new TranslatableString(getKey(@"quest_desc_19"), @"Pass 50 maps of any type with an A rank or higher.");
        public static LocalisableString QuestName20 => new TranslatableString(getKey(@"quest_name_20"), @"Spend 50 hours in game");
        public static LocalisableString QuestDesc20 => new TranslatableString(getKey(@"quest_desc_20"), @"Accumulate 50 hours of total play time on maps.");
        public static LocalisableString QuestName21 => new TranslatableString(getKey(@"quest_name_21"), @"Pass 5 maps with Anti Aim Assist");
        public static LocalisableString QuestDesc21 => new TranslatableString(getKey(@"quest_desc_21"), @"Pass 5 maps with the Anti Aim Assist (AA!) mod enabled with a B rank or higher.");
        public static LocalisableString QuestName22 => new TranslatableString(getKey(@"quest_name_22"), @"Add a map to favorites");
        public static LocalisableString QuestDesc22 => new TranslatableString(getKey(@"quest_desc_22"), @"Add any map to your favorite list.");
        public static LocalisableString QuestName23 => new TranslatableString(getKey(@"quest_name_23"), @"Pass 10 maps in each mode");
        public static LocalisableString QuestDesc23 => new TranslatableString(getKey(@"quest_desc_23"), @"Pass at least 10 maps in each of the 4 main modes (osu!, taiko, catch, mania) with a B rank or higher. Total 40 maps.");
        public static LocalisableString QuestName24 => new TranslatableString(getKey(@"quest_name_24"), @"Spend 100 hours in game");
        public static LocalisableString QuestDesc24 => new TranslatableString(getKey(@"quest_desc_24"), @"Accumulate 100 hours of total play time on maps.");
        public static LocalisableString QuestName25 => new TranslatableString(getKey(@"quest_name_25"), @"Pass 10 ten-minute maps");
        public static LocalisableString QuestDesc25 => new TranslatableString(getKey(@"quest_desc_25"), @"Successfully pass 10 maps with a duration of 10 minutes or more with a B rank or higher.");

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

        private static string getKey(string key) => $@"{prefix}:{key}";

    }
}
