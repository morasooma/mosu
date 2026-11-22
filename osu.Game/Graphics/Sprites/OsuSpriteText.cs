// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.IO.Stores;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Fonts;
using osu.Game.Skinning;

namespace osu.Game.Graphics.Sprites
{
    public partial class OsuSpriteText : SpriteText
    {
        [Obsolete("Use TruncatingSpriteText instead.")]
        public new bool Truncate
        {
            set => throw new InvalidOperationException($"Use {nameof(TruncatingSpriteText)} instead.");
        }

        public OsuSpriteText()
        {
            Shadow = true;
            Font = OsuFont.Default;
        }

        private readonly List<IBindable<string>> fontBindings = new List<IBindable<string>>();
        private IBindable<bool>? separateFonts;
        private OsuConfigManager fontConfig = null!;
        private string originalFamily = null!;
        private IBindable<string>? skinComponentFamily;
        private FontUsage? originalComponentFont;
        private Drawable? fontOwner;
        private Drawable? lastFontParent;
        private bool checkedFontParent;
        private float selectedComponentSize;
        private string? appliedComponentFamily;

        internal bool HasSkinComponentFont => !string.IsNullOrEmpty(appliedComponentFamily);

        // Texture-backed fonts override this to keep their original pixel height.
        protected virtual float ComponentFontSize => Font.Size;
        protected virtual bool UsesSkinGlyphStore => false;

        protected virtual void ComponentFontChanged(bool enabled) { }

        private void refreshComponentFontOwner()
        {
            if (checkedFontParent && lastFontParent == Parent) return;
            checkedFontParent = true;
            lastFontParent = Parent;
            Drawable? owner = Parent;
            while (owner != null && owner is not ISerialisableDrawable)
                owner = owner.Parent;
            if (owner == fontOwner) return;
            skinComponentFamily?.UnbindAll();
            skinComponentFamily = null;
            fontOwner = owner;
            if (owner == null || owner is FontAdjustableSkinComponent)
            {
                applyComponentFont();
                return;
            }
            skinComponentFamily = SkinComponentFont.For(owner).Family.GetBoundCopy();
            skinComponentFamily.BindValueChanged(_ => applyComponentFont(), true);
        }

        private void applyComponentFont()
        {
            string? family = skinComponentFamily?.Value;
            if (string.IsNullOrEmpty(family))
            {
                if (originalComponentFont.HasValue)
                {
                    Font = originalComponentFont.Value;
                    originalComponentFont = null;
                    appliedComponentFamily = null;
                    ComponentFontChanged(false);
                    if (!UsesSkinGlyphStore) onFontChanged();
                }
                return;
            }
            if (family == appliedComponentFamily) return;
            if (!originalComponentFont.HasValue)
            {
                selectedComponentSize = ComponentFontSize;
                originalComponentFont = Font;
            }
            float size = selectedComponentSize;
            appliedComponentFamily = family;
            Font = OsuFont.GetFont(family, size: size, weight: FontWeight.Regular, fixedWidth: Font.FixedWidth);
            ComponentFontChanged(true);
            RefreshAsyncTextFont();
        }

        /// <summary>
        /// Dynamic text whose cold glyph lookups must not run during Update/layout.
        /// Clears the previous label immediately and publishes the current localised
        /// text only after its glyphs are prepared. Ordinary <see cref="SpriteText.Text"/>
        /// retains its synchronous behaviour for other UI.
        /// </summary>
        public LocalisableString AsyncText
        {
            get => requestedAsyncText;
            set
            {
                if (IsDisposed || (usesAsyncText && requestedAsyncText.Equals(value)))
                    return;

                usesAsyncText = true;
                requestedAsyncText = value;
                if (IsLoaded)
                    refreshAsyncText();
            }
        }

        private bool usesAsyncText;
        private LocalisableString requestedAsyncText = string.Empty;
        private FontStore fonts = null!;
        private LocalisationManager localisation = null!;
        private IBindable<LocalisationParameters>? asyncLocalisation;
        private CancellationTokenSource? asyncTextCancellation;
        private readonly Lock asyncTextPublicationLock = new Lock();
        private bool asyncTextPending;
        private FontUsage preparedAsyncFont;
        // TextureStore's synchronous lookup can join another in-flight texture.
        // Its WaitSafely contract requires a non-TPL worker, not Task.Run.
        private static readonly ThreadedTaskScheduler glyphPreparationScheduler = new ThreadedTaskScheduler(1, "Mosu text glyph preparation");

        // SpriteText skips its scheduler while empty. Keep only a pending visible
        // label updating until publication; no old glyphs are rendered meanwhile.
        public override bool IsPresent => base.IsPresent || (asyncTextPending && Alpha > 0.0001f
            && DrawScale.X != 0 && DrawScale.Y != 0);

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, FontStore fonts, LocalisationManager localisation)
        {
            this.fonts = fonts;
            this.localisation = localisation;
            originalFamily = Font.Family;
            fontConfig = config;
            separateFonts = config.GetBindable<bool>(OsuSetting.ForkUseSeparateFonts);
            separateFonts.BindValueChanged(_ => onFontChanged());
            foreach (var setting in new[] { OsuSetting.ForkCustomUIFont, OsuSetting.ForkMainFont,
                         OsuSetting.ForkAlternateFont, OsuSetting.ForkNumericFont, OsuSetting.ForkInterFont })
            {
                var binding = config.GetBindable<string>(setting);
                fontBindings.Add(binding);
                binding.BindValueChanged(_ => onFontChanged());
            }
            onFontChanged();

            // The base loader ran before the configured family was applied.
            // Prepare its actual glyphs using the normal TextBuilder fallback order.
            GameplayGlyphPrecache.Prepare(fonts, Font, localisation.GetLocalisedString(Text));
            GameplayGlyphPrecache.Prepare(fonts, Font, EllipsisString);
            GameplayGlyphPrecache.Prepare(fonts, Font, $"{FallbackCharacter}{FixedWidthReferenceCharacter}");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            refreshComponentFontOwner();
            if (usesAsyncText)
                refreshAsyncText();
        }

        protected override void Update()
        {
            base.Update();
            refreshComponentFontOwner();
        }

        internal void RefreshAsyncTextFont()
        {
            if (IsLoaded && usesAsyncText && !Font.Equals(preparedAsyncFont))
                refreshAsyncText();
        }

        private void refreshAsyncText()
        {
            if (IsDisposed)
                return;

            // Like the font configuration callbacks, localisation changes are applied
            // on Update. Refresh immediately: an empty label cannot drain a scheduled
            // refresh, and the new script must not enter layout before preparation.
            if (asyncLocalisation == null)
            {
                asyncLocalisation = localisation.CurrentParameters.GetBoundCopy();
                asyncLocalisation.BindValueChanged(_ => refreshAsyncText());
            }

            CancellationToken token;
            lock (asyncTextPublicationLock)
            {
                asyncTextCancellation?.Cancel();
                asyncTextCancellation?.Dispose();
                asyncTextCancellation = new CancellationTokenSource();
                token = asyncTextCancellation.Token;
            }
            string text = localisation.GetLocalisedString(requestedAsyncText);
            var font = preparedAsyncFont = Font;
            string layoutCharacters = $"{EllipsisString}{FallbackCharacter}{FixedWidthReferenceCharacter}";
            asyncTextPending = text.Length > 0;
            Text = string.Empty;
            Invalidate(Invalidation.Presence);
            if (!asyncTextPending)
                return;

            _ = Task.Factory.StartNew(() =>
            {
                GameplayGlyphPrecache.Prepare(fonts, font, layoutCharacters, token);
                GameplayGlyphPrecache.Prepare(fonts, font, text, token);
            }, token, TaskCreationOptions.HideScheduler, glyphPreparationScheduler).ContinueWith(completed =>
            {
                var error = completed.Exception;
                if (error != null && !token.IsCancellationRequested)
                    Logger.Error(error, "Could not prepare dynamic song-select text");
                lock (asyncTextPublicationLock)
                {
                    if (IsDisposed || token.IsCancellationRequested)
                        return;

                    // Pooled/hidden labels may not update their scheduler for a
                    // long time. Keep one publication, not every obsolete string.
                    // Serialise against cancellation so an older completion cannot
                    // replace a newer result in AddOnce's latest-data slot.
                    Scheduler.AddOnce(applyAsyncText, (token, font, text, completed.IsCompletedSuccessfully));
                }
            }, TaskScheduler.Default);
        }

        private void applyAsyncText((CancellationToken Token, FontUsage Font, string Text, bool Succeeded) result)
        {
            if (IsDisposed || result.Token.IsCancellationRequested)
                return;
            if (!result.Succeeded)
            {
                asyncTextPending = false;
                Invalidate(Invalidation.Presence);
                return;
            }
            if (!Font.Equals(result.Font)
                || localisation.GetLocalisedString(requestedAsyncText) != result.Text)
            {
                refreshAsyncText();
                return;
            }
            asyncTextPending = false;
            Text = result.Text;
            Invalidate(Invalidation.Presence);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (IsDisposed)
                return;

            asyncTextPending = false;
            lock (asyncTextPublicationLock)
            {
                asyncTextCancellation?.Cancel();
                asyncTextCancellation?.Dispose();
                asyncTextCancellation = null;
            }
            skinComponentFamily?.UnbindAll();
            asyncLocalisation?.UnbindAll();
            separateFonts?.UnbindAll();
            foreach (var binding in fontBindings)
                binding.UnbindAll();
            base.Dispose(isDisposing);
        }

        internal void SetBaseFont(FontUsage font)
        {
            originalFamily = font.Family;
            Font = font;
            if (fontConfig != null)
                onFontChanged();
        }

        private void onFontChanged()
        {
            if (HasSkinComponentFont) return;
            string family = FontConfiguration.Capture(fontConfig).Resolve(originalFamily);
            Font = Font.With(family: family);
            RefreshAsyncTextFont();
        }

    }
}