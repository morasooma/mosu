using System;

namespace osu.Game.Database
{
    public sealed class DatabaseCompatibilityException : Exception
    {
        public ulong SupportedVersion { get; }

        public DatabaseCompatibilityException(ulong supportedVersion, Exception innerException)
            : base($"The database cannot be opened by this client (supported schema: {supportedVersion}). Update the client. The database has not been replaced.", innerException)
        {
            SupportedVersion = supportedVersion;
        }
    }
}
