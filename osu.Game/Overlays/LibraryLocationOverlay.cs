using osu.Framework.Allocation;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays.FirstRunSetup;
using osu.Game.Screens;
using osu.Game.Screens.Menu;

namespace osu.Game.Overlays
{
    /// <summary>Separate startup choice, shown before the ordinary first-run wizard.</summary>
    public partial class LibraryLocationOverlay : WizardOverlay
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private IPerformFromScreenRunner performer { get; set; } = null!;

        [Resolved]
        private OsuGame game { get; set; } = null!;

        public LibraryLocationOverlay() : base(OverlayColourScheme.Purple)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Header.Title = LibraryLocationStrings.Welcome;
            Header.Description = LibraryLocationStrings.Description;
            Header.Close = () => OnBackButton();
            AddStep<ScreenLibraryLocation>();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            if (!config.Get<bool>(OsuSetting.ForkLibraryLocationChosen))
                Show();
        }

        public override void Show() => performer.PerformFromScreen(screen =>
        {
            if (screen is MainMenu menu)
                menu.ReturnToOsuLogo();
            base.Show();
        }, new[] { typeof(MainMenu) });

        // Unlike dismissible informational wizards, the startup choice is modal.
        // A click on unused space must not hide it or activate the menu underneath.
        protected override bool OnClick(ClickEvent e) => true;

        public override bool OnBackButton()
        {
            // Cancelling keeps the current Mosu library and completes the startup choice.
            if (CurrentStepIndex != null)
                ShowNextStep();
            return true;
        }

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action == GlobalAction.Back)
            {
                if (!e.Repeat)
                    OnBackButton();
                return true;
            }

            return base.OnPressed(e);
        }

        protected override void ShowNextStep()
        {
            base.ShowNextStep();
            if (CurrentStepIndex == null)
            {
                config.SetValue(OsuSetting.ForkLibraryLocationChosen, true);
                config.Save();
                game.ShowInitialSetupAfterLibrarySelection();
            }
        }
    }
}
