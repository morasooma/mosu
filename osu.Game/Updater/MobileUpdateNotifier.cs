// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using System.Threading.Tasks;
using osu.Game.Configuration;

namespace osu.Game.Updater
{
    /// <summary>
    /// Public source builds do not participate in the private Mosu release channel.
    /// </summary>
    public partial class MobileUpdateNotifier : UpdateManager
    {
        public override ReleaseStream? FixedReleaseStream => null;

        protected override Task<bool> PerformUpdateCheck(CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
