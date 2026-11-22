// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Extensions;
using osu.Game.Online.Chat;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Chat
{
    public partial class ChatLine : CompositeDrawable
    {
        private Message message = null!;

        public Message Message
        {
            get => message;
            set
            {
                if (message == value) return;

                message = MessageFormatter.FormatMessage(value);

                if (!IsLoaded)
                    return;

                translatedText = null;
                sourceLanguage = translatedTarget = requestedTarget = null;
                showOriginal = false;
                updateMessageContent();
                requestTranslation();
            }
        }

        public IEnumerable<Drawable> DrawableContentFlow => drawableContentFlow.Children;

        private const float font_size = 13;

        protected virtual float Spacing => 15;

        protected virtual float UsernameWidth => 150;

        [Resolved]
        private Bindable<Channel?>? currentChannel { get; set; }

        [Resolved]
        private ChannelManager? channelManager { get; set; }

        [Resolved]
        private OverlayColourProvider? colourProvider { get; set; }

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        private OsuSpriteText drawableTimestamp = null!;

        private DrawableChatUsername drawableUsername = null!;

        private LinkFlowContainer drawableContentFlow = null!;

        [Resolved]
        private LocalisationManager localisation { get; set; } = null!;

        private readonly Bindable<bool> prefer24HourTime = new Bindable<bool>();
        private readonly Bindable<bool> translatorEnabled = new Bindable<bool>();
        private IBindable<Language>? targetLanguage;
        private string? translatedText;
        private string? sourceLanguage;
        private string? translatedTarget;
        private string? requestedTarget;
        private bool showOriginal;
        private static int activeTranslations;
        private const int max_active_translations = 4;
        private static readonly ConcurrentDictionary<(string Text, string Target), ChatTranslationResult> translationCache = new();

        private Container? highlight;

        private Drawable? background;

        private bool alternatingBackground;
        private bool requiresTimestamp = true;

        public bool RequiresTimestamp
        {
            get => requiresTimestamp;
            set
            {
                if (requiresTimestamp == value)
                    return;

                requiresTimestamp = value;

                if (!IsLoaded)
                    return;

                updateMessageContent();
            }
        }

        public bool AlternatingBackground
        {
            get => alternatingBackground;
            set
            {
                if (alternatingBackground == value)
                    return;

                alternatingBackground = value;
                updateBackground();
            }
        }

        private bool isMention;

        /// <summary>
        /// The colour used to paint the author's username.
        /// </summary>
        /// <remarks>
        /// The colour can be set explicitly by consumers via the property initialiser.
        /// If unspecified, the colour is by default initialised to:
        /// <list type="bullet">
        /// <item><see cref="APIUser.Colour">message.Sender.Colour</see>, if non-empty,</item>
        /// <item>a random colour from <see cref="default_username_colours"/> if the above is empty.</item>
        /// </list>
        /// </remarks>
        public Color4 UsernameColour { get; init; }

        public ChatLine(Message message)
        {
            Message = message;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            // initialise using sane defaults.
            // consumers can use the initialiser of `UsernameColour` to override this if they wish to.
            UsernameColour = !string.IsNullOrEmpty(message.Sender.Colour)
                ? Color4Extensions.FromHex(message.Sender.Colour)
                : default_username_colours[message.SenderId % default_username_colours.Length];
        }

        private IBindable<Colour4>? themeColour;

        internal Color4 TimestampColour => drawableTimestamp.Colour;
        internal IEnumerable<SpriteText> ContentSpriteTexts => drawableContentFlow.ChildrenOfType<SpriteText>();

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager configManager, OsuGameBase game)
        {
            configManager.BindWith(OsuSetting.Prefer24HourTime, prefer24HourTime);
            configManager.BindWith(OsuSetting.ForkChatTranslatorEnabled, translatorEnabled);
            targetLanguage = game.CurrentLanguage.GetBoundCopy();
            prefer24HourTime.BindValueChanged(_ => updateTimestamp());

            Padding = new MarginPadding { Right = 5 };

            InternalChildren = new[]
            {
                background = new Container
                {
                    Masking = true,
                    CornerRadius = 4,
                    Alpha = 0,
                    RelativeSizeAxes = Axes.Both,
                    Blending = BlendingParameters.Additive,
                    Child = new Box
                    {
                        Colour = Colour4.FromHex("#3b3234"),
                        RelativeSizeAxes = Axes.Both,
                    },
                },
                new GridContainer
                {
                    Padding = new MarginPadding
                    {
                        Horizontal = 2,
                        Vertical = 2,
                    },
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, 45),
                        new Dimension(GridSizeMode.Absolute, Spacing + UsernameWidth + Spacing),
                        new Dimension(),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            drawableTimestamp = new OsuSpriteText
                            {
                                Shadow = false,
                                Anchor = Anchor.TopLeft,
                                Origin = Anchor.TopLeft,
                                Spacing = new Vector2(-1, 0),
                                Font = OsuFont.GetFont(size: font_size, weight: FontWeight.SemiBold, fixedWidth: true),
                                AlwaysPresent = true,
                            },
                            drawableUsername = new DrawableChatUsername(message.Sender)
                            {
                                Width = UsernameWidth,
                                FontSize = font_size,
                                AutoSizeAxes = Axes.Y,
                                Origin = Anchor.TopRight,
                                Anchor = Anchor.TopRight,
                                Margin = new MarginPadding { Horizontal = Spacing },
                                AccentColour = UsernameColour,
                                Inverted = message.Sender.IsSupporter,
                            },
                            drawableContentFlow = new LinkFlowContainer(styleMessageContent)
                            {
                                AutoSizeAxes = Axes.Y,
                                RelativeSizeAxes = Axes.X,
                            }
                        },
                    }
                }
            };

            updateBackground();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            translatorEnabled.BindValueChanged(_ => { updateMessageContent(); requestTranslation(); });
            targetLanguage?.BindValueChanged(_ => { updateMessageContent(); requestTranslation(); });
            updateMessageContent();
            requestTranslation();

            if (colourProvider != null)
            {
                themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                themeColour.BindValueChanged(_ => updateColours(), true);
            }
            else
            {
                updateColours();
            }

            FinishTransforms(true);

            drawableUsername.ReportRequested = () => dialogOverlay?.Push(new ReportChatDialog(message)
            {
                Success = () =>
                {
                    Debug.Assert(currentChannel?.Value != null);

                    switch (currentChannel.Value.Type)
                    {
                        case ChannelType.PM:
                            currentChannel.Value.AddNewMessages(new InfoMessage(ChatStrings.ReportConfirmationPM));
                            break;

                        default:
                            currentChannel.Value.AddNewMessages(new InfoMessage(ChatStrings.ReportConfirmation));
                            break;
                    }
                }
            });
        }

        /// <summary>
        /// Performs a highlight animation on this <see cref="ChatLine"/>.
        /// </summary>
        public void Highlight()
        {
            if (highlight?.IsAlive != true)
            {
                AddInternal(highlight = new Container
                {
                    CornerRadius = 2f,
                    Masking = true,
                    RelativeSizeAxes = Axes.Both,
                    Colour = drawableUsername.AccentColour.Darken(1f),
                    Depth = float.MaxValue,
                    Child = new Box { RelativeSizeAxes = Axes.Both }
                });
            }

            highlight.FadeTo(0.5f).FadeOut(1500, Easing.InQuint);
            highlight.Expire();
        }

        private void styleMessageContent(SpriteText text)
        {
            text.Shadow = false;
            text.Font = OsuFont.Inter.With(size: font_size, italics: Message.IsAction, weight: isMention ? FontWeight.SemiBold : FontWeight.Regular);

            Color4 messageColour = colourProvider?.Content1 ?? Colour4.White;

            if (isMention)
                messageColour = colourProvider?.Highlight1 ?? Color4.Orange;
            else if (Message.IsAction && !string.IsNullOrEmpty(message.Sender.Colour))
                messageColour = Color4Extensions.FromHex(message.Sender.Colour);

            text.Colour = messageColour;
        }

        private void updateColours()
        {
            drawableTimestamp.Colour = colourProvider != null
                ? (OverlayColourProvider.IsLightTheme ? colourProvider.Content2 : colourProvider.Background1)
                : Colour4.White;

            Color4 messageColour = colourProvider?.Content1 ?? Colour4.White;

            if (isMention)
                messageColour = colourProvider?.Highlight1 ?? Color4.Orange;
            else if (Message.IsAction && !string.IsNullOrEmpty(message.Sender.Colour))
                messageColour = Color4Extensions.FromHex(message.Sender.Colour);

            var linkParts = new HashSet<Drawable>();
            foreach (var compiler in drawableContentFlow.ChildrenOfType<DrawableLinkCompiler>())
            {
                foreach (var part in compiler.Parts)
                    linkParts.Add(part);
            }

            foreach (var text in drawableContentFlow.ChildrenOfType<SpriteText>())
            {
                if (!linkParts.Contains(text))
                    text.Colour = messageColour;
            }
        }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private void updateMessageContent()
        {
            this.FadeTo(message is LocalEchoMessage ? 0.4f : 1.0f, 500, Easing.OutQuint);

            if (requiresTimestamp && !(message is LocalEchoMessage))
            {
                drawableTimestamp.Show();
                updateTimestamp();
            }
            else
            {
                drawableTimestamp.Hide();
            }

            drawableUsername.Text = $@"{message.Sender.Username}";

            // remove non-existent channels from the link list
            message.Links.RemoveAll(link => link.Action == LinkAction.OpenChannel && channelManager?.AvailableChannels.Any(c => c.Name == link.Argument.ToString()) != true);

            isMention = MessageNotifier.MatchUsername(message.DisplayContent, api.LocalUser.Value.Username).Success;

            drawableContentFlow.Clear();
            bool useTranslation = translatorEnabled.Value && !showOriginal && translatedText != null && translatedTarget == targetLanguage?.Value.ToCultureCode();

            if (useTranslation && !string.Equals(sourceLanguage, translatedTarget, System.StringComparison.OrdinalIgnoreCase))
            {
                drawableContentFlow.AddText(translatedText!);
                if (!string.IsNullOrWhiteSpace(sourceLanguage))
                    drawableContentFlow.AddLink($" [{sourceLanguage}]", () => { showOriginal = true; updateMessageContent(); }, localisation.GetLocalisedString(ForkSettingsStrings.ChatShowOriginal));
                else
                    drawableContentFlow.AddLink(ForkSettingsStrings.ChatOriginal, () => { showOriginal = true; updateMessageContent(); }, localisation.GetLocalisedString(ForkSettingsStrings.ChatShowOriginal));
            }
            else
            {
                drawableContentFlow.AddLinks(message.DisplayContent, message.Links);
                if (showOriginal && translatedText != null && translatorEnabled.Value && sourceLanguage != translatedTarget)
                    drawableContentFlow.AddLink(ForkSettingsStrings.ChatTranslation, () => { showOriginal = false; updateMessageContent(); }, localisation.GetLocalisedString(ForkSettingsStrings.ChatShowTranslation));
                else if (translatorEnabled.Value && !string.IsNullOrWhiteSpace(sourceLanguage))
                    drawableContentFlow.AddText($" [{sourceLanguage}]");
            }

            if (IsLoaded)
                updateColours();
        }

        private void requestTranslation()
        {
            if (!translatorEnabled.Value || targetLanguage == null || message is LocalMessage ||
                string.IsNullOrWhiteSpace(message.Content) || message.Content.Length > 2000 ||
                message.Content.StartsWith('/') || message.Content.StartsWith('!') ||
                message.Links?.Count > 0 || !IsPresent)
                return;

            string target = targetLanguage.Value.ToCultureCode();
            if (target == requestedTarget || target == translatedTarget)
                return;

            if (translationCache.TryGetValue((message.Content, target), out var cached))
            {
                translatedText = cached.TranslatedText;
                sourceLanguage = cached.SourceLanguage;
                translatedTarget = target;
                updateMessageContent();
                return;
            }

            if (Interlocked.Increment(ref activeTranslations) > max_active_translations)
            {
                Interlocked.Decrement(ref activeTranslations);
                Scheduler.AddDelayed(requestTranslation, 500);
                return;
            }

            requestedTarget = target;
            var currentMessage = message;
            _ = translateAsync(currentMessage, target);
        }

        private async System.Threading.Tasks.Task translateAsync(Message currentMessage, string target)
        {
            ChatTranslationResult? response = null;
            try
            {
                response = await ChatTranslator.TranslateAsync(currentMessage.Content, target).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref activeTranslations);
            }

            Schedule(() =>
            {
                if (message != currentMessage || requestedTarget != target)
                    return;

                if (response == null || !translatorEnabled.Value)
                {
                    requestedTarget = null;
                    return;
                }

                translatedText = response.TranslatedText;
                sourceLanguage = response.SourceLanguage;
                if (translationCache.Count > 512)
                    translationCache.Clear();
                translationCache.TryAdd((currentMessage.Content, target), response);
                translatedTarget = target;
                showOriginal = false;
                updateMessageContent();
            });
        }

        private void updateTimestamp()
        {
            drawableTimestamp.Text = message.Timestamp.LocalDateTime.ToLocalisableString(prefer24HourTime.Value ? @"HH:mm" : @"hh:mm tt");
        }

        private static readonly Color4[] default_username_colours =
        {
            Color4Extensions.FromHex("588c7e"),
            Color4Extensions.FromHex("b2a367"),
            Color4Extensions.FromHex("c98f65"),
            Color4Extensions.FromHex("bc5151"),
            Color4Extensions.FromHex("5c8bd6"),
            Color4Extensions.FromHex("7f6ab7"),
            Color4Extensions.FromHex("a368ad"),
            Color4Extensions.FromHex("aa6880"),

            Color4Extensions.FromHex("6fad9b"),
            Color4Extensions.FromHex("f2e394"),
            Color4Extensions.FromHex("f2ae72"),
            Color4Extensions.FromHex("f98f8a"),
            Color4Extensions.FromHex("7daef4"),
            Color4Extensions.FromHex("a691f2"),
            Color4Extensions.FromHex("c894d3"),
            Color4Extensions.FromHex("d895b0"),

            Color4Extensions.FromHex("53c4a1"),
            Color4Extensions.FromHex("eace5c"),
            Color4Extensions.FromHex("ea8c47"),
            Color4Extensions.FromHex("fc4f4f"),
            Color4Extensions.FromHex("3d94ea"),
            Color4Extensions.FromHex("7760ea"),
            Color4Extensions.FromHex("af52c6"),
            Color4Extensions.FromHex("e25696"),

            Color4Extensions.FromHex("677c66"),
            Color4Extensions.FromHex("9b8732"),
            Color4Extensions.FromHex("8c5129"),
            Color4Extensions.FromHex("8c3030"),
            Color4Extensions.FromHex("1f5d91"),
            Color4Extensions.FromHex("4335a5"),
            Color4Extensions.FromHex("812a96"),
            Color4Extensions.FromHex("992861"),
        };

        private void updateBackground()
        {
            background?.Alpha = alternatingBackground ? 0.2f : 0;
        }
    }
}
