// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Input.Handlers.Tablet;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osuTK;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// The fork's custom tablet filters must live in mosu-tablet-filters.json, never in the
    /// framework's input.json, so a restore of another client's input.json can never wipe them.
    /// </summary>
    [TestFixture]
    public class TabletFilterSettingsStoreTest
    {
        [Test]
        public void TestAttachWithoutHandlerReturnsNullAndWritesNothing()
        {
            using var storage = new TemporaryNativeStorage($"tablet-filters-none-{Guid.NewGuid():N}");

            Assert.That(TabletFilterSettingsStore.AttachTo(storage, null), Is.Null);
            Assert.That(storage.Exists(TabletFilterSettingsStore.FILENAME), Is.False);
        }

        [Test]
        public void TestChangePersistsToForkOwnedFileOnly()
        {
            using var storage = new TemporaryNativeStorage($"tablet-filters-save-{Guid.NewGuid():N}");
            var handler = new TestTabletHandler();

            var store = TabletFilterSettingsStore.AttachTo(storage, handler);
            Assert.That(store, Is.Not.Null);

            handler.RadialFollowEnabled.Value = true;
            handler.RadialFollowOuterRadius.Value = 7.5f;
            handler.ChatterExterminationStrength.Value = 6f;

            Assert.That(storage.Exists(TabletFilterSettingsStore.FILENAME), Is.True);

            var saved = readSaved(storage);
            Assert.That(saved[nameof(handler.RadialFollowEnabled)], Is.EqualTo("True"));
            Assert.That(saved[nameof(handler.RadialFollowOuterRadius)], Is.EqualTo("7.5"));
            Assert.That(saved[nameof(handler.ChatterExterminationStrength)], Is.EqualTo("6"));
        }

        [Test]
        public void TestValuesSurviveRestartOnANewHandler()
        {
            using var storage = new TemporaryNativeStorage($"tablet-filters-restore-{Guid.NewGuid():N}");

            var first = new TestTabletHandler();
            TabletFilterSettingsStore.AttachTo(storage, first);
            first.RadialFollowEnabled.Value = true;
            first.RadialFollowSmoothingCoefficient.Value = 0.8f;
            first.BezierInterpolatorEnabled.Value = true;
            first.BezierSmoothingFactor.Value = 0.7f;

            // Simulate a restart: brand-new handler with framework defaults, same storage.
            var second = new TestTabletHandler();
            Assert.That(second.RadialFollowEnabled.Value, Is.False);

            TabletFilterSettingsStore.AttachTo(storage, second);

            Assert.That(second.RadialFollowEnabled.Value, Is.True);
            Assert.That(second.RadialFollowSmoothingCoefficient.Value, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(second.BezierInterpolatorEnabled.Value, Is.True);
            Assert.That(second.BezierSmoothingFactor.Value, Is.EqualTo(0.7f).Within(0.0001f));
        }

        [Test]
        public void TestCorruptFileFallsBackToDefaults()
        {
            using var storage = new TemporaryNativeStorage($"tablet-filters-corrupt-{Guid.NewGuid():N}");
            using (var stream = storage.CreateFileSafely(TabletFilterSettingsStore.FILENAME))
            using (var writer = new StreamWriter(stream))
                writer.Write("{ not valid json !!!");

            var handler = new TestTabletHandler();
            TabletFilterSettingsStore.AttachTo(storage, handler);

            // Defaults intact, and the corrupt file was replaced with a valid snapshot.
            Assert.That(handler.RadialFollowEnabled.Value, Is.False);
            Assert.That(handler.ChatterExterminationStrength.Value, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(() => readSaved(storage), Throws.Nothing);
        }

        [Test]
        public void TestInputJsonRestoreCannotTouchFilterFile()
        {
            using var storage = new TemporaryNativeStorage($"tablet-filters-isolation-{Guid.NewGuid():N}");

            var handler = new TestTabletHandler();
            TabletFilterSettingsStore.AttachTo(storage, handler);
            handler.RadialFollowEnabled.Value = true;
            handler.RadialFollowOuterRadius.Value = 9f;

            // Another client overwrites input.json (as a config restore would); the filter
            // file is fork-owned and untouched, and a fresh handler still loads the values.
            writeStorageFile(storage, "input.json", "{\"InputHandlers\":[]}");

            var restarted = new TestTabletHandler();
            TabletFilterSettingsStore.AttachTo(storage, restarted);

            Assert.That(restarted.RadialFollowEnabled.Value, Is.True);
            Assert.That(restarted.RadialFollowOuterRadius.Value, Is.EqualTo(9f).Within(0.0001f));
        }

        private static Dictionary<string, string> readSaved(TemporaryNativeStorage storage)
        {
            using var stream = storage.GetStream(TabletFilterSettingsStore.FILENAME, FileAccess.Read, FileMode.Open);
            using var reader = new StreamReader(stream);
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd())
                   ?? throw new InvalidOperationException("Filter snapshot is empty.");
        }

        private static void writeStorageFile(TemporaryNativeStorage storage, string filename, string contents)
        {
            using var stream = storage.CreateFileSafely(filename);
            using var writer = new StreamWriter(stream);
            writer.Write(contents);
        }

        /// <summary>
        /// Minimal ITabletHandler exposing only the filter bindables the store persists.
        /// </summary>
        private class TestTabletHandler : ITabletHandler
        {
            public Bindable<Vector2> AreaOffset { get; } = new Bindable<Vector2>();
            public Bindable<Vector2> AreaSize { get; } = new Bindable<Vector2>();
            public Bindable<Vector2> OutputAreaOffset { get; } = new Bindable<Vector2>(new Vector2(0.5f));
            public Bindable<Vector2> OutputAreaSize { get; } = new Bindable<Vector2>(Vector2.One);
            public IBindable<TabletInfo?> Tablet { get; } = new Bindable<TabletInfo?>();
            public Bindable<float> Rotation { get; } = new BindableFloat();
            public BindableFloat PressureThreshold { get; } = new BindableFloat();
            public BindableFloat ReconstructorWeight { get; } = new BindableFloat(1f);
            public BindableBool ChatterExterminatorEnabled { get; } = new BindableBool();
            public BindableFloat ChatterExterminationStrength { get; } = new BindableFloat(2f);
            public BindableBool RadialFollowEnabled { get; } = new BindableBool();
            public BindableFloat RadialFollowOuterRadius { get; } = new BindableFloat(5f);
            public BindableFloat RadialFollowInnerRadius { get; } = new BindableFloat();
            public BindableFloat RadialFollowSmoothingCoefficient { get; } = new BindableFloat(0.95f);
            public BindableFloat RadialFollowSoftKneeScale { get; } = new BindableFloat(1f);
            public BindableFloat RadialFollowSmoothingLeakCoefficient { get; } = new BindableFloat();
            public BindableBool BezierInterpolatorEnabled { get; } = new BindableBool();
            public BindableFloat BezierSmoothingFactor { get; } = new BindableFloat(1f);
            public BindableBool Enabled { get; } = new BindableBool(true);
        }
    }
}
