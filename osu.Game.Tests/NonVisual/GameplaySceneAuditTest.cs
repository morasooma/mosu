// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Screens.Play;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public partial class GameplaySceneAuditTest
    {
        /// <summary>
        /// The audit used to descend only into <see cref="Container"/>, which silently skipped every plain
        /// <see cref="CompositeDrawable"/> -- the category that most hit-object and skin components fall into.
        /// </summary>
        [Test]
        public void TestDescendsIntoNonContainerComposites()
        {
            var root = new AuditTestComposite(new Drawable[]
            {
                new Box(),
                new AuditTestComposite(new Drawable[] { new Box() }),
            });

            var counts = GameplaySceneAudit.CountDrawableTypes(root);

            Assert.That(count(counts, nameof(AuditTestComposite)), Is.EqualTo(2));
            Assert.That(count(counts, nameof(Box)), Is.EqualTo(2));
        }

        [Test]
        public void TestStillDescendsIntoContainers()
        {
            var root = new Container
            {
                Children = new Drawable[]
                {
                    new Box(),
                    new Container { Child = new Box() },
                }
            };

            var counts = GameplaySceneAudit.CountDrawableTypes(root);

            Assert.That(count(counts, nameof(Box)), Is.EqualTo(2));
        }

        /// <summary>
        /// Masking regions each force a <c>SetMasking</c> batch flush, so the audit reports them separately.
        /// </summary>
        [Test]
        public void TestCountsMaskingRegionsOnlyWhilePresent()
        {
            var root = new AuditTestComposite(new Drawable[]
            {
                new Container { Masking = true },
                new Container { Masking = true, Alpha = 0 },
                new Container { Masking = false },
            });

            var counts = GameplaySceneAudit.CountDrawableTypes(root);
            var containers = counts.Single(c => c.TypeName == nameof(Container));

            Assert.That(containers.TotalCount, Is.EqualTo(3));
            Assert.That(containers.PresentCount, Is.EqualTo(2));
            Assert.That(containers.MaskingCount, Is.EqualTo(1));
        }

        /// <summary>
        /// Drawables held in the traversal by <c>AlwaysPresent</c> while invisible pay full update and draw-node
        /// cost for nothing, so they are reported separately to make that cost findable.
        /// </summary>
        [Test]
        public void TestCountsInvisibleAlwaysPresentDrawables()
        {
            var root = new AuditTestComposite(new Drawable[]
            {
                new Box { Alpha = 0, AlwaysPresent = true },
                new Box { Alpha = 0 },
                new Box(),
            });

            var counts = GameplaySceneAudit.CountDrawableTypes(root);
            var boxes = counts.Single(c => c.TypeName == nameof(Box));

            Assert.That(boxes.TotalCount, Is.EqualTo(3));
            Assert.That(boxes.PresentCount, Is.EqualTo(2));
            Assert.That(boxes.AlwaysPresentInvisibleCount, Is.EqualTo(1));
        }

        [Test]
        public void TestCsvColumnsMatchRecordFieldCount()
        {
            // The logger writes the record's fields positionally under this header, so the two must stay in step.
            Assert.That(GameplaySceneAudit.CSV_COLUMNS.Split(',').Length, Is.EqualTo(6));
        }

        private static int count(System.Collections.Generic.List<GameplaySceneAudit.DrawableTypeCount> counts, string typeName)
            => counts.SingleOrDefault(c => c.TypeName == typeName).TotalCount;

        private partial class AuditTestComposite : CompositeDrawable
        {
            public AuditTestComposite(Drawable[] children)
            {
                InternalChildren = children;
            }
        }
    }
}
