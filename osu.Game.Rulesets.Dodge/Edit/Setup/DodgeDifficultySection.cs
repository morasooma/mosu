// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Screens.Edit.Setup;

namespace osu.Game.Rulesets.Dodge.Edit.Setup
{
    public partial class DodgeDifficultySection : SetupSection
    {
        private FormSliderBar<double> appearanceDurationSlider = null!;
        private FormSliderBar<double> bulletSizeSlider = null!;
        private FormSliderBar<double> hpDrainSlider = null!;
        private FormSliderBar<double> playerSpeedSlider = null!;
        private FormSliderBar<double> playerSizeSlider = null!;
        private FormSliderBar<double> grazeDistanceSlider = null!;
        private FormSliderBar<double> grazeScoreSlider = null!;

        public override LocalisableString Title => DodgeEditorStrings.DifficultyTitle;

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                appearanceDurationSlider = new FormSliderBar<double>
                {
                    Caption = DodgeEditorStrings.AppearanceDuration,
                    HintText = DodgeEditorStrings.AppearanceDurationHint,
                    KeyboardStep = 25,
                    Current = new BindableDouble(DodgeBeatmapSettings.GetAppearanceDuration(Beatmap.Difficulty))
                    {
                        Default = DodgeBeatmapSettings.APPEARANCE_DURATION_MID,
                        MinValue = DodgeBeatmapSettings.APPEARANCE_DURATION_MIN,
                        MaxValue = DodgeBeatmapSettings.APPEARANCE_DURATION_MAX,
                        Precision = 25,
                    },
                    LabelFormat = value => $"{value:N0} ms",
                    TransferValueOnCommit = true,
                    TabbableContentContainer = this,
                },
                bulletSizeSlider = new FormSliderBar<double>
                {
                    Caption = DodgeEditorStrings.BulletSize,
                    HintText = DodgeEditorStrings.BulletSizeHint,
                    KeyboardStep = 1,
                    Current = new BindableDouble(DodgeBeatmapSettings.GetBulletSize(Beatmap.Difficulty))
                    {
                        Default = DodgeBeatmapSettings.BULLET_SIZE_MID,
                        MinValue = DodgeBeatmapSettings.BULLET_SIZE_MIN,
                        MaxValue = DodgeBeatmapSettings.BULLET_SIZE_MAX,
                        Precision = 1,
                    },
                    LabelFormat = value => $"{value:N0} px",
                    TransferValueOnCommit = true,
                    TabbableContentContainer = this,
                },
                hpDrainSlider = new FormSliderBar<double>
                {
                    Caption = DodgeEditorStrings.HpDrain,
                    HintText = DodgeEditorStrings.HpDrainHint,
                    KeyboardStep = 0.5f,
                    Current = new BindableDouble(Beatmap.Difficulty.DrainRate)
                    {
                        Default = 5,
                        MinValue = 0,
                        MaxValue = 10,
                        Precision = 0.1,
                    },
                    LabelFormat = value => $"{value:N1}",
                    TransferValueOnCommit = true,
                    TabbableContentContainer = this,
                },
                playerSpeedSlider = new FormSliderBar<double>
                {
                    Caption = DodgeEditorStrings.PlayerSpeed,
                    HintText = DodgeEditorStrings.PlayerSpeedHint,
                    KeyboardStep = 10,
                    Current = new BindableDouble(DodgeBeatmapSettings.GetPlayerSpeed(Beatmap.Difficulty))
                    {
                        Default = DodgeBeatmapSettings.PLAYER_SPEED_MID,
                        MinValue = DodgeBeatmapSettings.PLAYER_SPEED_MIN,
                        MaxValue = DodgeBeatmapSettings.PLAYER_SPEED_MAX,
                        Precision = 10,
                    },
                    LabelFormat = value => $"{value:N0} px/s",
                    TransferValueOnCommit = true,
                    TabbableContentContainer = this,
                },
                playerSizeSlider = new FormSliderBar<double>
                {
                    Caption = DodgeEditorStrings.PlayerSize,
                    HintText = DodgeEditorStrings.PlayerSizeHint,
                    KeyboardStep = 1,
                    Current = new BindableDouble(DodgeBeatmapSettings.GetPlayerSize(Beatmap.Difficulty))
                    {
                        Default = DodgeBeatmapSettings.PLAYER_SIZE_MID,
                        MinValue = DodgeBeatmapSettings.PLAYER_SIZE_MIN,
                        MaxValue = DodgeBeatmapSettings.PLAYER_SIZE_MAX,
                        Precision = 1,
                    },
                    LabelFormat = value => $"{value:N0} px",
                    TransferValueOnCommit = true,
                    TabbableContentContainer = this,
                },
                grazeDistanceSlider = new FormSliderBar<double>
                {
                    Caption = DodgeEditorStrings.GrazeDistance,
                    HintText = DodgeEditorStrings.GrazeDistanceHint,
                    KeyboardStep = 1,
                    Current = new BindableDouble(DodgeBeatmapSettings.GetGrazeDistance(Beatmap.Difficulty))
                    {
                        Default = 0,
                        MinValue = DodgeBeatmapSettings.GRAZE_DISTANCE_MIN,
                        MaxValue = DodgeBeatmapSettings.GRAZE_DISTANCE_MAX,
                        Precision = 1,
                    },
                    LabelFormat = value => value == 0 ? DodgeEditorStrings.Disabled : $"{value:N0} px",
                    TransferValueOnCommit = true,
                    TabbableContentContainer = this,
                },
                grazeScoreSlider = new FormSliderBar<double>
                {
                    Caption = DodgeEditorStrings.GrazeScore,
                    HintText = DodgeEditorStrings.GrazeScoreHint,
                    KeyboardStep = 10,
                    Current = new BindableDouble(DodgeBeatmapSettings.GetGrazeScore(Beatmap.Difficulty))
                    {
                        Default = DodgeBeatmapSettings.GRAZE_SCORE_DEFAULT,
                        MinValue = DodgeBeatmapSettings.GRAZE_SCORE_MIN,
                        MaxValue = DodgeBeatmapSettings.GRAZE_SCORE_MAX,
                        Precision = 10,
                    },
                    LabelFormat = value => $"{value:N0}",
                    TransferValueOnCommit = true,
                    TabbableContentContainer = this,
                },
            };

            appearanceDurationSlider.Current.ValueChanged += _ => updateValues();
            bulletSizeSlider.Current.ValueChanged += _ => updateValues();
            hpDrainSlider.Current.ValueChanged += _ => updateValues();
            playerSpeedSlider.Current.ValueChanged += _ => updateValues();
            playerSizeSlider.Current.ValueChanged += _ => updateValues();
            grazeDistanceSlider.Current.ValueChanged += _ => updateValues();
            grazeScoreSlider.Current.ValueChanged += _ => updateValues();
        }

        private void updateValues()
        {
            Beatmap.Difficulty.ApproachRate = DodgeBeatmapSettings.GetApproachRate(appearanceDurationSlider.Current.Value);
            Beatmap.Difficulty.CircleSize = DodgeBeatmapSettings.GetCircleSize(bulletSizeSlider.Current.Value);
            Beatmap.Difficulty.DrainRate = (float)hpDrainSlider.Current.Value;
            Beatmap.Difficulty.OverallDifficulty = DodgeBeatmapSettings.GetOverallDifficulty(playerSpeedSlider.Current.Value);
            DodgeBeatmapSettings.SetPlayerSize(Beatmap.Difficulty, playerSizeSlider.Current.Value);
            Beatmap.Difficulty.SliderMultiplier = DodgeBeatmapSettings.GetSliderMultiplier(grazeDistanceSlider.Current.Value);
            Beatmap.Difficulty.SliderTickRate = DodgeBeatmapSettings.GetSliderTickRate(grazeScoreSlider.Current.Value);

            Beatmap.UpdateAllHitObjects();
            Beatmap.SaveState();
        }
    }
}
