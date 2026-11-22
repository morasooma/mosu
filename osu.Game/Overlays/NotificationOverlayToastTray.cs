// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Utils;
using osu.Game.Overlays.Notifications;
using osuTK;

namespace osu.Game.Overlays
{
    /// <summary>
    /// A tray which attaches to the left of <see cref="NotificationOverlay"/> to show temporary toasts.
    /// </summary>
    public partial class NotificationOverlayToastTray : CompositeDrawable
    {
        public override bool IsPresent => toastContentBackground.Height > 0 || toastFlow.Count > 0;

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => toastFlow.ReceivePositionalInputAt(screenSpacePos);

        /// <summary>
        /// All notifications currently being displayed by the toast tray.
        /// </summary>
        public IEnumerable<Notification> Notifications => toastFlow.Concat(InternalChildren.OfType<Notification>());

        public bool IsDisplayingToasts => toastFlow.Count > 0;

        private FillFlowContainer<Notification> toastFlow = null!;
        private BufferedContainer toastContentBackground = null!;
        private IBindable<Colour4> themeColour = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public required Action<Notification> ForwardNotificationToPermanentStore { get; init; }

        public int UnreadCount => Notifications.Count(n => !n.WasClosed && !n.Read);

        private static readonly bool retainFrameBufferHeight = Environment.GetEnvironmentVariable("MOSU_QUICK_TOAST_EXACT_BUFFER") != "1";

        private int runningDepth;
        private readonly Dictionary<Notification, double> remainingLifetime = new Dictionary<Notification, double>();
        private readonly List<KeyValuePair<Notification, double>> lifetimeSnapshot = new List<KeyValuePair<Notification, double>>();

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Padding = new MarginPadding(20);

            InternalChildren = new Drawable[]
            {
                toastContentBackground = (new Box
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Colour = ColourInfo.GradientVertical(
                        colourProvider.Background6.Opacity(0.7f),
                        colourProvider.Background6.Opacity(0.5f)),
                    RelativeSizeAxes = Axes.Both,
                    Height = 0,
                }.WithEffect(new BlurEffect
                {
                    PadExtent = true,
                    Sigma = new Vector2(20),
                    RetainFrameBufferHeight = retainFrameBufferHeight,
                }).With(postEffectDrawable =>
                {
                    postEffectDrawable.Scale = new Vector2(1.5f, 1);
                    postEffectDrawable.Position += new Vector2(70, -50);
                    postEffectDrawable.AutoSizeAxes = Axes.None;
                    postEffectDrawable.RelativeSizeAxes = Axes.X;
                })),
                toastFlow = new FillFlowContainer<Notification>
                {
                    LayoutDuration = 150,
                    LayoutEasing = Easing.OutQuart,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                },
            };

            themeColour = colourProvider.GetColourBindable(OverlayColour.Background6);
            themeColour.BindValueChanged(_ => toastContentBackground.Colour = ColourInfo.GradientVertical(
                colourProvider.Background6.Opacity(0.7f),
                colourProvider.Background6.Opacity(0.5f)), true);
        }

        public void MarkAllRead() => Notifications.ForEach(n => n.Read = true);

        public void FlushAllToasts(bool force = false)
        {
            foreach (var notification in toastFlow.ToArray())
            {
                if (!force && notification.IsCritical)
                    continue;

                forwardNotification(notification);
            }
        }

        public void Post(Notification notification)
        {
            ++runningDepth;

            notification.ForwardToOverlay = () => forwardNotification(notification);

            int depth = notification.DisplayOnTop ? -runningDepth : runningDepth;

            toastFlow.Insert(depth, notification);

            remainingLifetime[notification] = notification.IsImportant ? 12000 : 2500;
            notification.ToastLifetimeProgress = 1;
        }

        private void forwardNotification(Notification notification)
        {
            if (!notification.IsInToastTray)
                return;

            remainingLifetime.Remove(notification);
            Debug.Assert(notification.Parent == toastFlow);

            // Temporarily remove from flow so we can animate the position off to the right.
            toastFlow.Remove(notification, false);
            AddInternal(notification);

            notification.MoveToOffset(new Vector2(400, 0), NotificationOverlay.TRANSITION_LENGTH, Easing.OutQuint);
            notification.FadeOut(NotificationOverlay.TRANSITION_LENGTH, Easing.OutQuint).OnComplete(_ =>
            {
                if (notification.Transient)
                {
                    notification.IsInToastTray = false;
                    notification.Close(false);
                    return;
                }

                RemoveInternal(notification, false);
                ForwardNotificationToPermanentStore(notification);

                notification.FadeIn(300, Easing.OutQuint);
            });
        }

        protected override void Update()
        {
            base.Update();

            // Dismissing a toast mutates the dictionary. Reuse the snapshot rather than
            // allocating an array on every Update while any toast is present.
            lifetimeSnapshot.AddRange(remainingLifetime);
            try
            {
                foreach (var (notification, remaining) in lifetimeSnapshot)
                {
                    if (notification.WasClosed || notification.Parent != toastFlow)
                    {
                        remainingLifetime.Remove(notification);
                        continue;
                    }

                    // Keep the bar and dismissal in sync, including while the toast is hovered or dragged.
                    if (notification.IsHovered || notification.IsDragged)
                        continue;

                    double duration = notification.IsImportant ? 12000 : 2500;
                    double next = Math.Max(0, remaining - Clock.ElapsedFrameTime);
                    remainingLifetime[notification] = next;
                    notification.ToastLifetimeProgress = (float)(next / duration);

                    if (next == 0)
                        forwardNotification(notification);
                }
            }
            finally
            {
                lifetimeSnapshot.Clear();
            }

            float height = 0;
            float alpha = 0;

            if (toastFlow.Count > 0)
            {
                float maxNotificationAlpha = 0;

                foreach (var t in toastFlow)
                {
                    if (t.Alpha > maxNotificationAlpha)
                        maxNotificationAlpha = t.Alpha;
                }

                height = toastFlow.DrawHeight + 120;
                alpha = Math.Clamp(toastFlow.DrawHeight / 41, 0, 1) * maxNotificationAlpha;
            }

            toastContentBackground.Height = (float)Interpolation.DampContinuously(toastContentBackground.Height, height, 10, Clock.ElapsedFrameTime);
            toastContentBackground.Alpha = (float)Interpolation.DampContinuously(toastContentBackground.Alpha, alpha, 10, Clock.ElapsedFrameTime);
        }
    }
}
