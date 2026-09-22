// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Net
{
    /// <summary>
    /// Outcome of the initial world load.
    /// </summary>
    internal enum DodgeWorldAvailability
    {
        Loading,

        /// <summary>
        /// Dodge World needs an online session and there is none.
        /// </summary>
        LoginRequired,

        /// <summary>
        /// The world could not be reached.
        /// </summary>
        Unreachable,

        /// <summary>
        /// A world came back, but this client cannot read it.
        /// </summary>
        InvalidDocument,

        Ready,
    }

    internal enum PublishOutcome
    {
        Published,

        /// <summary>
        /// This session is not allowed to publish.
        /// </summary>
        Forbidden,

        /// <summary>
        /// Rejected or unreachable. For the server this usually means another editor moved the
        /// revision forward, so the world has to be reloaded before trying again.
        /// </summary>
        Failed,
    }

    /// <summary>
    /// An image being imported into the world.
    /// </summary>
    /// <param name="Content">Raw file bytes.</param>
    /// <param name="ContentType">MIME type, for a server upload.</param>
    /// <param name="Extension">Lowercased file extension, including the leading dot.</param>
    /// <param name="FileName">Original name, used for display only.</param>
    /// <param name="Purpose">
    /// What the image is for, used to name the file in local storage. Carries no meaning on the server.
    /// </param>
    internal readonly record struct TextureImport(
        byte[] Content,
        string ContentType,
        string Extension,
        string FileName,
        TextureImportPurpose Purpose);

    internal enum TextureImportPurpose
    {
        Surface,
        Weapon,
    }

    /// <summary>
    /// One warp, identified the way the server identifies it: by room and entity id.
    /// </summary>
    /// <remarks>
    /// Entity ids are only unique within a room, so the room is part of the identity.
    /// </remarks>
    internal readonly record struct WarpKey(string RoomId, string EntityId);

    /// <summary>
    /// What reaching a story point did.
    /// </summary>
    /// <param name="Applied">
    /// Whether this is the visit that moved the story on. False for a point already passed, and then
    /// nothing was paid — which is exactly why a story point is allowed to pay at all.
    /// </param>
    /// <param name="Experience">Experience awarded.</param>
    /// <param name="Coins">Coins awarded.</param>
    internal readonly record struct StoryOutcome(bool Applied, int Experience, int Coins)
    {
        public bool Paid => Applied && (Experience > 0 || Coins > 0);
    }

    internal enum WarpOutcome
    {
        /// <summary>The price was paid: the warp is now open, or the travel is allowed.</summary>
        Paid,

        /// <summary>
        /// The warp was already open, so nothing was charged. Not a failure — the player can use it.
        /// </summary>
        AlreadyUnlocked,

        /// <summary>The player cannot afford the price.</summary>
        NotEnoughCoins,

        /// <summary>Travel was refused because this warp was never opened.</summary>
        NotUnlocked,

        Failed,
    }
}
