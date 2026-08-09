// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Framework.Threading;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Screens.Edit;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.Edit
{
    /// <summary>
    /// Editor-only heatmap. Green cells are positions at which the player is never touched by a bullet;
    /// red cells are crossed by one or more bullet trajectories.
    /// </summary>
    public partial class DodgeCoverageOverlay : BufferedContainer
    {
        [Resolved]
        private DodgeEditorSettings settings { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private readonly CoverageGrid coverageGrid;
        private ScheduledDelegate? recalculation;
        private int? coverageStateHash;
        private CancellationTokenSource? calculationCancellation;

        public IReadOnlyList<int> HitCounts => coverageGrid.HitCounts;

        public bool IsCoverageVisible => Alpha > 0;

        public DodgeCoverageOverlay()
            : base(cachedFrameBuffer: true)
        {
            Size = DodgePlayfield.BASE_SIZE;
            Alpha = 0;
            BackgroundColour = new Colour4(1, 1, 1, 0);
            Child = coverageGrid = new CoverageGrid
            {
                RelativeSizeAxes = Axes.Both,
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            editorBeatmap.HitObjectAdded += onHitObjectChanged;
            editorBeatmap.HitObjectRemoved += onHitObjectChanged;
            editorBeatmap.HitObjectUpdated += onHitObjectChanged;
            editorBeatmap.BeatmapReprocessed += queueRecalculation;

            settings.ShowBulletCoverage.BindValueChanged(visibility =>
            {
                Alpha = visibility.NewValue ? 1 : 0;

                if (visibility.NewValue)
                    recalculate();
                else
                    calculationCancellation?.Cancel();
            }, true);
        }

        private void onHitObjectChanged(osu.Game.Rulesets.Objects.HitObject _) => queueRecalculation();

        private void queueRecalculation()
        {
            if (!settings.ShowBulletCoverage.Value)
                return;

            float playerSize = DodgeBeatmapSettings.GetPlayerSize(editorBeatmap.Difficulty);
            int currentStateHash = DodgeCoverageCalculator.CalculateStateHash(editorBeatmap.HitObjects.OfType<DodgeHitObject>(), playerSize);

            if (currentStateHash == coverageStateHash)
            {
                recalculation?.Cancel();
                recalculation = null;
                return;
            }

            calculationCancellation?.Cancel();
            recalculation?.Cancel();
            recalculation = Scheduler.AddDelayed(recalculate, 100);
        }

        private async void recalculate()
        {
            DodgeHitObject[] hitObjects = editorBeatmap.HitObjects.OfType<DodgeHitObject>().ToArray();
            float playerSize = DodgeBeatmapSettings.GetPlayerSize(editorBeatmap.Difficulty);
            int currentStateHash = DodgeCoverageCalculator.CalculateStateHash(hitObjects, playerSize);

            if (currentStateHash == coverageStateHash)
                return;

            calculationCancellation?.Cancel();

            var cancellation = calculationCancellation = new CancellationTokenSource();
            DodgeCoverageCalculator.CalculationInput input = DodgeCoverageCalculator.CreateCalculationInput(hitObjects, playerSize);

            int[] hitCounts;

            try
            {
                hitCounts = await Task.Run(
                    () => DodgeCoverageCalculator.CalculateHitCounts(input, cancellationToken: cancellation.Token),
                    cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                cancellation.Dispose();
                return;
            }

            if (cancellation.IsCancellationRequested || IsDisposed)
            {
                cancellation.Dispose();
                return;
            }

            Schedule(() =>
            {
                if (calculationCancellation != cancellation || cancellation.IsCancellationRequested)
                {
                    cancellation.Dispose();
                    return;
                }

                int latestStateHash = DodgeCoverageCalculator.CalculateStateHash(
                    editorBeatmap.HitObjects.OfType<DodgeHitObject>(),
                    DodgeBeatmapSettings.GetPlayerSize(editorBeatmap.Difficulty));

                if (latestStateHash != currentStateHash)
                {
                    cancellation.Dispose();
                    queueRecalculation();
                    return;
                }

                coverageGrid.HitCounts = hitCounts;
                coverageStateHash = currentStateHash;
                ForceRedraw();

                calculationCancellation = null;
                cancellation.Dispose();
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            recalculation?.Cancel();
            calculationCancellation?.Cancel();
            calculationCancellation = null;

            if (editorBeatmap != null)
            {
                editorBeatmap.HitObjectAdded -= onHitObjectChanged;
                editorBeatmap.HitObjectRemoved -= onHitObjectChanged;
                editorBeatmap.HitObjectUpdated -= onHitObjectChanged;
                editorBeatmap.BeatmapReprocessed -= queueRecalculation;
            }

            base.Dispose(isDisposing);
        }

        private partial class CoverageGrid : Drawable
        {
            private IShader shader = null!;
            private Texture texture = null!;
            private int[] hitCounts = Array.Empty<int>();

            public IShader Shader => shader;

            public Texture Texture => texture;

            public int[] HitCounts
            {
                get => hitCounts;
                set
                {
                    hitCounts = value;
                    Invalidate(Invalidation.DrawNode);
                }
            }

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer, ShaderManager shaders)
            {
                texture = renderer.WhitePixel;
                shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
            }

            protected override DrawNode CreateDrawNode() => new CoverageDrawNode(this);
        }

        private class CoverageDrawNode : DrawNode
        {
            private const int columns = DodgeCoverageCalculator.DEFAULT_COLUMNS;
            private const int rows = DodgeCoverageCalculator.DEFAULT_ROWS;

            public new CoverageGrid Source => (CoverageGrid)base.Source;

            private IShader shader = null!;
            private Texture texture = null!;
            private int[] hitCounts = Array.Empty<int>();
            private int maximumHitCount;
            private Vector2 drawSize;

            public CoverageDrawNode(CoverageGrid source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                shader = Source.Shader;
                texture = Source.Texture;
                hitCounts = (int[])Source.HitCounts.Clone();
                maximumHitCount = hitCounts.Length == 0 ? 0 : hitCounts.Max();
                drawSize = Source.DrawSize;
            }

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                if (hitCounts.Length != columns * rows)
                    return;

                shader.Bind();

                float cellWidth = drawSize.X / columns;
                float cellHeight = drawSize.Y / rows;

                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        int count = hitCounts[row * columns + column];
                        Colour4 colour;

                        if (count == 0)
                        {
                            colour = new Colour4(0.1f, 0.9f, 0.35f, 0.22f);
                        }
                        else
                        {
                            float intensity = maximumHitCount == 0 ? 0 : MathF.Sqrt((float)count / maximumHitCount);
                            colour = new Colour4(1, 0.12f, 0.08f, 0.09f + 0.23f * intensity);
                        }

                        Vector2 topLeft = new Vector2(column * cellWidth, row * cellHeight);
                        Vector2 bottomRight = topLeft + new Vector2(cellWidth, cellHeight);
                        var quad = new Quad(
                            Vector2Extensions.Transform(topLeft, DrawInfo.Matrix),
                            Vector2Extensions.Transform(new Vector2(bottomRight.X, topLeft.Y), DrawInfo.Matrix),
                            Vector2Extensions.Transform(new Vector2(topLeft.X, bottomRight.Y), DrawInfo.Matrix),
                            Vector2Extensions.Transform(bottomRight, DrawInfo.Matrix));

                        ColourInfo drawColour = DrawColourInfo.Colour;
                        drawColour.ApplyChild(colour);
                        renderer.DrawQuad(texture, quad, drawColour);
                    }
                }

                shader.Unbind();
            }
        }
    }
}
