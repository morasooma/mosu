// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.CompilerServices;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Dodge.Beatmaps
{
    public static class DodgeBeatmapSettings
    {
        public const double APPEARANCE_DURATION_MIN = 250;
        public const double APPEARANCE_DURATION_MID = 750;
        public const double APPEARANCE_DURATION_MAX = 1500;

        public const double BULLET_SIZE_MIN = 4;
        public const double BULLET_SIZE_MID = 12;
        public const double BULLET_SIZE_MAX = 32;

        public const double PLAYER_SPEED_MIN = 80;
        public const double PLAYER_SPEED_MID = 240;
        public const double PLAYER_SPEED_MAX = 480;

        public const double PLAYER_SIZE_MIN = 6;
        public const double PLAYER_SIZE_MID = 16;
        public const double PLAYER_SIZE_MAX = 32;

        public const double GRAZE_DISTANCE_MIN = 0;
        public const double GRAZE_DISTANCE_MAX = 64;
        public const double GRAZE_SCORE_MIN = 0;
        public const double GRAZE_SCORE_DEFAULT = 100;
        public const double GRAZE_SCORE_MAX = 1000;

        public static double GetAppearanceDuration(IBeatmapDifficultyInfo difficulty)
            => IBeatmapDifficultyInfo.DifficultyRange(
                difficulty.ApproachRate,
                APPEARANCE_DURATION_MAX,
                APPEARANCE_DURATION_MID,
                APPEARANCE_DURATION_MIN);

        public static float GetApproachRate(double appearanceDuration)
            => (float)IBeatmapDifficultyInfo.InverseDifficultyRange(
                Math.Clamp(appearanceDuration, APPEARANCE_DURATION_MIN, APPEARANCE_DURATION_MAX),
                APPEARANCE_DURATION_MAX,
                APPEARANCE_DURATION_MID,
                APPEARANCE_DURATION_MIN);

        public static float GetBulletSize(IBeatmapDifficultyInfo difficulty)
            => (float)Math.Clamp(
                IBeatmapDifficultyInfo.DifficultyRange(
                    difficulty.CircleSize,
                    BULLET_SIZE_MAX,
                    BULLET_SIZE_MID,
                    BULLET_SIZE_MIN),
                BULLET_SIZE_MIN,
                BULLET_SIZE_MAX);

        public static float GetCircleSize(double bulletSize)
            => (float)IBeatmapDifficultyInfo.InverseDifficultyRange(
                Math.Clamp(bulletSize, BULLET_SIZE_MIN, BULLET_SIZE_MAX),
                BULLET_SIZE_MAX,
                BULLET_SIZE_MID,
                BULLET_SIZE_MIN);

        public static double GetPlayerSpeed(IBeatmapDifficultyInfo difficulty)
            => Math.Clamp(IBeatmapDifficultyInfo.DifficultyRange(
                difficulty.OverallDifficulty,
                PLAYER_SPEED_MIN,
                PLAYER_SPEED_MID,
                PLAYER_SPEED_MAX), PLAYER_SPEED_MIN, PLAYER_SPEED_MAX);

        public static float GetOverallDifficulty(double playerSpeed)
            => (float)IBeatmapDifficultyInfo.InverseDifficultyRange(
                Math.Clamp(playerSpeed, PLAYER_SPEED_MIN, PLAYER_SPEED_MAX),
                PLAYER_SPEED_MIN,
                PLAYER_SPEED_MID,
                PLAYER_SPEED_MAX);

        private static readonly ConditionalWeakTable<IBeatmapDifficultyInfo, PlayerSizeHolder> playerSizes = new ConditionalWeakTable<IBeatmapDifficultyInfo, PlayerSizeHolder>();
        private static readonly ConditionalWeakTable<IBeatmapDifficultyInfo, VisualSettingsHolder> visualSettings = new ConditionalWeakTable<IBeatmapDifficultyInfo, VisualSettingsHolder>();

        public static float GetPlayerSize(IBeatmapDifficultyInfo difficulty)
            => playerSizes.TryGetValue(difficulty, out PlayerSizeHolder? holder)
                ? holder.Value
                : (float)PLAYER_SIZE_MID;

        public static void SetPlayerSize(IBeatmapDifficultyInfo difficulty, double playerSize)
        {
            playerSizes.Remove(difficulty);
            playerSizes.Add(difficulty, new PlayerSizeHolder
            {
                Value = (float)Math.Clamp(playerSize, PLAYER_SIZE_MIN, PLAYER_SIZE_MAX),
            });
        }

        public static bool GetForceStoryboard(IBeatmapDifficultyInfo difficulty)
            => visualSettings.TryGetValue(difficulty, out VisualSettingsHolder? holder) && holder.ForceStoryboard;

        public static void SetForceStoryboard(IBeatmapDifficultyInfo difficulty, bool value)
            => getVisualSettings(difficulty).ForceStoryboard = value;

        public static bool GetForceBeatmapSkin(IBeatmapDifficultyInfo difficulty)
            => visualSettings.TryGetValue(difficulty, out VisualSettingsHolder? holder) && holder.ForceBeatmapSkin;

        public static void SetForceBeatmapSkin(IBeatmapDifficultyInfo difficulty, bool value)
            => getVisualSettings(difficulty).ForceBeatmapSkin = value;

        private static VisualSettingsHolder getVisualSettings(IBeatmapDifficultyInfo difficulty)
            => visualSettings.GetValue(difficulty, _ => new VisualSettingsHolder());

        // Slider multiplier 1.4 is the lazer default. Keeping that value equal to
        // zero graze distance makes newly-created and legacy maps opt out of graze.
        public static double GetGrazeDistance(IBeatmapDifficultyInfo difficulty)
            => Math.Clamp((difficulty.SliderMultiplier - 1.4) * 32, GRAZE_DISTANCE_MIN, GRAZE_DISTANCE_MAX);

        public static double GetSliderMultiplier(double grazeDistance)
            => 1.4 + Math.Clamp(grazeDistance, GRAZE_DISTANCE_MIN, GRAZE_DISTANCE_MAX) / 32;

        public static double GetGrazeScore(IBeatmapDifficultyInfo difficulty)
        {
            double configuredScore = Math.Clamp((difficulty.SliderTickRate - 1) * 100, GRAZE_SCORE_MIN, GRAZE_SCORE_MAX);

            // Early Dodge maps could enable graze while retaining lazer's default
            // slider tick rate, resulting in a working but invisible zero-point
            // graze. Migrate those maps to a useful value when graze is enabled.
            return GetGrazeDistance(difficulty) > 0 && configuredScore <= 0
                ? GRAZE_SCORE_DEFAULT
                : configuredScore;
        }

        public static double GetSliderTickRate(double grazeScore)
            => 1 + Math.Clamp(grazeScore, GRAZE_SCORE_MIN, GRAZE_SCORE_MAX) / 100;

        private class PlayerSizeHolder
        {
            public float Value;
        }

        private class VisualSettingsHolder
        {
            public bool ForceStoryboard;
            public bool ForceBeatmapSkin;
        }
    }
}
