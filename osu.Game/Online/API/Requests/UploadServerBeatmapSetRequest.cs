// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using osu.Framework.IO.Network;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Screens.Edit.Submission;

namespace osu.Game.Online.API.Requests
{
    public class UploadServerBeatmapSetRequest : APIRequest<APIBeatmapSet>
    {
        private readonly byte[] beatmapArchive;
        private readonly string filename;

        protected override string Uri => BeatmapSetID == null
            ? $@"{API!.Endpoints.APIUrl}/api/private/beatmapsets/upload"
            : $@"{API!.Endpoints.APIUrl}/api/private/beatmapsets/{BeatmapSetID}/upload";

        protected override string Target => throw new NotSupportedException();

        public int? BeatmapSetID { get; }

        public int? BaseRevision { get; }

        public BeatmapSubmissionTarget? SubmissionTarget { get; }

        public int? SourceBeatmapSetID { get; }

        public int? SourceBeatmapID { get; }

        public UploadServerBeatmapSetRequest(byte[] beatmapArchive, string filename)
            : this(beatmapArchive, filename, null, null, null, null, null)
        {
        }

        private UploadServerBeatmapSetRequest(
            byte[] beatmapArchive,
            string filename,
            int? beatmapSetId,
            int? baseRevision,
            BeatmapSubmissionTarget? submissionTarget,
            int? sourceBeatmapSetId,
            int? sourceBeatmapId)
        {
            this.beatmapArchive = beatmapArchive;
            this.filename = filename;
            BeatmapSetID = beatmapSetId;
            BaseRevision = baseRevision;
            SubmissionTarget = submissionTarget;
            SourceBeatmapSetID = sourceBeatmapSetId;
            SourceBeatmapID = sourceBeatmapId;
        }

        public static UploadServerBeatmapSetRequest CreateDodge(
            byte[] beatmapArchive,
            string filename,
            BeatmapSubmissionTarget submissionTarget,
            int? sourceBeatmapSetId,
            int? sourceBeatmapId)
            => new UploadServerBeatmapSetRequest(
                beatmapArchive,
                filename,
                null,
                null,
                submissionTarget,
                sourceBeatmapSetId,
                sourceBeatmapId);

        public static UploadServerBeatmapSetRequest UpdateDodge(
            int beatmapSetId,
            int baseRevision,
            byte[] beatmapArchive,
            string filename,
            BeatmapSubmissionTarget submissionTarget,
            int? sourceBeatmapSetId,
            int? sourceBeatmapId)
            => new UploadServerBeatmapSetRequest(
                beatmapArchive,
                filename,
                beatmapSetId,
                baseRevision,
                submissionTarget,
                sourceBeatmapSetId,
                sourceBeatmapId);

        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.UploadProgress += onUploadProgress;
            request.Method = BeatmapSetID == null ? HttpMethod.Post : HttpMethod.Put;
            request.Timeout = 600_000;
            request.AddFile(@"content", beatmapArchive, filename);

            if (SubmissionTarget != null)
                request.AddParameter(@"status", SubmissionTarget.Value.ToString().ToLowerInvariant(), RequestParameterType.Form);

            if (BaseRevision != null)
                request.AddParameter(@"base_revision", BaseRevision.Value.ToString(CultureInfo.InvariantCulture), RequestParameterType.Form);

            if (SourceBeatmapSetID is > 0)
                request.AddParameter(@"source_beatmapset_id", SourceBeatmapSetID.Value.ToString(CultureInfo.InvariantCulture), RequestParameterType.Form);

            if (SourceBeatmapID is > 0)
                request.AddParameter(@"source_beatmap_id", SourceBeatmapID.Value.ToString(CultureInfo.InvariantCulture), RequestParameterType.Form);

            return request;
        }

        private void onUploadProgress(long current, long total)
        {
            Debug.Assert(API != null);
            API.Schedule(() => Progressed?.Invoke(current, total));
        }

        public event APIProgressHandler? Progressed;
    }
}
