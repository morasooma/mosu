// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Net
{
    /// <summary>
    /// The shared, server-hosted Dodge World.
    /// </summary>
    internal sealed class ServerDodgeWorldSession : DodgeWorldSession
    {
        /// <summary>
        /// Formats the server accepts for a texture upload.
        /// </summary>
        private static readonly string[] uploadable_extensions = { ".png", ".jpg", ".jpeg" };

        private const long maximum_upload_bytes = 8 * 1024 * 1024;

        private readonly IAPIProvider api;

        public ServerDodgeWorldSession(IAPIProvider api)
        {
            this.api = api;
        }

        public override void Load(Action<DodgeWorldDocument> onLoaded)
        {
            SetAvailability(DodgeWorldAvailability.Loading);
            SetCanEdit(false);

            if (!api.IsLoggedIn)
            {
                SetAvailability(DodgeWorldAvailability.LoginRequired);
                return;
            }

            var request = new GetDodgeWorldRequest();

            request.Success += response =>
            {
                DodgeWorldDocument? document;

                try
                {
                    document = response.World.ToObject<DodgeWorldDocument>();
                }
                catch (Exception)
                {
                    document = null;
                }

                if (document?.IsSupported != true)
                {
                    SetAvailability(DodgeWorldAvailability.InvalidDocument);
                    return;
                }

                SetRevision(response.Revision);
                SetCanEdit(response.CanEdit);
                SetOnlineUsers(Math.Max(0, response.OnlineUsers));
                SetProgression(response.Progression);
                SetUnlockedWarps(read(response.UnlockedWarps));
                SetFlags(response.Flags);
                SetAvailability(DodgeWorldAvailability.Ready);
                onLoaded(document);
            };

            request.Failure += _ => SetAvailability(DodgeWorldAvailability.Unreachable);

            api.Queue(request);
        }

        public override void Publish(DodgeWorldDocument document, Action<PublishOutcome> onCompleted)
        {
            // A revision of zero means nothing was ever loaded, so there is no baseline to publish against.
            if (!CanEdit.Value || Revision.Value < 1)
            {
                onCompleted(PublishOutcome.Forbidden);
                return;
            }

            var request = new ReplaceDodgeWorldRequest(Revision.Value, DodgeWorldSerializer.Serialize(document));

            request.Success += response =>
            {
                SetRevision(response.Revision);
                SetCanEdit(response.CanEdit);
                SetOnlineUsers(Math.Max(0, response.OnlineUsers));
                SetProgression(response.Progression);
                onCompleted(PublishOutcome.Published);
            };

            request.Failure += _ => onCompleted(PublishOutcome.Failed);

            api.Queue(request);
        }

        public override void StoreTexture(TextureImport upload, Action<string> onStored, Action onFailed)
        {
            if (!CanEdit.Value || Array.IndexOf(uploadable_extensions, upload.Extension) < 0
                                || upload.Content.Length <= 0 || upload.Content.Length > maximum_upload_bytes)
            {
                onFailed();
                return;
            }

            var request = new UploadDodgeWorldAssetRequest(upload.Content, upload.ContentType);

            request.Success += response => onStored(response.Url);
            request.Failure += _ => onFailed();

            api.Queue(request);
        }

        /// <summary>
        /// Tells the server the player reached a story point. The declared flag and value are ignored here:
        /// the server reads them from the published world, so this client cannot write its own story.
        /// </summary>
        public override void RaiseStoryFlag(string roomId, string entityId, string flag, int value,
                                            Action<StoryOutcome>? onCompleted = null)
        {
            if (!api.IsLoggedIn || Availability.Value != DodgeWorldAvailability.Ready)
                return;

            var request = new RaiseDodgeWorldStoryFlagRequest(roomId, entityId);

            // A failure is left alone on purpose: the flag stays unset, so the story point is simply not
            // passed yet and can be passed again. Pretending locally would show progress the server does
            // not have.
            request.Success += response =>
            {
                SetFlags(response.Flags);

                // The reward, if the world put one on this point, arrives already applied: the amounts came
                // from the published document and the payment happened in the same transaction as the flag.
                if (response.Progression != null)
                    SetProgression(response.Progression);

                onCompleted?.Invoke(new StoryOutcome(response.Applied, response.ExperienceAwarded, response.CoinsAwarded));
            };

            api.Queue(request);
        }

        public override void UnlockWarp(string roomId, string entityId, Action<WarpOutcome> onCompleted) =>
            payForWarp(roomId, entityId, unlocking: true, onCompleted);

        public override void TravelToWarp(string roomId, string entityId, Action<WarpOutcome> onCompleted) =>
            payForWarp(roomId, entityId, unlocking: false, onCompleted);

        /// <summary>
        /// Asks the server to charge a warp. The price is not sent: the server reads it from the
        /// published world, so a client cannot name its own.
        /// </summary>
        private void payForWarp(string roomId, string entityId, bool unlocking, Action<WarpOutcome> onCompleted)
        {
            if (!api.IsLoggedIn || Availability.Value != DodgeWorldAvailability.Ready)
            {
                onCompleted(WarpOutcome.Failed);
                return;
            }

            var request = new DodgeWorldWarpRequest(roomId, entityId, unlocking);

            request.Success += response =>
            {
                if (response.Progression != null)
                    SetProgression(response.Progression);

                SetUnlockedWarps(read(response.UnlockedWarps));

                onCompleted(response.Reason switch
                {
                    "already_unlocked" => WarpOutcome.AlreadyUnlocked,
                    "not_enough_coins" => WarpOutcome.NotEnoughCoins,
                    "warp_not_unlocked" => WarpOutcome.NotUnlocked,
                    _ => response.Ok ? WarpOutcome.Paid : WarpOutcome.Failed,
                });
            };

            request.Failure += _ => onCompleted(WarpOutcome.Failed);

            api.Queue(request);
        }

        private static WarpKey[] read(DodgeWorldWarpReference[]? warps) => warps == null
            ? Array.Empty<WarpKey>()
            : warps.Select(warp => new WarpKey(warp.RoomId, warp.EntityId)).ToArray();
    }
}
