// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Configuration;

namespace osu.Game.Updater
{
    /// <summary>
    /// Update manager for package-managed builds. Updates are supplied externally.
    /// </summary>
    public partial class NoActionUpdateManager : UpdateManager
    {
        public override ReleaseStream? FixedReleaseStream => externalReleaseStream;

        private static ReleaseStream? externalReleaseStream => Enum.TryParse(Environment.GetEnvironmentVariable("OSU_EXTERNAL_UPDATE_STREAM"), true, out ReleaseStream stream) ? stream : null;

        protected override Task<bool> PerformUpdateCheck(CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
