using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class InterfaceAnimationStrings
    {
        private const string prefix = "osu.Game.Localisation.InterfaceAnimation";

        /// <summary>"Interface animations"</summary>
        public static LocalisableString Caption => new TranslatableString($"{prefix}:caption", "Interface animations");
        /// <summary>"Applies immediately. Minimal keeps short fades but removes decorative movement. Gameplay is not affected."</summary>
        public static LocalisableString Hint => new TranslatableString($"{prefix}:hint", "Applies immediately. Minimal keeps short fades but removes decorative movement. Gameplay is not affected.");
        /// <summary>"Enhanced animations"</summary>
        public static LocalisableString EnhancedCaption => new TranslatableString($"{prefix}:enhanced_caption", "Enhanced animations");
        /// <summary>"Smoother, shorter menu and panel transitions, and expanding settings rows. Requires full interface animations. Gameplay is not affected."</summary>
        public static LocalisableString EnhancedHint => new TranslatableString($"{prefix}:enhanced_hint", "Smoother, shorter menu and panel transitions, and expanding settings rows. Requires full interface animations. Gameplay is not affected.");
        /// <summary>"Full"</summary>
        public static LocalisableString Full => new TranslatableString($"{prefix}:full", "Full");
        /// <summary>"Minimal"</summary>
        public static LocalisableString Minimal => new TranslatableString($"{prefix}:minimal", "Minimal");
        /// <summary>"Disabled"</summary>
        public static LocalisableString Disabled => new TranslatableString($"{prefix}:disabled", "Disabled");
    }
}
