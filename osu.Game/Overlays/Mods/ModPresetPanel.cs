// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Overlays.Mods
{
    public partial class ModPresetPanel : ModSelectPanel, IHasCustomTooltip<ModPreset>, IHasContextMenu, IHasPopover
    {
        public readonly Live<ModPreset> Preset;

        public int? Index { get; init; }

        public override BindableBool Active { get; } = new BindableBool();

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> selectedMods { get; set; } = null!;

        [Resolved]
        private ModSelectOverlay overlay { get; set; } = null!;

        private ModSettingChangeTracker? settingChangeTracker;

        private OsuSpriteText? shortcutKeyText;

        private IBindable<bool> showModsInPresetList = null!;

        private readonly FillFlowContainer modIconsFlow;

        public ModPresetPanel(Live<ModPreset> preset)
        {
            Preset = preset;
            preset.PerformRead(value =>
            {
                Title = value.Name;
                Description = value.Description;
            });

            Mod[] displayMods = preset.PerformRead(value => value.Mods.ToArray());

            TextFlow.Add(modIconsFlow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.X,
                Height = 14,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(-1, 0),
                ChildrenEnumerable = displayMods.Select(mod => new ModIcon(mod, showTooltip: false, showExtendedInformation: false)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Scale = new Vector2(14f / ModIcon.MOD_ICON_SIZE.Y),
                })
            });
        }

        protected override float IdleSwitchWidth => 24;
        protected override float ExpandedSwitchWidth => 40;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, OsuConfigManager config)
        {
            AccentColour = colours.Orange1;

            showModsInPresetList = config.GetBindable<bool>(OsuSetting.ForkShowModsInPresetList);
            showModsInPresetList.BindValueChanged(_ => updateModDisplay(), true);

            if (Index != null)
            {
                SwitchContainer.Child = shortcutKeyText = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Shear = -OsuGame.SHEAR,
                    Text = Index.Value.ToString(),
                    Font = OsuFont.Style.Heading2,
                    Alpha = 0,
                    Margin = new MarginPadding(10),
                };
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selectedMods.BindValueChanged(_ => selectedModsChanged(), true);

            Active.BindValueChanged(active =>
            {
                shortcutKeyText?.FadeTo(active.NewValue ? 1 : 0.4f, TRANSITION_DURATION, Easing.OutQuint);
            }, true);
        }

        public void Toggle()
        {
            if (!Active.Value)
                Select();
            else
                Deselect();
        }

        protected override void Select()
        {
            ICollection<Mod> presetMods = Preset.Value.Mods;

            // this implicitly presumes that if a system mod declares incompatibility with a non-system mod,
            // the non-system mod should take precedence.
            // if this assumption is ever broken, this should be reconsidered.
            var selectedSystemMods = selectedMods.Value.Where(mod => mod.Type == ModType.System &&
                                                                     !mod.IncompatibleMods.Any(t => presetMods.Any(t.IsInstanceOfType)));

            // Only apply preset mods that are valid/allowed in the current mod select overlay.
            var validPresetMods = presetMods.Where(mod => overlay.IsValidMod(mod)).ToArray();

            // will also have the side effect of activating the preset (see `updateActiveState()`).
            selectedMods.Value = validPresetMods.Concat(selectedSystemMods).ToArray();
        }

        protected override void Deselect()
        {
            ICollection<Mod> presetMods = Preset.Value.Mods;
            selectedMods.Value = selectedMods.Value.Except(presetMods).ToArray();
        }

        private void selectedModsChanged()
        {
            settingChangeTracker?.Dispose();
            settingChangeTracker = new ModSettingChangeTracker(selectedMods.Value);
            settingChangeTracker.SettingChanged = _ => updateActiveState();
            updateActiveState();
        }

        private void updateActiveState()
        {
            ICollection<Mod> presetMods = Preset.Value.Mods;
            Active.Value = new HashSet<Mod>(presetMods).SetEquals(selectedMods.Value.Where(mod => mod.Type != ModType.System));
        }

        private void updateModDisplay()
        {
            if (showModsInPresetList.Value)
            {
                DescriptionText.Hide();
                modIconsFlow.Show();
            }
            else
            {
                DescriptionText.Show();
                modIconsFlow.Hide();
            }
        }

        #region Filtering support

        public override IEnumerable<LocalisableString> FilterTerms => getFilterTerms();

        private IEnumerable<LocalisableString> getFilterTerms()
        {
            var preset = Preset.Value;

            yield return preset.Name;
            yield return preset.Description;

            ICollection<Mod> presetMods = preset.Mods;

            foreach (Mod mod in presetMods)
            {
                yield return mod.Name;
                yield return mod.Acronym;
                yield return mod.Description;
            }
        }

        #endregion

        #region IHasCustomTooltip

        public ModPreset TooltipContent => Preset.Value;
        public ITooltip<ModPreset> GetCustomTooltip() => new ModPresetTooltip(ColourProvider);

        #endregion

        #region IHasContextMenu

        public MenuItem[] ContextMenuItems => new MenuItem[]
        {
            new OsuMenuItem(CommonStrings.ButtonsEdit, MenuItemType.Highlighted, this.ShowPopover),
            new OsuMenuItem(CommonStrings.ButtonsDelete, MenuItemType.Destructive, () => dialogOverlay?.Push(new DeleteModPresetDialog(Preset))),
        };

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            settingChangeTracker?.Dispose();
        }

        public Popover GetPopover() => new EditPresetPopover(Preset);
    }
}
