// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Allocation;
using osu.Game.Graphics;
using osu.Game.Overlays;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.BeatmapSet
{
    public abstract partial class MetadataSection : MetadataSection<string>
    {
        public override string Metadata
        {
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    HideMetadata();
                    return;
                }

                base.Metadata = value;
            }
        }

        protected MetadataSection(MetadataType type, Action<string>? searchAction = null)
            : base(type, searchAction)
        {
        }
    }

    public abstract partial class MetadataSection<T> : Container
    {
        private readonly FillFlowContainer textContainer;
        private TextFlowContainer? textFlow;
        private CancellationTokenSource? metadataCancellation;
        private IBindable<Colour4> themeColour = null!;

        protected readonly Action<T>? SearchAction;

        protected const float TRANSITION_DURATION = 250;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        protected MetadataSection(MetadataType type, Action<T>? searchAction = null)
        {
            SearchAction = searchAction;

            Alpha = 0;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = textContainer = new FillFlowContainer
            {
                Alpha = 0,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,

                Margin = new MarginPadding { Top = 15 },
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Child = new OsuSpriteText
                        {
                            Text = type.GetLocalisableDescription(),
                            Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 14),
                        },
                    },
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ =>
            {
                if (textFlow != null)
                    textFlow.Colour = (OverlayColourProvider.IsLightTheme ? (Color4)colourProvider.Content1 : Color4.White).Opacity(0.75f);
            }, true);
        }

        public virtual T Metadata
        {
            set
            {
                if (value == null)
                {
                    HideMetadata();
                    return;
                }

                this.FadeIn(TRANSITION_DURATION);

                setTextFlowAsync(value);
            }
        }

        protected void HideMetadata()
        {
            metadataCancellation?.Cancel();
            metadataCancellation?.Dispose();
            metadataCancellation = null;
            if (textFlow != null)
            {
                textContainer.Remove(textFlow, true);
                textFlow = null;
            }
            this.FadeOut(TRANSITION_DURATION);
        }

        private void setTextFlowAsync(T metadata)
        {
            metadataCancellation?.Cancel();
            metadataCancellation?.Dispose();
            metadataCancellation = new CancellationTokenSource();
            var token = metadataCancellation.Token;

            // Populate text parts in the flow's dependency loader, before its
            // LoadAsyncComplete creates/loads the word and link drawables.
            LoadComponentAsync(new PreparedMetadataFlow(flow => AddMetadata(metadata, flow))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Colour = (OverlayColourProvider.IsLightTheme && colourProvider != null ? (Color4)colourProvider.Content1 : Color4.White).Opacity(0.75f),
            }, loaded =>
            {
                if (token.IsCancellationRequested)
                    return;
                if (textFlow != null)
                    textContainer.Remove(textFlow, true);
                textContainer.Add(textFlow = loaded);

                // fade in if we haven't yet.
                textContainer.FadeIn(TRANSITION_DURATION);
            }, token);
        }

        protected override void Dispose(bool isDisposing)
        {
            metadataCancellation?.Cancel();
            metadataCancellation?.Dispose();
            base.Dispose(isDisposing);
        }

        private partial class PreparedMetadataFlow : LinkFlowContainer
        {
            private readonly Action<LinkFlowContainer> populate;

            public PreparedMetadataFlow(Action<LinkFlowContainer> populate)
                : base(s => s.Font = s.Font.With(size: 14))
            {
                this.populate = populate;
            }

            [BackgroundDependencyLoader]
            private void load(CancellationToken? cancellation)
            {
                cancellation?.ThrowIfCancellationRequested();
                populate(this);
            }
        }

        protected abstract void AddMetadata(T metadata, LinkFlowContainer loaded);
    }
}
