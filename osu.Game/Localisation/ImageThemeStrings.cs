using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ImageThemeStrings
    {
        private const string prefix = "osu.Game.Localisation.ImageTheme";
        /// <summary>"Theme from image"</summary>
        public static LocalisableString Open => new TranslatableString($"{prefix}:open", "Theme from image");
        /// <summary>"Choose a profile image or the current beatmap background. A new theme is saved only when you apply it; your existing theme is kept."</summary>
        public static LocalisableString Description => new TranslatableString($"{prefix}:description", "Choose a profile image or the current beatmap background. A new theme is saved only when you apply it; your existing theme is kept.");
        /// <summary>"Avatar"</summary>
        public static LocalisableString Avatar => new TranslatableString($"{prefix}:avatar", "Avatar");
        /// <summary>"Banner"</summary>
        public static LocalisableString Banner => new TranslatableString($"{prefix}:banner", "Banner");
        /// <summary>"Profile background"</summary>
        public static LocalisableString Background => new TranslatableString($"{prefix}:background", "Profile background");
        /// <summary>"Current background"</summary>
        public static LocalisableString CurrentBackground => new TranslatableString($"{prefix}:current_background", "Current background");
        /// <summary>"Cancel"</summary>
        public static LocalisableString Cancel => new TranslatableString($"{prefix}:cancel", "Cancel");
        /// <summary>"Accent colour"</summary>
        public static LocalisableString Accent => new TranslatableString($"{prefix}:accent", "Accent colour");
        /// <summary>"Appearance"</summary>
        public static LocalisableString Appearance => new TranslatableString($"{prefix}:appearance", "Appearance");
        /// <summary>"Keep current appearance"</summary>
        public static LocalisableString Keep => new TranslatableString($"{prefix}:keep", "Keep current appearance");
        /// <summary>"Rounded"</summary>
        public static LocalisableString Rounded => new TranslatableString($"{prefix}:rounded", "Rounded");
        /// <summary>"Sharp"</summary>
        public static LocalisableString Sharp => new TranslatableString($"{prefix}:sharp", "Sharp");
        /// <summary>"Keep your geometry, top bar and gradients, or choose a shape preset. Font, transparency, animations and song selection layout are always preserved."</summary>
        public static LocalisableString AppearanceHint => new TranslatableString($"{prefix}:appearance_hint", "Keep your geometry, top bar and gradients, or choose a shape preset. Font, transparency, animations and song selection layout are always preserved.");
        /// <summary>"Light theme"</summary>
        public static LocalisableString Light => new TranslatableString($"{prefix}:light", "Light theme");
        /// <summary>"Create and apply theme"</summary>
        public static LocalisableString Apply => new TranslatableString($"{prefix}:apply", "Create and apply theme");
        /// <summary>"Loading profile images…"</summary>
        public static LocalisableString Loading => new TranslatableString($"{prefix}:loading", "Loading profile images…");
        /// <summary>"Extracting colours…"</summary>
        public static LocalisableString Analysing => new TranslatableString($"{prefix}:analysing", "Extracting colours…");
        /// <summary>"Choose an image above."</summary>
        public static LocalisableString Choose => new TranslatableString($"{prefix}:choose", "Choose an image above.");
        /// <summary>"This profile image is not set."</summary>
        public static LocalisableString Unavailable => new TranslatableString($"{prefix}:unavailable", "This profile image is not set.");
        /// <summary>"Could not analyse the image. Try again or choose another image."</summary>
        public static LocalisableString Failed => new TranslatableString($"{prefix}:failed", "Could not analyse the image. Try again or choose another image.");
        /// <summary>"Could not refresh the profile. Available images are shown; reopen to retry."</summary>
        public static LocalisableString ProfileFailed => new TranslatableString($"{prefix}:profile_failed", "Could not refresh the profile. Available images are shown; reopen to retry.");
        /// <summary>"Log in to use your profile images."</summary>
        public static LocalisableString Login => new TranslatableString($"{prefix}:login", "Log in to use your profile images.");
        /// <summary>"Preview only — your current theme has not changed."</summary>
        public static LocalisableString Ready => new TranslatableString($"{prefix}:ready", "Preview only — your current theme has not changed.");
        /// <summary>"Theme preview"</summary>
        public static LocalisableString Sample => new TranslatableString($"{prefix}:sample", "Theme preview");
        /// <summary>"Text, panels and controls"</summary>
        public static LocalisableString SampleDetail => new TranslatableString($"{prefix}:sample_detail", "Text, panels and controls");
        /// <summary>"Selected"</summary>
        public static LocalisableString SampleButton => new TranslatableString($"{prefix}:sample_button", "Selected");
        /// <summary>"Image theme"</summary>
        public static LocalisableString Generated => new TranslatableString($"{prefix}:generated", "Image theme");
    }
}
