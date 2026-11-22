using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    /// <summary>Only feature switches and entry points; parameters live in the shared AI panel.</summary>
    public partial class ForkAiSettings : SettingsSubsection
    {
        protected override LocalisableString Header => AiFunctionsStrings.Title;
        private readonly AiFunctionSettingsPanel? settings;

        public ForkAiSettings(AiFunctionSettingsPanel? settings = null)
        {
            this.settings = settings;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                FeatureRow(config, AiFunctionsStrings.Nsfw, AiFunctionsStrings.Description, OsuSetting.ForkAiBackgroundBlur, AiFunction.Nsfw,
                    new[] { "ai", "nsfw", "nudity", "anime", "ии", "нагота", "размытие" }),
                FeatureRow(config, AiFunctionsStrings.Distortion, AiFunctionsStrings.StillLifeHint, OsuSetting.ForkAiStillLife, AiFunction.BackgroundDistortion,
                    new[] { "ai", "tiny-sd", "lcm", "cpu", "gpu", "vulkan", "ии", "ломание", "перерисовка" }),
                FeatureRow(config, DiscoveryStrings.AudioTitle, DiscoveryStrings.AudioHint, OsuSetting.ForkAiAudioRecommendations, AiFunction.AudioRecommendations,
                    new[] { "ai", "clap", "audio", "recommendations", "music", "ии", "музыка", "подбор" }),
            };
        }

        private Drawable FeatureRow(OsuConfigManager config, LocalisableString caption, LocalisableString hint, OsuSetting setting, AiFunction function, string[] keywords)
            => new Container
            {
                Name = "ai-function-" + function,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Right = SettingsPanel.CONTENT_PADDING.Right },
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Right = 112 },
                        Child = new SettingsItemV2(new FormCheckBox
                        {
                            Caption = caption,
                            HintText = hint,
                            ExtendedHeight = true,
                            Current = config.GetBindable<bool>(setting),
                        }) {
                            FavouriteId = $"OsuSetting.{setting}", Keywords = keywords },
                    },
                    new SettingsButtonV2
                    {
                        RelativeSizeAxes = Axes.None,
                        Width = 104,
                        Height = 52,
                        Padding = new MarginPadding(),
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Text = AiFunctionsStrings.Configure,
                        TooltipText = caption,
                        Keywords = keywords,
                        Action = () => settings?.ShowFunction(function),
                    },
                },
            };
    }
}
