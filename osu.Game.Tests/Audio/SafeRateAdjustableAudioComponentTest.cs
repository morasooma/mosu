// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Game.Audio;

namespace osu.Game.Tests.Audio
{
    public class SafeRateAdjustableAudioComponentTest
    {
        [Test]
        public void TestPlaybackRateAdjustmentsAreClamped()
        {
            var component = new TestAdjustableAudioComponent();
            var safeComponent = new SafeRateAdjustableAudioComponent(component);
            var source = new BindableDouble(100);

            safeComponent.AddAdjustment(AdjustableProperty.Frequency, source);

            Assert.That(component.AddedAdjustments, Has.Count.EqualTo(1));
            Assert.That(component.AddedAdjustments[0].Bindable, Is.Not.SameAs(source));
            Assert.That(component.AddedAdjustments[0].Bindable.Value, Is.EqualTo(50).Within(0.000001));

            source.Value = -25;
            Assert.That(component.AddedAdjustments[0].Bindable.Value, Is.EqualTo(0.05).Within(0.000001));

            source.Value = 37.5;
            Assert.That(component.AddedAdjustments[0].Bindable.Value, Is.EqualTo(37.5).Within(0.000001));
        }

        [Test]
        public void TestNonRateAdjustmentsPassThrough()
        {
            var component = new TestAdjustableAudioComponent();
            var safeComponent = new SafeRateAdjustableAudioComponent(component);
            var source = new BindableDouble(100);

            safeComponent.AddAdjustment(AdjustableProperty.Volume, source);

            Assert.That(component.AddedAdjustments, Has.Count.EqualTo(1));
            Assert.That(component.AddedAdjustments[0].Bindable, Is.SameAs(source));
        }

        [Test]
        public void TestRemoveAdjustmentUsesClampedBindable()
        {
            var component = new TestAdjustableAudioComponent();
            var safeComponent = new SafeRateAdjustableAudioComponent(component);
            var source = new BindableDouble(100);

            safeComponent.AddAdjustment(AdjustableProperty.Tempo, source);
            var addedBindable = component.AddedAdjustments[0].Bindable;

            safeComponent.RemoveAdjustment(AdjustableProperty.Tempo, source);

            Assert.That(component.RemovedAdjustments, Has.Count.EqualTo(1));
            Assert.That(component.RemovedAdjustments[0].Bindable, Is.SameAs(addedBindable));
        }

        private sealed class TestAdjustableAudioComponent : IAdjustableAudioComponent
        {
            public readonly List<(AdjustableProperty Property, IBindable<double> Bindable)> AddedAdjustments = new List<(AdjustableProperty Property, IBindable<double> Bindable)>();
            public readonly List<(AdjustableProperty Property, IBindable<double> Bindable)> RemovedAdjustments = new List<(AdjustableProperty Property, IBindable<double> Bindable)>();

            public BindableNumber<double> Volume { get; } = new BindableDouble(1);

            public BindableNumber<double> Balance { get; } = new BindableDouble();

            public BindableNumber<double> Frequency { get; } = new BindableDouble(1);

            public BindableNumber<double> Tempo { get; } = new BindableDouble(1);

            public IBindable<double> AggregateVolume => Volume;

            public IBindable<double> AggregateBalance => Balance;

            public IBindable<double> AggregateFrequency => Frequency;

            public IBindable<double> AggregateTempo => Tempo;

            public void BindAdjustments(IAggregateAudioAdjustment component)
            {
            }

            public void UnbindAdjustments(IAggregateAudioAdjustment component)
            {
            }

            public void AddAdjustment(AdjustableProperty type, IBindable<double> adjustBindable)
                => AddedAdjustments.Add((type, adjustBindable));

            public void RemoveAdjustment(AdjustableProperty type, IBindable<double> adjustBindable)
                => RemovedAdjustments.Add((type, adjustBindable));

            public void RemoveAllAdjustments(AdjustableProperty type)
            {
            }
        }
    }
}
