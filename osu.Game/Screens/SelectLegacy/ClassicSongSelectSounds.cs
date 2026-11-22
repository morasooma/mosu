using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Input.Events;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Screens.SelectLegacy
{
    /// <summary>
    /// Original footer hover and click samples, isolated from the modern UI sample set.
    /// </summary>
    public partial class ClassicSongSelectSounds : HoverSounds
    {
        private Sample? hover;
        private Sample? click;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            hover = audio.Samples.Get("SongSelectClassic/generic-hover");
            click = audio.Samples.Get("SongSelectClassic/generic-select");
        }

        public override void PlayHoverSample()
        {
            if (Enabled.Value)
                hover?.Play();
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (Enabled.Value && e.Button == osuTK.Input.MouseButton.Left)
                click?.Play();
            return base.OnClick(e);
        }
    }
}
