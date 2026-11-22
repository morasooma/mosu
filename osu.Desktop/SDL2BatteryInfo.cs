// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using osu.Game.Utils;

namespace osu.Desktop
{
    internal class SDL2BatteryInfo : BatteryInfo
    {
        private static readonly object powerQueryLock = new object();

        public override double? ChargeLevel => readStatus().ChargeLevel;

        public override bool OnBattery => readStatus().OnBattery;

        public override Task<(bool OnBattery, double? ChargeLevel)> GetStatusAsync() => Task.Run(readStatus);

        private static (bool OnBattery, double? ChargeLevel) readStatus()
        {
            lock (powerQueryLock)
            {
                bool onBattery = SDL2.SDL.SDL_GetPowerInfo(out _, out int percentage) == SDL2.SDL.SDL_PowerState.SDL_POWERSTATE_ON_BATTERY;
                return (onBattery, percentage == -1 ? null : percentage / 100.0);
            }
        }
    }
}
