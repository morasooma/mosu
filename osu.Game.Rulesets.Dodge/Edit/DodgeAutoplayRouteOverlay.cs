// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Threading;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Replays;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.Edit
{
    /// <summary>
    /// Editor-only visualisation of the deterministic route selected by Dodge autoplay.
    /// Cyan is the planned route; red markers identify fallback samples at impossible sections.
    /// </summary>
    public partial class DodgeAutoplayRouteOverlay : CompositeDrawable
    {
        [Resolved]
        private DodgeEditorSettings settings { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private const double future_window = 4000;
        private const double time_marker_interval = 1000;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        private readonly Path routePath;
        private readonly Container fallbackMarkers;
        private readonly Container timeMarkers;
        private readonly Container currentPositionMarker;
        private ScheduledDelegate? recalculation;
        private CancellationTokenSource? calculationCancellation;
        private int stateRevision;
        private int? requestedStateHash;
        private DodgeAutoplayRoutePoint[] fullRoute = Array.Empty<DodgeAutoplayRoutePoint>();
        private double lastDisplayedTime = double.MinValue;

        public bool IsRouteVisible => Alpha > 0;

        public int RoutePointCount { get; private set; }

        public int VisibleRoutePointCount { get; private set; }

        public int SnapshotObjectCount { get; private set; }

        public int GeneratedRoutePointCount { get; private set; }

        public string? LastCalculationError { get; private set; }

        public DodgeAutoplayRouteOverlay()
        {
            Size = DodgePlayfield.BASE_SIZE;
            Alpha = 0;
            InternalChildren = new Drawable[]
            {
                routePath = new Path
                {
                    AutoSizeAxes = Axes.None,
                    Size = DodgePlayfield.BASE_SIZE,
                    PathRadius = 1.5f,
                    Colour = new Color4(0.1f, 0.85f, 1, 0.82f),
                },
                fallbackMarkers = new Container
                {
                    Size = DodgePlayfield.BASE_SIZE,
                },
                timeMarkers = new Container
                {
                    Size = DodgePlayfield.BASE_SIZE,
                },
                currentPositionMarker = new Container
                {
                    Origin = Anchor.Centre,
                    Size = new Vector2(14),
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(12),
                            Colour = new Color4(0.1f, 0.85f, 1, 0.9f),
                        },
                        new Box
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(7),
                            Colour = Color4.White,
                        },
                    },
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            editorBeatmap.HitObjectAdded += onHitObjectChanged;
            editorBeatmap.HitObjectRemoved += onHitObjectChanged;
            editorBeatmap.HitObjectUpdated += onHitObjectChanged;
            editorBeatmap.BeatmapReprocessed += onBeatmapChanged;

            settings.ShowAutoplayRoute.BindValueChanged(visibility =>
            {
                Alpha = visibility.NewValue ? 1 : 0;

                if (visibility.NewValue)
                    queueRecalculation(true);
                else
                {
                    cancelCalculation();
                    requestedStateHash = null;
                }
            }, true);
        }

        private void onHitObjectChanged(HitObject _) => onBeatmapChanged();

        private void onBeatmapChanged()
        {
            queueRecalculation();
        }

        private void queueRecalculation(bool immediate = false)
        {
            if (!settings.ShowAutoplayRoute.Value)
                return;

            int currentStateHash = calculateStateHash();

            if (requestedStateHash == currentStateHash)
                return;

            requestedStateHash = currentStateHash;
            stateRevision++;
            cancelCalculation();
            recalculation?.Cancel();

            if (immediate)
            {
                recalculation = null;
                recalculate();
            }
            else
            {
                recalculation = Scheduler.AddDelayed(recalculate, 250);
            }
        }

        private async void recalculate()
        {
            int revision = stateRevision;
            var snapshot = DodgeBeatmapSnapshot.Create(editorBeatmap);
            SnapshotObjectCount = snapshot.HitObjects.Count;
            LastCalculationError = null;

            cancelCalculation();
            var cancellation = calculationCancellation = new CancellationTokenSource();

            DodgeAutoplayRoutePoint[] route;

            try
            {
                route = await Task.Run(() =>
                {
                    var generator = new DodgeAutoGenerator(snapshot, cancellation.Token);
                    generator.Generate();
                    return generator.Route.ToArray();
                }, cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                releaseCalculation(cancellation);
                return;
            }
            catch (Exception exception)
            {
                Schedule(() => LastCalculationError = exception.ToString());
                releaseCalculation(cancellation);
                return;
            }

            GeneratedRoutePointCount = route.Length;

            if (cancellation.IsCancellationRequested || IsDisposed)
            {
                releaseCalculation(cancellation);
                return;
            }

            Schedule(() =>
            {
                if (calculationCancellation != cancellation || cancellation.IsCancellationRequested || revision != stateRevision)
                {
                    releaseCalculation(cancellation);

                    if (revision != stateRevision)
                        queueRecalculation();

                    return;
                }

                applyRoute(route);
                releaseCalculation(cancellation);
            });
        }

        private void cancelCalculation()
        {
            CancellationTokenSource? cancellation = Interlocked.Exchange(ref calculationCancellation, null);

            if (cancellation == null)
                return;

            cancellation.Cancel();
            cancellation.Dispose();
        }

        private void releaseCalculation(CancellationTokenSource cancellation)
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref calculationCancellation, null, cancellation), cancellation))
                cancellation.Dispose();
        }

        private void applyRoute(DodgeAutoplayRoutePoint[] route)
        {
            fullRoute = route;
            RoutePointCount = route.Length;
            lastDisplayedTime = double.MinValue;
            updateVisibleRoute();
        }

        protected override void Update()
        {
            base.Update();

            if (!settings.ShowAutoplayRoute.Value || fullRoute.Length == 0)
                return;

            if (Math.Abs(editorClock.CurrentTime - lastDisplayedTime) >= 16)
                updateVisibleRoute();
        }

        private void updateVisibleRoute()
        {
            double currentTime = editorClock.CurrentTime;
            double endTime = currentTime + future_window;
            lastDisplayedTime = currentTime;

            routePath.ClearVertices();
            fallbackMarkers.Clear();
            timeMarkers.Clear();

            if (fullRoute.Length == 0 || currentTime > fullRoute[^1].Time || endTime < fullRoute[0].Time)
            {
                VisibleRoutePointCount = 0;
                currentPositionMarker.Alpha = 0;
                return;
            }

            double visibleStartTime = Math.Max(currentTime, fullRoute[0].Time);
            Vector2 currentPosition = positionAt(fullRoute, visibleStartTime);
            routePath.AddVertex(currentPosition);
            int visiblePoints = 1;

            foreach (DodgeAutoplayRoutePoint point in fullRoute)
            {
                if (point.Time <= visibleStartTime || point.Time > endTime)
                    continue;

                routePath.AddVertex(point.Position);
                visiblePoints++;

                if (!point.UsedFallback)
                    continue;

                fallbackMarkers.Add(new Circle
                {
                    Origin = Anchor.Centre,
                    Position = point.Position,
                    Size = new Vector2(7),
                    Colour = new Color4(1, 0.12f, 0.08f, 0.9f),
                });
            }

            VisibleRoutePointCount = visiblePoints;
            currentPositionMarker.Position = currentPosition;
            currentPositionMarker.Alpha = 1;

            for (double markerTime = Math.Ceiling(visibleStartTime / time_marker_interval) * time_marker_interval;
                 markerTime <= Math.Min(endTime, fullRoute[^1].Time);
                 markerTime += time_marker_interval)
            {
                float progress = (float)Math.Clamp((markerTime - currentTime) / future_window, 0, 1);
                timeMarkers.Add(new Circle
                {
                    Origin = Anchor.Centre,
                    Position = positionAt(fullRoute, markerTime),
                    Size = new Vector2(5 - progress * 2),
                    Colour = new Color4(0.65f, 0.95f, 1, 0.75f - progress * 0.35f),
                });
            }
        }

        private static Vector2 positionAt(DodgeAutoplayRoutePoint[] route, double time)
        {
            if (time <= route[0].Time)
                return route[0].Position;

            for (int i = 1; i < route.Length; i++)
            {
                if (time > route[i].Time)
                    continue;

                DodgeAutoplayRoutePoint previous = route[i - 1];
                DodgeAutoplayRoutePoint next = route[i];
                float progress = (float)((time - previous.Time) / Math.Max(0.001, next.Time - previous.Time));
                return Vector2.Lerp(previous.Position, next.Position, progress);
            }

            return route[^1].Position;
        }

        private int calculateStateHash()
        {
            var hash = new HashCode();
            hash.Add(editorBeatmap.Difficulty.ApproachRate);
            hash.Add(editorBeatmap.Difficulty.CircleSize);
            hash.Add(editorBeatmap.Difficulty.OverallDifficulty);
            hash.Add(DodgeBeatmapSettings.GetPlayerSize(editorBeatmap.Difficulty));

            foreach (DodgeHitObject hitObject in editorBeatmap.HitObjects.OfType<DodgeHitObject>())
            {
                hash.Add(hitObject.StartTime);
                hash.Add(hitObject.GetEndTime());

                switch (hitObject)
                {
                    case DodgeBullet bullet:
                        hash.Add(bullet.Position);
                        hash.Add(bullet.EndPosition);
                        hash.Add(bullet.ContinueUntilExit);
                        hash.Add(bullet.MovementType);
                        hash.Add(bullet.WaveAmplitude);
                        hash.Add(bullet.WaveCycles);
                        hash.Add(bullet.WavePhase);
                        break;

                    case DodgeEmitter emitter:
                        hash.Add(emitter.Position);
                        hash.Add(emitter.AimPosition);
                        hash.Add(emitter.MovementEndPosition);
                        hash.Add(emitter.BulletCount);
                        hash.Add(emitter.SpreadAngle);
                        hash.Add(emitter.BurstCount);
                        hash.Add(emitter.BurstInterval);
                        hash.Add(emitter.BurstBeatDivisor);
                        hash.Add(emitter.MoveSource);
                        hash.Add(emitter.ContinueUntilExit);
                        hash.Add(emitter.MovementType);
                        hash.Add(emitter.WaveAmplitude);
                        hash.Add(emitter.WaveCycles);
                        hash.Add(emitter.WavePhase);
                        break;

                    case DodgeArenaChange arena:
                        hash.Add(arena.TargetPosition);
                        hash.Add(arena.TargetSize);
                        break;
                }
            }

            return hash.ToHashCode();
        }

        protected override void Dispose(bool isDisposing)
        {
            recalculation?.Cancel();
            cancelCalculation();

            if (editorBeatmap != null)
            {
                editorBeatmap.HitObjectAdded -= onHitObjectChanged;
                editorBeatmap.HitObjectRemoved -= onHitObjectChanged;
                editorBeatmap.HitObjectUpdated -= onHitObjectChanged;
                editorBeatmap.BeatmapReprocessed -= onBeatmapChanged;
            }

            base.Dispose(isDisposing);
        }
    }
}
