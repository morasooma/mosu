// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using osu.Framework.Audio;
using osu.Framework.Bindables;

namespace osu.Game.Audio
{
    /// <summary>
    /// Wraps an audio component and clamps playback-rate adjustments to ranges tolerated by the audio backend.
    /// </summary>
    public sealed class SafeRateAdjustableAudioComponent : IAdjustableAudioComponent
    {
        // BASS_FX tempo/rate controls operate within 5%..5000% of the original playback rate.
        public const double MinSafePlaybackRate = 0.05;
        public const double MaxSafePlaybackRate = 50;

        private readonly IAdjustableAudioComponent component;
        private readonly Dictionary<AdjustmentKey, RateAdjustmentBinding> rateBindings = new(new AdjustmentKeyComparer());

        public SafeRateAdjustableAudioComponent(IAdjustableAudioComponent component)
        {
            this.component = component;
        }

        public BindableNumber<double> Volume => component.Volume;

        public BindableNumber<double> Balance => component.Balance;

        public BindableNumber<double> Frequency => component.Frequency;

        public BindableNumber<double> Tempo => component.Tempo;

        public IBindable<double> AggregateVolume => component.AggregateVolume;

        public IBindable<double> AggregateBalance => component.AggregateBalance;

        public IBindable<double> AggregateFrequency => component.AggregateFrequency;

        public IBindable<double> AggregateTempo => component.AggregateTempo;

        public void BindAdjustments(IAggregateAudioAdjustment component) => this.component.BindAdjustments(component);

        public void UnbindAdjustments(IAggregateAudioAdjustment component) => this.component.UnbindAdjustments(component);

        public void AddAdjustment(AdjustableProperty type, IBindable<double> adjustBindable)
        {
            component.AddAdjustment(type, requiresRateClamp(type) ? getOrCreateRateBinding(type, adjustBindable).Bindable : adjustBindable);
        }

        public void RemoveAdjustment(AdjustableProperty type, IBindable<double> adjustBindable)
        {
            if (!requiresRateClamp(type))
            {
                component.RemoveAdjustment(type, adjustBindable);
                return;
            }

            var key = new AdjustmentKey(type, adjustBindable);

            if (!rateBindings.Remove(key, out var binding))
            {
                component.RemoveAdjustment(type, adjustBindable);
                return;
            }

            component.RemoveAdjustment(type, binding.Bindable);
            binding.Dispose();
        }

        public void RemoveAllAdjustments(AdjustableProperty type)
        {
            component.RemoveAllAdjustments(type);

            if (!requiresRateClamp(type))
                return;

            var keysToRemove = new List<AdjustmentKey>();

            foreach (var (key, binding) in rateBindings)
            {
                if (key.Property != type)
                    continue;

                binding.Dispose();
                keysToRemove.Add(key);
            }

            foreach (var key in keysToRemove)
                rateBindings.Remove(key);
        }

        private RateAdjustmentBinding getOrCreateRateBinding(AdjustableProperty type, IBindable<double> source)
        {
            var key = new AdjustmentKey(type, source);

            if (rateBindings.TryGetValue(key, out var existing))
                return existing;

            var binding = new RateAdjustmentBinding(source);
            rateBindings.Add(key, binding);
            return binding;
        }

        private static bool requiresRateClamp(AdjustableProperty type)
            => type is AdjustableProperty.Frequency or AdjustableProperty.Tempo;

        private static double clampRate(double value)
        {
            if (double.IsNaN(value))
                return 1;

            if (double.IsPositiveInfinity(value))
                return MaxSafePlaybackRate;

            if (double.IsNegativeInfinity(value) || value <= 0)
                return MinSafePlaybackRate;

            return Math.Clamp(value, MinSafePlaybackRate, MaxSafePlaybackRate);
        }

        private readonly record struct AdjustmentKey(AdjustableProperty Property, IBindable<double> Source);

        private sealed class AdjustmentKeyComparer : IEqualityComparer<AdjustmentKey>
        {
            public bool Equals(AdjustmentKey x, AdjustmentKey y)
                => x.Property == y.Property && ReferenceEquals(x.Source, y.Source);

            public int GetHashCode(AdjustmentKey obj)
                => HashCode.Combine(obj.Property, RuntimeHelpers.GetHashCode(obj.Source));
        }

        private sealed class RateAdjustmentBinding : IDisposable
        {
            private readonly IBindable<double> source;

            public readonly BindableDouble Bindable = new BindableDouble();

            public RateAdjustmentBinding(IBindable<double> source)
            {
                this.source = source.GetBoundCopy();
                this.source.BindValueChanged(value => Bindable.Value = clampRate(value.NewValue), true);
            }

            public void Dispose()
            {
                source.UnbindAll();
            }
        }
    }
}
