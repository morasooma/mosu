// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;
using osu.Framework.Bindables;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Mosu-specific settings which belong to a skin rather than to the client.
    /// </summary>
    [Serializable]
    public class MosuSkinSettings
    {
        public const string FILENAME = "mosu-skin-settings.json";
        public const int LATEST_VERSION = 1;

        public int Version { get; set; } = LATEST_VERSION;

        /// <summary>
        /// The visual scale of the mania notefield, in percent.
        /// </summary>
        [JsonIgnore]
        public BindableNumber<int> ManiaNoteScalePercent { get; } = new BindableNumber<int>(100)
        {
            MinValue = 20,
            MaxValue = 100,
            Precision = 1,
            Default = 100,
        };

        [JsonProperty("ManiaNoteScalePercent")]
        private int maniaNoteScalePercent
        {
            get => ManiaNoteScalePercent.Value;
            set => SetManiaNoteScalePercent(value);
        }

        public int GetManiaNoteScalePercent() => Math.Clamp(ManiaNoteScalePercent.Value, 20, 100);

        public void SetManiaNoteScalePercent(int value) => ManiaNoteScalePercent.Value = Math.Clamp(value, 20, 100);
    }
}
