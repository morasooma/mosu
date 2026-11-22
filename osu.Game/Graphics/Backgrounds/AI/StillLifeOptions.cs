using System;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation;

namespace osu.Game.Graphics.Backgrounds.AI
{
    public enum StillLifeBackend
    {
        [LocalisableDescription(typeof(AiFunctionsStrings), nameof(AiFunctionsStrings.Cpu))]
        Cpu,
        [LocalisableDescription(typeof(AiFunctionsStrings), nameof(AiFunctionsStrings.Gpu))]
        Gpu,
    }

    public enum StillLifeResolution
    {
        [LocalisableDescription(typeof(AiFunctionsStrings), nameof(AiFunctionsStrings.Resolution256))]
        Low = 256,
        [LocalisableDescription(typeof(AiFunctionsStrings), nameof(AiFunctionsStrings.Resolution384))]
        Medium = 384,
        [LocalisableDescription(typeof(AiFunctionsStrings), nameof(AiFunctionsStrings.Resolution512))]
        High = 512,
    }

    internal sealed record StillLifeOptions(bool Enabled, StillLifeBackend Backend, StillLifeResolution Resolution, int Passes, int Steps, double Strength,
                                            int Seed, int Threads, bool CacheEnabled, int CacheMegabytes)
    {
        public static StillLifeOptions FromConfig(OsuConfigManager config) => new StillLifeOptions(
            config.Get<bool>(OsuSetting.ForkAiStillLife), config.Get<StillLifeBackend>(OsuSetting.ForkAiStillLifeBackend),
            config.Get<StillLifeResolution>(OsuSetting.ForkAiStillLifeSize), config.Get<int>(OsuSetting.ForkAiStillLifePasses),
            config.Get<int>(OsuSetting.ForkAiStillLifeSteps), config.Get<double>(OsuSetting.ForkAiStillLifeStrength),
            config.Get<int>(OsuSetting.ForkAiStillLifeSeed), config.Get<int>(OsuSetting.ForkAiStillLifeThreads),
            config.Get<bool>(OsuSetting.ForkAiCacheEnabled), config.Get<int>(OsuSetting.ForkAiCacheMegabytes));

        // Backend is part of the identity: CPU and GPU can round differently.
        public string CacheIdentity => FormattableString.Invariant($"{DirectBackgroundPackage.VERSION}:img2img-v2:{Backend}:{(int)Resolution}:{Passes}:{Steps}:{Strength:R}:{Seed}");
    }
}
