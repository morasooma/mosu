// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenTabletDriver;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Platform.Pointer;
using OpenTabletDriver.Plugin.Tablet;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Input.StateChanges;
using osu.Framework.Platform;
using osu.Framework.Statistics;
using osuTK;

namespace osu.Framework.Input.Handlers.Tablet
{
    public class OpenTabletDriverHandler : InputHandler, IAbsolutePointer, IRelativePointer, IPressureHandler, ITabletHandler
    {
        private static readonly GlobalStatistic<ulong> statistic_total_events = GlobalStatistics.Get<ulong>(StatisticGroupFor<OpenTabletDriverHandler>(), "Total events");

        private TabletDriver? tabletDriver;

        private InputDeviceTree? device;

        private AbsoluteOutputMode outputMode = null!;

        private GameHost host = null!;

        public override string Description => "Tablet";

        public override bool IsActive => tabletDriver != null;

        public Bindable<Vector2> AreaOffset { get; } = new Bindable<Vector2>();

        public Bindable<Vector2> AreaSize { get; } = new Bindable<Vector2>();

        public Bindable<Vector2> OutputAreaOffset { get; } = new Bindable<Vector2>(new Vector2(0.5f, 0.5f));

        public Bindable<Vector2> OutputAreaSize { get; } = new Bindable<Vector2>(new Vector2(1f, 1f));

        public Bindable<float> Rotation { get; } = new Bindable<float>();

        public BindableFloat PressureThreshold { get; } = new BindableFloat
        {
            MinValue = 0f,
            MaxValue = 1f,
            Precision = 0.005f,
        };

        public BindableFloat ReconstructorWeight { get; } = new BindableFloat(1.0f)
        {
            MinValue = 0.01f,
            MaxValue = 1.0f,
            Precision = 0.01f,
        };

        public BindableBool ChatterExterminatorEnabled { get; } = new BindableBool();

        public BindableFloat ChatterExterminationStrength { get; } = new BindableFloat(2.0f)
        {
            MinValue = 0.01f,
            MaxValue = 25.0f,
            Precision = 0.1f,
        };

        public BindableBool RadialFollowEnabled { get; } = new BindableBool();

        public BindableFloat RadialFollowOuterRadius { get; } = new BindableFloat(5.0f)
        {
            MinValue = 0.0f,
            MaxValue = 100.0f,
            Precision = 0.1f,
        };

        public BindableFloat RadialFollowInnerRadius { get; } = new BindableFloat(0.0f)
        {
            MinValue = 0.0f,
            MaxValue = 100.0f,
            Precision = 0.1f,
        };

        public BindableFloat RadialFollowSmoothingCoefficient { get; } = new BindableFloat(0.95f)
        {
            MinValue = 0.0001f,
            MaxValue = 1.0f,
            Precision = 0.001f,
        };

        public BindableFloat RadialFollowSoftKneeScale { get; } = new BindableFloat(1.0f)
        {
            MinValue = 0.0f,
            MaxValue = 100.0f,
            Precision = 0.1f,
        };

        public BindableFloat RadialFollowSmoothingLeakCoefficient { get; } = new BindableFloat(0.0f)
        {
            MinValue = 0.0f,
            MaxValue = 1.0f,
            Precision = 0.01f,
        };

        public BindableBool BezierInterpolatorEnabled { get; } = new BindableBool();

        public BindableFloat BezierSmoothingFactor { get; } = new BindableFloat(1.0f)
        {
            MinValue = 0.01f,
            MaxValue = 1.0f,
            Precision = 0.01f,
        };

        public IBindable<TabletInfo?> Tablet => tablet;

        private readonly Bindable<TabletInfo?> tablet = new Bindable<TabletInfo?>();

        private Task? lastInitTask;

        private IBindable<bool> windowActive = null!;

        public override bool Initialize(GameHost host)
        {
            this.host = host;

            outputMode = new AbsoluteTabletMode(this);

            host.Window.Resized += () => updateOutputArea(host.Window);
            windowActive = host.Window.IsActive.GetBoundCopy();

            AreaOffset.BindValueChanged(_ => updateTabletAndInputArea(device));
            AreaSize.BindValueChanged(_ => updateTabletAndInputArea(device));
            Rotation.BindValueChanged(_ => updateTabletAndInputArea(device), true);

            OutputAreaOffset.BindValueChanged(_ => updateOutputArea(host.Window));
            OutputAreaSize.BindValueChanged(_ => updateOutputArea(host.Window), true);

            Enabled.BindValueChanged(enabled =>
            {
                if (enabled.NewValue)
                {
                    lastInitTask = Task.Run(() =>
                    {
                        tabletDriver = TabletDriver.Create();
                        tabletDriver.PostLog = Log;
                        tabletDriver.TabletsChanged += handleTabletsChanged;
                        tabletDriver.DeviceReported += handleDeviceReported;
                        tabletDriver.Detect();
                    });
                }
                else
                {
                    lastInitTask?.WaitSafely();

                    if (tabletDriver != null)
                    {
                        tabletDriver.DeviceReported -= handleDeviceReported;
                        tabletDriver.TabletsChanged -= handleTabletsChanged;
                        tabletDriver.Dispose();
                        tabletDriver = null;
                    }
                }
            }, true);

            return true;
        }

        private TabletPenDeviceType lastTabletDeviceType = TabletPenDeviceType.Unknown;
        private Vector2? lastTabletReportedPosition;
        private readonly ChatterExterminatorFilter chatterExterminatorFilter = new ChatterExterminatorFilter();
        private readonly RadialFollowFilter radialFollowFilter = new RadialFollowFilter();
        private readonly BezierInterpolatorFilter bezierInterpolatorFilter = new BezierInterpolatorFilter();
        private bool[] lastPenButtonStates = Array.Empty<bool>();
        private bool[] lastAuxiliaryButtonStates = Array.Empty<bool>();

        void IAbsolutePointer.SetPosition(System.Numerics.Vector2 pos)
        {
            lastTabletDeviceType = TabletPenDeviceType.Unknown;
            Vector2 currentTabletPos = new Vector2(pos.X, pos.Y);
            Vector2 reconstructedPos = currentTabletPos;

            float weight = ReconstructorWeight.Value;

            if (weight < 1.0f && lastTabletReportedPosition.HasValue)
            {
                reconstructedPos = (currentTabletPos - lastTabletReportedPosition.Value * (1 - weight)) / weight;
            }

            lastTabletReportedPosition = currentTabletPos;

            if (ChatterExterminatorEnabled.Value)
                reconstructedPos = chatterExterminatorFilter.Filter(reconstructedPos, ChatterExterminationStrength.Value);
            else
                chatterExterminatorFilter.Reset();

            if (RadialFollowEnabled.Value)
            {
                radialFollowFilter.Configure(
                    RadialFollowOuterRadius.Value,
                    RadialFollowInnerRadius.Value,
                    RadialFollowSmoothingCoefficient.Value,
                    RadialFollowSoftKneeScale.Value,
                    RadialFollowSmoothingLeakCoefficient.Value);

                reconstructedPos = radialFollowFilter.Filter(reconstructedPos);
            }
            else
                radialFollowFilter.Reset();

            if (BezierInterpolatorEnabled.Value)
                reconstructedPos = bezierInterpolatorFilter.Filter(reconstructedPos, BezierSmoothingFactor.Value);
            else
                bezierInterpolatorFilter.Reset();

            enqueueInput(new MousePositionAbsoluteInputFromPen { Position = reconstructedPos, DeviceType = lastTabletDeviceType });
        }

        void IRelativePointer.SetPosition(System.Numerics.Vector2 delta)
        {
            lastTabletDeviceType = TabletPenDeviceType.Indirect;
            enqueueInput(new MousePositionRelativeInputFromPen { Delta = new Vector2(delta.X, delta.Y), DeviceType = lastTabletDeviceType });
        }

        private bool penPressed;

        void IPressureHandler.SetPressure(float pressure)
        {
            // Most important for edge cases where users have pressure set to 0 or 1 and tablets can report fuzzy data.
            const float hysteresis_half = 0.02f;

            pressure = Math.Clamp(pressure, 0f, 1f);

            float releaseThreshold = PressureThreshold.Value - hysteresis_half;
            float pressThreshold = PressureThreshold.Value + hysteresis_half;

            // keep press..release threshold range constant for edge cases.
            if (releaseThreshold < 0f)
            {
                pressThreshold = hysteresis_half * 2;
                releaseThreshold = 0f;
            }
            else if (pressThreshold > 1f)
            {
                releaseThreshold = 1 - (hysteresis_half * 2);
                pressThreshold = 1f;
            }

            setPressed(penPressed
                ? pressure > releaseThreshold
                : pressure > pressThreshold);
        }

        private void handleTabletsChanged(object? sender, IEnumerable<TabletReference> tablets)
        {
            releaseTabletButtons();
            device = tablets.Any() ? tabletDriver?.InputDevices.First() : null;

            if (device != null)
            {
                // Important to reinitialise outputMode here as the previous one is likely disposed.
                device.OutputMode = outputMode = new AbsoluteTabletMode(this);
                outputMode.Tablet = device.CreateReference();

                updateTabletAndInputArea(device);
                updateOutputArea(host.Window);
            }
            else
            {
                // Ensure we don't leave the simulated mouse button pressed if the tablet disappears.
                setPressed(false);
                tablet.Value = null;
                lastTabletReportedPosition = null;
                chatterExterminatorFilter.Reset();
                radialFollowFilter.Reset();
                bezierInterpolatorFilter.Reset();
            }
        }

        private void setPressed(bool pressed)
        {
            // Importantly, only fire input when the state changes.
            // If we fire more often, this may intefere with users that click with mouse but use tablet for positional input (hovering).
            if (pressed == penPressed)
                return;

            enqueueInput(new MouseButtonInputFromPen(pressed) { DeviceType = lastTabletDeviceType });
            penPressed = pressed;
        }

        private void handleDeviceReported(object? sender, IDeviceReport report)
        {
            if (report is ITabletReport tabletReport)
                handleTabletReport(tabletReport);

            if (report is IAuxReport auxiliaryReport)
                handleAuxiliaryReport(auxiliaryReport);
        }

        private void updateOutputArea(IWindow window)
        {
            if (device == null)
                return;

            switch (device.OutputMode)
            {
                case AbsoluteOutputMode absoluteOutputMode:
                {
                    Vector2 windowSize = new Vector2(window.ClientSize.Width, window.ClientSize.Height);
                    Vector2 scaledSize = windowSize * OutputAreaSize.Value;
                    Vector2 offsetFromCenter = (OutputAreaOffset.Value - new Vector2(0.5f, 0.5f)) * (windowSize - scaledSize);
                    Vector2 position = (windowSize / 2) + offsetFromCenter;

                    absoluteOutputMode.Output = new Area(scaledSize.X, scaledSize.Y, position.ToSystemNumerics(), 0);
                    break;
                }
            }
        }

        private void updateTabletAndInputArea(InputDeviceTree? inputDevice)
        {
            if (inputDevice == null)
                return;

            var digitizer = inputDevice.Properties.Specifications.Digitizer;
            float inputWidth = digitizer.Width;
            float inputHeight = digitizer.Height;

            AreaSize.Default = new Vector2(inputWidth, inputHeight);

            // if it's clear the user has not configured the area, take the full area from the tablet that was just found.
            if (AreaSize.Value == Vector2.Zero)
                AreaSize.SetDefault();

            AreaOffset.Default = new Vector2(inputWidth / 2, inputHeight / 2);

            // likewise with the position, use the centre point if it has not been configured.
            // it's safe to assume no user would set their centre point to 0,0 for now.
            if (AreaOffset.Value == Vector2.Zero)
                AreaOffset.SetDefault();

            tablet.Value = new TabletInfo(inputDevice.Properties.Name, AreaSize.Default);

            switch (inputDevice.OutputMode)
            {
                case AbsoluteOutputMode absoluteOutputMode:
                {
                    // Set input area in millimeters
                    absoluteOutputMode.Input = new Area(AreaSize.Value.X, AreaSize.Value.Y,
                        new System.Numerics.Vector2(AreaOffset.Value.X, AreaOffset.Value.Y), Rotation.Value);
                    break;
                }
            }
        }

        private void handleTabletReport(ITabletReport tabletReport)
        {
            var buttons = GetButtonChanges<TabletPenButton>(tabletReport.PenButtons, ref lastPenButtonStates);

            if (buttons.Length > 0)
                enqueueInput(new TabletPenButtonInput(buttons));
        }

        private void handleAuxiliaryReport(IAuxReport auxiliaryReport)
        {
            var buttons = GetButtonChanges<TabletAuxiliaryButton>(auxiliaryReport.AuxButtons, ref lastAuxiliaryButtonStates);

            if (buttons.Length > 0)
                enqueueInput(new TabletAuxiliaryButtonInput(buttons));
        }

        private void releaseTabletButtons()
        {
            var penButtons = GetButtonChanges<TabletPenButton>(Array.Empty<bool>(), ref lastPenButtonStates);
            var auxiliaryButtons = GetButtonChanges<TabletAuxiliaryButton>(Array.Empty<bool>(), ref lastAuxiliaryButtonStates);

            if (penButtons.Length > 0)
                enqueueInput(new TabletPenButtonInput(penButtons));

            if (auxiliaryButtons.Length > 0)
                enqueueInput(new TabletAuxiliaryButtonInput(auxiliaryButtons));
        }

        internal static ButtonInputEntry<TButton>[] GetButtonChanges<TButton>(bool[] currentStates, ref bool[] previousStates)
            where TButton : struct, Enum
        {
            int stateCount = Math.Max(currentStates.Length, previousStates.Length);
            int changeCount = 0;

            for (int i = 0; i < stateCount; i++)
            {
                bool current = i < currentStates.Length && currentStates[i];
                bool previous = i < previousStates.Length && previousStates[i];

                if (current != previous)
                    changeCount++;
            }

            if (changeCount == 0)
            {
                if (previousStates.Length != currentStates.Length)
                    Array.Resize(ref previousStates, currentStates.Length);

                return Array.Empty<ButtonInputEntry<TButton>>();
            }

            var changes = new ButtonInputEntry<TButton>[changeCount];
            int changeIndex = 0;

            for (int i = 0; i < stateCount; i++)
            {
                bool current = i < currentStates.Length && currentStates[i];
                bool previous = i < previousStates.Length && previousStates[i];

                if (current != previous)
                    changes[changeIndex++] = new ButtonInputEntry<TButton>((TButton)Enum.ToObject(typeof(TButton), i), current);
            }

            if (previousStates.Length != currentStates.Length)
                Array.Resize(ref previousStates, currentStates.Length);

            currentStates.CopyTo(previousStates, 0);
            return changes;
        }

        private void enqueueInput(IInput input)
        {
            if (!windowActive.Value)
                return;

            EnqueueInput(input);
            FrameStatistics.Increment(StatisticsCounterType.TabletEvents);
            statistic_total_events.Value++;
        }
    }
}
