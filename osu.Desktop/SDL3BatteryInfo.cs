// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using osu.Game.Utils;
using SDL;

namespace osu.Desktop
{
    internal unsafe class SDL3BatteryInfo : BatteryInfo
    {
        private static readonly object powerQueryLock = new object();

        public override double? ChargeLevel => readStatus().ChargeLevel;

        public override bool OnBattery => readStatus().OnBattery;

        // SDL_GetPowerInfo is not thread-safe. Serialize all access, including the legacy
        // getters, and perform the loader's synchronous system query away from Update.
        public override Task<(bool OnBattery, double? ChargeLevel)> GetStatusAsync() => Task.Run(readStatus);

        private static (bool OnBattery, double? ChargeLevel) readStatus()
        {
            lock (powerQueryLock)
            {
                int percentage;
                bool onBattery = SDL3.SDL_GetPowerInfo(null, &percentage) == SDL_PowerState.SDL_POWERSTATE_ON_BATTERY;
                return (onBattery, percentage == -1 ? null : percentage / 100.0);
            }
        }
    }
}
