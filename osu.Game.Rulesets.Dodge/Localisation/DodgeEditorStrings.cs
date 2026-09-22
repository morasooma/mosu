// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Rulesets.Dodge.Localisation
{
    public static class DodgeEditorStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.DodgeEditor";

        public static LocalisableString Arena => get(@"arena", @"Arena");
        public static LocalisableString ArenaRotation => get(@"arena_rotation", @"Вращение арены");
        public static LocalisableString ArenaEasing => get(@"arena_easing", @"Movement curve");
        public static LocalisableString ArenaEasingHint => get(@"arena_easing_hint", @"Changes the acceleration of an arena transition, including its movement, size, rotation and appearance.");
        public static LocalisableString ArenaKiaiShakeAngle => get(@"arena_kiai_shake_angle", @"Kiai shake (°)");
        public static LocalisableString Bullet => get(@"bullet", @"Bullet");
        public static LocalisableString Emitter => get(@"emitter", @"Emitter");
        public static LocalisableString Beam => get(@"beam", @"Beam");
        public static LocalisableString BeamToolTip => get(@"beam_tooltip", @"Place a full-arena beam danger zone.");
        public static LocalisableString BeamWidth => get(@"beam_width", @"Ширина луча");
        public static LocalisableString Camera => get(@"camera", @"Camera");
        public static LocalisableString Trigger => get(@"trigger", @"Triggers");
        public static LocalisableString TriggerAction => get(@"trigger_action", @"Action");
        public static LocalisableString TriggerStrength => get(@"trigger_strength", @"Strength");
        public static LocalisableString TriggerDuration => get(@"trigger_duration", @"Effect duration (ms)");
        public static LocalisableString TriggerColour => get(@"trigger_colour", @"Effect colour");
        public static LocalisableString TriggerToolTip => get(@"trigger_tooltip", @"Place a time-based trigger that fires without player contact.");
        public static LocalisableString TriggerClearBullets => get(@"trigger_clear_bullets", @"Clear bullets");
        public static LocalisableString TriggerToggleHud => get(@"trigger_toggle_hud", @"Toggle HUD");
        public static LocalisableString TriggerHideHud => get(@"trigger_hide_hud", @"Hide HUD");
        public static LocalisableString TriggerShowHud => get(@"trigger_show_hud", @"Show HUD");
        public static LocalisableString TriggerScreenShake => get(@"trigger_screen_shake", @"Screen shake");
        public static LocalisableString TriggerFlashEffect => get(@"trigger_flash_effect", @"Flash effect");
        public static LocalisableString TriggerTrailEnable => get(@"trigger_trail_enable", @"Enable player trail");
        public static LocalisableString TriggerTrailDisable => get(@"trigger_trail_disable", @"Disable player trail");
        public static LocalisableString CameraContinuousScroll => get(@"camera_continuous_scroll", @"Continuous scroll");
        public static LocalisableString CameraContinuousScrollHint => get(@"camera_continuous_scroll_hint", @"When enabled, the camera follows the beatmap scroll position instead of being centred on the arena.");
        public static LocalisableString CameraEasing => get(@"camera_easing", @"Movement curve");
        public static LocalisableString CameraEasingHint => get(@"camera_easing_hint", @"Changes the acceleration of a timed camera move. Continuous scroll always uses a constant velocity.");
        public static LocalisableString CameraEasingLinear => get(@"camera_easing_linear", @"Linear");
        public static LocalisableString CameraEasingIn => get(@"camera_easing_in", @"Ease in");
        public static LocalisableString CameraEasingOut => get(@"camera_easing_out", @"Ease out");
        public static LocalisableString CameraEasingInOut => get(@"camera_easing_in_out", @"Ease in/out");
        public static LocalisableString CameraEasingSmooth => get(@"camera_easing_smooth", @"Smooth");
        public static LocalisableString CameraEasingOvershoot => get(@"camera_easing_overshoot", @"Overshoot");
        public static LocalisableString CameraEasingBounce => get(@"camera_easing_bounce", @"Bounce");
        public static LocalisableString CameraEasingElastic => get(@"camera_easing_elastic", @"Elastic");
        public static LocalisableString Grid => get(@"grid", @"Grid");
        public static LocalisableString Patterns => get(@"patterns", @"Patterns");
        public static LocalisableString Transform => get(@"transform", @"Transform");
        public static LocalisableString View => get(@"view", @"View");
        public static LocalisableString Mapping => get(@"mapping", @"Mapping");
        public static LocalisableString Timing => get(@"timing", @"Timing");
        public static LocalisableString StoryboardDesign => get(@"storyboard_design", @"Оформление Dodge");
        public static LocalisableString StoryboardCanvasHint => get(@"storyboard_canvas_hint", @"Сториборд занимает весь холст osu!. Голубая внутренняя рамка — игровая арена Dodge 512 × 384.");
        public static LocalisableString StoryboardEditMode => get(@"storyboard_edit_mode", @"Режим редактирования");
        public static LocalisableString StoryboardObjectMode => get(@"storyboard_object_mode", @"Объект");
        public static LocalisableString StoryboardAnimationMode => get(@"storyboard_animation_mode", @"Анимация");
        public static LocalisableString StoryboardObjectModeHint => get(@"storyboard_object_mode_hint", @"Изменения применяются ко всему объекту и ко всей траектории. Новые ключи не создаются.");
        public static LocalisableString StoryboardAnimationModeHint => get(@"storyboard_animation_mode_hint", @"Изменения записываются в текущий момент как ключи анимации.");
        public static LocalisableString ForceStoryboard => get(@"force_storyboard", @"Принудительно включать сториборд");
        public static LocalisableString ForceMapSkin => get(@"force_map_skin", @"Принудительно применять скин карты");
        public static LocalisableString ForcedVisualsNotice => get(@"forced_visuals_notice", @"This map forces its storyboard and/or map skin.");
        public static LocalisableString StoryboardTab => get(@"storyboard_tab", @"Сториборд");
        public static LocalisableString MapSkin => get(@"map_skin", @"Скин карты");
        public static LocalisableString ImportStoryboardImage => get(@"import_storyboard_image", @"Импорт изображения сториборда");
        public static LocalisableString ChooseOrDropImage => get(@"choose_or_drop_image", @"Выберите или перетащите изображение");
        public static LocalisableString ReplaceMapSkinComponent => get(@"replace_map_skin_component", @"Replace map-skin component");
        public static LocalisableString MapSkinHint => get(@"map_skin_hint", @"These resources belong to this beatmap. Gameplay geometry and collision sizes are unchanged.");
        public static LocalisableString BuiltInFallback => get(@"built_in_fallback", @"built-in fallback");
        public static LocalisableString StoryboardResources => get(@"storyboard_resources", @"Resources");
        public static LocalisableString StoryboardCreateObject => get(@"storyboard_create_object", @"Новый объект");
        public static LocalisableString StoryboardCreateStepResource => get(@"storyboard_create_step_resource", @"1. Выберите изображение");
        public static LocalisableString StoryboardCreateStepLayer => get(@"storyboard_create_step_layer", @"2. Выберите слой");
        public static LocalisableString StoryboardCreateStepTiming => get(@"storyboard_create_step_timing", @"3. Задайте время на экране");
        public static LocalisableString StoryboardVisibleDuration => get(@"storyboard_visible_duration", @"Длительность (мс)");
        public static LocalisableString StoryboardNoResource => get(@"storyboard_no_resource", @"Сначала импортируйте или выберите изображение.");
        public static LocalisableString StoryboardInsertionSummary(string layer, string start, string end)
            => get(@"storyboard_insertion_summary", @"Будет добавлено в {0}: {1}–{2}, по центру (320, 240).", layer, start, end);
        public static LocalisableString StoryboardInsertionHint => get(@"storyboard_insertion_hint", @"После добавления перетащите само изображение, а за углы измените размер. Время меняется на нижней шкале.");
        public static LocalisableString AddSprite => get(@"add_sprite", @"Добавить спрайт");
        public static LocalisableString AddAnimation => get(@"add_animation", @"Добавить анимацию");
        public static LocalisableString LayersAndObjects => get(@"layers_and_objects", @"Слои и объекты");
        public static LocalisableString EmptyStoryboardLayer => get(@"empty_storyboard_layer", @"Нет объектов");
        public static LocalisableString SelectedObject => get(@"selected_object", @"Выбранный объект");
        public static LocalisableString NoStoryboardSelection => get(@"no_storyboard_selection", @"Выберите изображение в списке слоёв, на холсте или на нижней шкале.");
        public static LocalisableString StoryboardObjectLayer(string layer)
            => get(@"storyboard_object_layer", @"Слой: {0}", layer);
        public static LocalisableString StoryboardObjectVisibleRange(string start, string end, string duration)
            => get(@"storyboard_object_visible_range", @"На экране: {0}–{1} ({2})", start, end, duration);
        public static LocalisableString StoryboardObjectVisibleNow => get(@"storyboard_object_visible_now", @"Объект виден в текущий момент");
        public static LocalisableString StoryboardObjectHiddenNow => get(@"storyboard_object_hidden_now", @"Сейчас объект не виден. Нажмите «К началу объекта», чтобы увидеть его.");
        public static LocalisableString StoryboardGoToStart => get(@"storyboard_go_to_start", @"К началу объекта");
        public static LocalisableString StoryboardStartAtPlayhead => get(@"storyboard_start_at_playhead", @"Начинать с текущего времени");
        public static LocalisableString StoryboardEndAtPlayhead => get(@"storyboard_end_at_playhead", @"Заканчивать в текущее время");
        public static LocalisableString StoryboardRangeLocked => get(@"storyboard_range_locked", @"Время заблокировано: объект содержит импортированные циклы или триггеры.");
        public static LocalisableString ShowAll => get(@"show_all", @"Показать всё");
        public static LocalisableString ShowArena => get(@"show_arena", @"Показать арену");
        public static LocalisableString GameplayPreview => get(@"gameplay_preview", @"Игровой просмотр");
        public static LocalisableString StoryboardTimeline => get(@"storyboard_timeline", @"Ключи выбранного объекта");
        public static LocalisableString StoryboardTimelineEmpty => get(@"storyboard_timeline_empty", @"Выберите объект: здесь появятся его время на экране и анимация.");
        public static LocalisableString StoryboardTimelineHelp => get(@"storyboard_timeline_help", @"Щёлкните по шкале, чтобы перейти по времени. Фиолетовую полосу можно двигать целиком или тянуть за края.");
        public static LocalisableString StoryboardTimelineObjectRange => get(@"storyboard_timeline_object_range", @"ВРЕМЯ ОБЪЕКТА");
        public static LocalisableString StoryboardTimelineViewPlayhead => get(@"storyboard_timeline_view_playhead", @"Показать текущее время");
        public static LocalisableString StoryboardTimelineViewObject => get(@"storyboard_timeline_view_object", @"Показать объект");
        public static LocalisableString StoryboardTimelineCollapse => get(@"storyboard_timeline_collapse", @"Свернуть");
        public static LocalisableString StoryboardTimelineExpand => get(@"storyboard_timeline_expand", @"Развернуть");
        public static LocalisableString StoryboardTimelineSnap => get(@"storyboard_timeline_snap", @"Привязка");
        public static LocalisableString StoryboardTimelineRangeHelp => get(@"storyboard_timeline_range_help", @"Середина полосы сдвигает объект; края растягивают его вместе с анимацией. Alt отключает привязку.");
        public static LocalisableString StoryboardCanvasLegend => get(@"storyboard_canvas_legend", @"Щёлкните изображение, чтобы выбрать • тяните его для перемещения • тяните за углы для размера • Alt отключает сетку");
        public static LocalisableString StoryboardCanvasCoordinates => get(@"storyboard_canvas_coordinates", @"Холст сториборда: 640 × 480");
        public static LocalisableString StoryboardArenaCoordinates => get(@"storyboard_arena_coordinates", @"Арена Dodge: 512 × 384 (поля 64 / 48)");

        public static LocalisableString StoryboardAtPlayhead(string time)
            => get(@"storyboard_at_playhead", @"В текущий момент — {0}", time);
        public static LocalisableString StoryboardDirectManipulationHint => get(@"storyboard_direct_manipulation_hint", @"Тяните изображение, чтобы переместить его. Тяните жёлтые углы, чтобы изменить размер; Shift сохраняет пропорции.");
        public static LocalisableString StoryboardResourceReady(float sourceWidth, float sourceHeight, float renderedWidth, float renderedHeight)
            => get(@"storyboard_resource_ready", @"Изображение загружено: {0:0} × {1:0} px. На холсте: {2:0.#} × {3:0.#}.", sourceWidth, sourceHeight, renderedWidth, renderedHeight);
        public static LocalisableString StoryboardResourceLoading(string path)
            => get(@"storyboard_resource_loading", @"Загружается изображение: {0}", path);
        public static LocalisableString StoryboardResourceMissing(string path)
            => get(@"storyboard_resource_missing", @"Изображение не найдено или не читается: {0}", path);
        public static LocalisableString StoryboardPositionX => get(@"storyboard_position_x", @"Позиция X");
        public static LocalisableString StoryboardPositionY => get(@"storyboard_position_y", @"Позиция Y");
        public static LocalisableString StoryboardScaleX => get(@"storyboard_scale_x", @"Размер по X");
        public static LocalisableString StoryboardScaleY => get(@"storyboard_scale_y", @"Размер по Y");
        public static LocalisableString StoryboardRotation => get(@"storyboard_rotation", @"Поворот (°)");
        public static LocalisableString StoryboardOpacity => get(@"storyboard_opacity", @"Прозрачность (%)");
        public static LocalisableString StoryboardOrigin => get(@"storyboard_origin", @"Точка привязки");
        public static LocalisableString StoryboardAnimatedPositionHint => get(@"storyboard_animated_position_hint", @"У объекта уже есть движение. Перетаскивание или ввод координат запишет положение именно в текущий момент.");
        public static LocalisableString StoryboardVisualEditingLocked => get(@"storyboard_visual_editing_locked", @"Ручное редактирование заблокировано для импортированного объекта с циклами или триггерами.");
        public static LocalisableString StoryboardTimingSection => get(@"storyboard_timing_section", @"Время на экране");
        public static LocalisableString StoryboardStartTime => get(@"storyboard_start_time", @"Начало (мс)");
        public static LocalisableString StoryboardEndTime => get(@"storyboard_end_time", @"Конец (мс)");
        public static LocalisableString StoryboardBrokenDurationHint => get(@"storyboard_broken_duration_hint", @"У старого объекта нет нормальной длительности, поэтому его почти невозможно увидеть.");
        public static LocalisableString StoryboardRepairDuration => get(@"storyboard_repair_duration", @"Исправить длительность до 2 секунд");
        public static LocalisableString StoryboardShowAnimation => get(@"storyboard_show_animation", @"Анимация и ключи  ▸");
        public static LocalisableString StoryboardHideAnimation => get(@"storyboard_hide_animation", @"Анимация и ключи  ▾");
        public static LocalisableString StoryboardAnimationHelp => get(@"storyboard_animation_help", @"Ключ запоминает значение в текущий момент. Перейдите в другое время, измените свойство или перетащите изображение — между значениями появится переход.");
        public static LocalisableString StoryboardEasing => get(@"storyboard_easing", @"Характер перехода для новых ключей");
        public static LocalisableString StoryboardEasingHelp => get(@"storyboard_easing_help", @"None — равномерно. Остальные варианты ускоряют или замедляют начало и конец перехода.");
        public static LocalisableString StoryboardPositionKey => get(@"storyboard_position_key", @"Позиция");
        public static LocalisableString StoryboardScaleKey => get(@"storyboard_scale_key", @"Размер");
        public static LocalisableString StoryboardRotationKey => get(@"storyboard_rotation_key", @"Поворот");
        public static LocalisableString StoryboardOpacityKey => get(@"storyboard_opacity_key", @"Прозрачность");
        public static LocalisableString StoryboardColourKey => get(@"storyboard_colour_key", @"Цвет");
        public static LocalisableString StoryboardAdditiveKey => get(@"storyboard_additive_key", @"Свечение");
        public static LocalisableString StoryboardFlipHKey => get(@"storyboard_flip_h_key", @"Отразить X");
        public static LocalisableString StoryboardFlipVKey => get(@"storyboard_flip_v_key", @"Отразить Y");
        public static LocalisableString StoryboardObjectActions => get(@"storyboard_object_actions", @"Действия с объектом");
        public static LocalisableString StoryboardDuplicate => get(@"storyboard_duplicate", @"Дублировать");
        public static LocalisableString StoryboardMoveUp => get(@"storyboard_move_up", @"Поднять в слое");
        public static LocalisableString StoryboardMoveDown => get(@"storyboard_move_down", @"Опустить в слое");
        public static LocalisableString StoryboardDelete => get(@"storyboard_delete", @"Удалить");

        public static LocalisableString ToolShortcuts => get(@"tool_shortcuts", @"1 Select  ·  2 Bullet  ·  3 Arena  ·  4 Emitter");
        public static LocalisableString SelectMode => get(@"select_mode", @"Select");
        public static LocalisableString BulletMode => get(@"bullet_mode", @"Bullet placement");
        public static LocalisableString EmitterMode => get(@"emitter_mode", @"Emitter placement");
        public static LocalisableString ArenaMode => get(@"arena_mode", @"Arena placement");
        public static LocalisableString ReadyToMap => get(@"ready_to_map", @"Choose a tool or select objects. The inspector only shows controls relevant to the current action.");
        public static LocalisableString BulletPlacementStart => get(@"bullet_placement_start", @"Click the bullet start position.");
        public static LocalisableString BulletPlacementDirection => get(@"bullet_placement_direction", @"Click the direction and distance. Adjust exact flight time on the timeline or in Timing.");
        public static LocalisableString EmitterPlacementSource => get(@"emitter_placement_source", @"Click the emitter source.");
        public static LocalisableString EmitterPlacementAim => get(@"emitter_placement_aim", @"Click the aim point. A moving emitter will then ask for the end of its source path.");
        public static LocalisableString EmitterPlacementMovementEnd => get(@"emitter_placement_movement_end", @"Click the end of the emitter source path.");
        public static LocalisableString ArenaPlacementStart => get(@"arena_placement_start", @"Click the first corner of the target arena.");
        public static LocalisableString ArenaPlacementEnd => get(@"arena_placement_end", @"Click the opposite corner. Timeline duration controls the transition.");
        public static LocalisableString SelectionCount(int count) => get(@"selection_count", @"Selected: {0}", count);
        public static LocalisableString SelectionHint(int bullets, int emitters, int arenas)
            => get(@"selection_hint", @"Bullets: {0}  ·  Emitters: {1}  ·  Arenas: {2}. Use Timing for duration and Transform for precise batch edits.", bullets, emitters, arenas);
        public static LocalisableString DurationSnapUnits => get(@"duration_snap_units", @"Duration (snap units)");
        public static LocalisableString DurationValue(int snapUnits, double milliseconds)
            => get(@"duration_value", @"{0} snaps · {1:N0} ms", snapUnits, milliseconds);

        public static LocalisableString ArenaToolTip => get(@"arena_tooltip", @"Place the target arena rectangle. A change at time zero is immediate; later changes interpolate over their timeline duration.");
        public static LocalisableString EmitterToolTip => get(@"emitter_tooltip", @"Place a source and aim point. Moving emitters use a third click for the end of the source path.");
        public static LocalisableString Reset => get(@"reset", @"Reset");
        public static LocalisableString ResetArena => get(@"reset_arena", @"Reset arena to 512 × 384");

        public static LocalisableString ProjectileShape => get(@"projectile_shape", @"Projectile shape");
        public static LocalisableString FillColour => get(@"fill_colour", @"Fill colour");
        public static LocalisableString OutlineColour => get(@"outline_colour", @"Outline colour");
        public static LocalisableString Opacity => get(@"opacity", @"Opacity");
        public static LocalisableString OutlineThickness => get(@"outline_thickness", @"Outline thickness");
        public static LocalisableString ShowAppearance => get(@"show_appearance", @"Appearance…");
        public static LocalisableString HideAppearance => get(@"hide_appearance", @"Hide appearance");
        public static LocalisableString ContinueUntilOutside => get(@"continue_until_outside", @"Continue until outside playfield");
        public static LocalisableString LockFlight => get(@"lock_flight", @"Lock flight time and speed");
        public static LocalisableString MovementType => get(@"movement_type", @"Projectile movement");
        public static LocalisableString MovementEasing => get(@"movement_easing", @"Speed curve");
        public static LocalisableString MovementEasingLinear => get(@"movement_easing_linear", @"Constant speed");
        public static LocalisableString MovementEasingIn => get(@"movement_easing_in", @"Accelerate");
        public static LocalisableString MovementEasingOut => get(@"movement_easing_out", @"Decelerate");
        public static LocalisableString MovementEasingInOut => get(@"movement_easing_in_out", @"Accelerate and decelerate");
        public static LocalisableString MovementLinear => get(@"movement_linear", @"Linear");
        public static LocalisableString MovementSine => get(@"movement_sine", @"Wave");
        public static LocalisableString WaveAmplitude => get(@"wave_amplitude", @"Wave amplitude");
        public static LocalisableString WaveCycles => get(@"wave_cycles", @"Wave cycles");
        public static LocalisableString WavePhase => get(@"wave_phase", @"Wave phase");
        public static LocalisableString BurstRotation => get(@"burst_rotation", @"Rotation per burst");
        public static LocalisableString TrajectoryGuide => get(@"trajectory_guide", @"Trajectory guide");
        public static LocalisableString GuideArrow => get(@"guide_arrow", @"Arrow");
        public static LocalisableString GuidePath => get(@"guide_path", @"Path");
        public static LocalisableString GuideFullPath => get(@"guide_full_path", @"Full path");
        public static LocalisableString GuideHidden => get(@"guide_hidden", @"Hidden");
        public static LocalisableString ArenaBackgroundColour => get(@"arena_background_colour", @"Background colour");
        public static LocalisableString ArenaBackgroundOpacity => get(@"arena_background_opacity", @"Background opacity");
        public static LocalisableString ArenaBorderColour => get(@"arena_border_colour", @"Border colour");
        public static LocalisableString ArenaBorderOpacity => get(@"arena_border_opacity", @"Border opacity");
        public static LocalisableString BulletCount => get(@"bullet_count", @"Bullet count");
        public static LocalisableString SpreadAngle => get(@"spread_angle", @"Spread angle");
        public static LocalisableString MoveEmitter => get(@"move_emitter", @"Move source while emitting");
        public static LocalisableString RepeatEmitter => get(@"repeat_emitter", @"Repeat bursts");
        public static LocalisableString BurstCount => get(@"burst_count", @"Burst count");
        public static LocalisableString BurstInterval => get(@"burst_interval_bpm", @"Burst interval (BPM)");
        public static LocalisableString BeatOne => get(@"beat_one", @"1/1 beat");
        public static LocalisableString BeatHalf => get(@"beat_half", @"1/2 beat");
        public static LocalisableString BeatThird => get(@"beat_third", @"1/3 beat");
        public static LocalisableString BeatQuarter => get(@"beat_quarter", @"1/4 beat");
        public static LocalisableString BeatSixth => get(@"beat_sixth", @"1/6 beat");
        public static LocalisableString BeatEighth => get(@"beat_eighth", @"1/8 beat");
        public static LocalisableString BeatTwelfth => get(@"beat_twelfth", @"1/12 beat");
        public static LocalisableString BeatSixteenth => get(@"beat_sixteenth", @"1/16 beat");
        public static LocalisableString Count(int value) => get(@"count_value", @"Count: {0}", value);
        public static LocalisableString Spread(float value) => get(@"spread_value", @"Spread: {0:N0}°", value);

        public static LocalisableString ShapeCircle => get(@"shape_circle", @"Circle");
        public static LocalisableString ShapeSquare => get(@"shape_square", @"Square");
        public static LocalisableString ShapeDiamond => get(@"shape_diamond", @"Diamond");
        public static LocalisableString ShapeTriangle => get(@"shape_triangle", @"Triangle");

        public static LocalisableString XOffset => get(@"x_offset", @"X offset");
        public static LocalisableString YOffset => get(@"y_offset", @"Y offset");
        public static LocalisableString Spacing => get(@"spacing", @"Spacing");
        public static LocalisableString Rotation => get(@"rotation", @"Rotation");

        public static LocalisableString SmallerPlayfield => get(@"smaller_playfield", @"Smaller playfield");
        public static LocalisableString ShowBulletCoverage => get(@"show_bullet_coverage", @"Show bullet coverage");
        public static LocalisableString ShowAutoplayRoute => get(@"show_autoplay_route", @"Show autoplay route");
        public static LocalisableString AutoplayRouteLegend => get(@"autoplay_route_legend", @"Cyan: next 4s  ·  Red: fallback");
        public static LocalisableString GridSnap => get(@"grid_snap", @"Grid snap");

        public static LocalisableString ObjectRepeatCount => get(@"object_repeat_count", @"Object / repeat count");
        public static LocalisableString TimeStep => get(@"time_step", @"Time step (snap units)");
        public static LocalisableString RepeatXOffset => get(@"repeat_x_offset", @"Repeat X offset");
        public static LocalisableString RepeatYOffset => get(@"repeat_y_offset", @"Repeat Y offset");
        public static LocalisableString RepeatRotation => get(@"repeat_rotation", @"Repeat rotation");
        public static LocalisableString QuickPrefabSlot => get(@"quick_prefab_slot", @"Quick prefab slot");
        public static LocalisableString Step(int value) => get(@"step_value", @"Step: {0}", value);
        public static LocalisableString Prefab(int value) => get(@"prefab_value", @"Prefab: {0}", value);
        public static LocalisableString RepeatArray => get(@"repeat_array", @"Repeat / array");
        public static LocalisableString Ring => get(@"ring", @"Ring");
        public static LocalisableString Spiral => get(@"spiral", @"Spiral");
        public static LocalisableString Fan => get(@"fan", @"Fan");
        public static LocalisableString WallSafeGap => get(@"wall_safe_gap", @"Wall (safe gap)");
        public static LocalisableString Sweep => get(@"sweep", @"Sweep");
        public static LocalisableString Cross => get(@"cross", @"Cross");
        public static LocalisableString SaveQuickPrefab => get(@"save_quick_prefab", @"Save quick prefab");
        public static LocalisableString InsertQuickPrefab => get(@"insert_quick_prefab", @"Insert quick prefab");
        public static LocalisableString EmptyPatternSlot => get(@"empty_pattern_slot", @"Empty slot");
        public static LocalisableString SavePatternHere => get(@"save_pattern_here", @"Save here");
        public static LocalisableString PatternSelectionRequired => get(@"pattern_selection_required", @"Select objects to enable Repeat and Save prefab.");

        public static LocalisableString RelativeX => get(@"relative_x", @"Relative X");
        public static LocalisableString RelativeY => get(@"relative_y", @"Relative Y");
        public static LocalisableString ApplyMovement => get(@"apply_movement", @"Apply movement");
        public static LocalisableString ApplyRotation => get(@"apply_rotation", @"Apply rotation");
        public static LocalisableString ScaleX => get(@"scale_x", @"Scale X (%)");
        public static LocalisableString ScaleY => get(@"scale_y", @"Scale Y (%)");
        public static LocalisableString ApplyScale => get(@"apply_scale", @"Apply scale");

        public static LocalisableString DifficultyTitle => get(@"difficulty_title", @"Dodge");
        public static LocalisableString AppearanceDuration => get(@"appearance_duration", @"Bullet appearance duration");
        public static LocalisableString AppearanceDurationHint => get(@"appearance_duration_hint", @"How long a bullet fades in before it starts moving.");
        public static LocalisableString BulletSize => get(@"bullet_size", @"Bullet size");
        public static LocalisableString BulletSizeHint => get(@"bullet_size_hint", @"The visual size and collision diameter of every bullet in this map.");
        public static LocalisableString HpDrain => get(@"hp_drain", @"HP drain");
        public static LocalisableString HpDrainHint => get(@"hp_drain_hint", @"How much health each collision removes.");
        public static LocalisableString PlayerSpeed => get(@"player_speed", @"Player speed");
        public static LocalisableString PlayerSpeedHint => get(@"player_speed_hint", @"Normal movement speed. Holding Shift uses 40% of this value.");
        public static LocalisableString PlayerSize => get(@"player_size", @"Player size");
        public static LocalisableString PlayerSizeHint => get(@"player_size_hint", @"The visual size and collision diameter of the player.");
        public static LocalisableString GrazeDistance => get(@"graze_distance", @"Graze distance");
        public static LocalisableString GrazeDistanceHint => get(@"graze_distance_hint", @"Extra distance around the collision box that awards a near-miss. Set to 0 to disable.");
        public static LocalisableString GrazeScore => get(@"graze_score", @"Graze score");
        public static LocalisableString GrazeScoreHint => get(@"graze_score_hint", @"Bonus score awarded once per projectile that passes through the graze area.");
        public static LocalisableString Disabled => get(@"disabled", @"Disabled");
        public static LocalisableString PlayfieldDim => get(@"playfield_dim", @"Playfield dim");
        public static LocalisableString PlayfieldDimHint => get(@"playfield_dim_hint", @"Controls the darkness of the arena where the player moves.");
        public static LocalisableString GrazeIndicatorBrightness => get(@"graze_indicator_brightness", @"Graze circle brightness");
        public static LocalisableString GrazeIndicatorBrightnessHint => get(@"graze_indicator_brightness_hint", @"Controls the visibility of the circle shown while a projectile is within graze range.");
        public static LocalisableString MissSound => get(@"miss_sound", @"Collision sound");
        public static LocalisableString MissSoundHint => get(@"miss_sound_hint", @"Play a skin-replaceable sound when the player is hit.");
        public static LocalisableString MissSoundVolume => get(@"miss_sound_volume", @"Collision sound volume");
        public static LocalisableString MissSoundVolumeHint => get(@"miss_sound_volume_hint", @"Volume of the Dodge collision sound.");
        public static LocalisableString EffectsEnabled => get(@"effects_enabled", @"Эффекты карты");
        public static LocalisableString EffectsEnabledHint => get(@"effects_enabled_hint", @"Тряска экрана, вспышки и прочие визуальные эффекты, добавленные триггерами карты. Не влияет на столкновения и счёт.");
        public static LocalisableString PlayerTrailEnabled => get(@"player_trail_enabled", @"След игрока");
        public static LocalisableString PlayerTrailEnabledHint => get(@"player_trail_enabled_hint", @"Отключает след игрока, включая след, включённый триггерами карты.");

        public static LocalisableString FullPathsDescription => get(@"full_paths_description", @"Shows the complete route of every active projectile.");

        private static LocalisableString get(string key, string fallback, params object[] args)
            => new TranslatableString($@"{prefix}:{key}", fallback, args);
    }
}
