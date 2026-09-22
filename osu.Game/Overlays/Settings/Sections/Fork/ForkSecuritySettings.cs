// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.IO.Network;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Settings;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class ForkSecuritySettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"Практический аудит комнат";

        private readonly Bindable<string> apiEndpoint = new Bindable<string>();
        private readonly Bindable<string> localUser = new Bindable<string>();
        private readonly Bindable<string> accessToken = new Bindable<string>();
        private readonly Bindable<string> roomId = new Bindable<string>();
        private readonly Bindable<string> targetUserId = new Bindable<string>();
        private readonly Bindable<string> playlistId = new Bindable<string>();
        private readonly Bindable<string> beatmapId = new Bindable<string>();
        private readonly Bindable<string> beatmapHash = new Bindable<string>();
        private readonly Bindable<string> rulesetId = new Bindable<string>("0");
        private readonly Bindable<string> rulesetHash = new Bindable<string>();
        private readonly BindableInt burstCount = new BindableInt
        {
            MinValue = 1,
            MaxValue = 10,
            Default = 3,
            Value = 3,
        };

        private readonly BindableInt roomMutationCount = new BindableInt
        {
            MinValue = 1,
            MaxValue = 20,
            Default = 3,
            Value = 3,
        };

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private MultiplayerClient? multiplayerClient { get; set; }

        [Resolved(CanBeNull = true)]
        private BeatmapManager? beatmapManager { get; set; }

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            refreshAutoFilledValues();

            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormTextBox
                {
                    Caption = @"API endpoint",
                    HintText = @"Берётся из вшитого endpoint клиента. В single-server сборке вручную не меняется.",
                    Current = apiEndpoint,
                    ReadOnly = true,
                }),
                new SettingsItemV2(new FormTextBox
                {
                    Caption = @"local_user",
                    HintText = @"Текущий пользователь клиента. target_user_id по умолчанию берётся отсюда.",
                    Current = localUser,
                    ReadOnly = true,
                }),
                new SettingsItemV2(new FormTextBox
                {
                    Caption = @"access_token",
                    HintText = @"Подставляется автоматически из текущей авторизации клиента. Показывается в маскированном виде.",
                    Current = accessToken,
                    ReadOnly = true,
                }),
                new SettingsButtonV2
                {
                    Text = @"Обновить поля из клиента",
                    TooltipText = @"Повторно подтягивает endpoint, текущего пользователя, токен и данные активной multiplayer-комнаты.",
                    Action = refreshAutoFilledValues,
                },
                new SettingsItemV2(new FormNumberBox
                {
                    Caption = @"room_id",
                    HintText = @"ID комнаты для тестов завершения, join/part и повторного self-join. Если вы уже в комнате, подставляется автоматически.",
                    Current = roomId,
                    PlaceholderText = @"Например: 123",
                }),
                new SettingsItemV2(new FormNumberBox
                {
                    Caption = @"target_user_id",
                    HintText = @"ID пользователя для тестов принудительного добавления и удаления из комнаты. По умолчанию берётся из текущего клиента.",
                    Current = targetUserId,
                    PlaceholderText = @"Например: 456",
                }),
                new SettingsItemV2(new FormNumberBox
                {
                    Caption = @"playlist_id",
                    HintText = @"ID playlist item для теста создания room score token без вступления в комнату. Если вы уже в комнате, подставляется автоматически.",
                    Current = playlistId,
                    PlaceholderText = @"Например: 0",
                }),
                new SettingsItemV2(new FormNumberBox
                {
                    Caption = @"beatmap_id",
                    HintText = @"Beatmap ID для теста room score token. Если текущий playlist item известен клиенту, подставляется автоматически.",
                    Current = beatmapId,
                    PlaceholderText = @"Например: 75",
                }),
                new SettingsItemV2(new FormTextBox
                {
                    Caption = @"beatmap_hash",
                    HintText = @"MD5 хэш beatmap-файла для теста room score token. Подставляется из текущего playlist item или локальной базы битмап.",
                    Current = beatmapHash,
                    PlaceholderText = @"Например: a5b99395a42bd55bc5eb1d2411cbdf8b",
                }),
                new SettingsItemV2(new FormNumberBox
                {
                    Caption = @"ruleset_id",
                    HintText = @"0 = Standard, 1 = Taiko, 2 = Catch, 3 = Mania. Если текущий playlist item известен клиенту, подставляется автоматически.",
                    Current = rulesetId,
                    PlaceholderText = @"Обычно 0",
                }),
                new SettingsItemV2(new FormTextBox
                {
                    Caption = @"ruleset_hash",
                    HintText = @"Необязательный ruleset hash. Если не нужен, оставьте пустым.",
                    Current = rulesetHash,
                    PlaceholderText = @"Можно оставить пустым",
                }),
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = @"Количество комнат в burst-тесте",
                    HintText = @"Показывает, что сервер не ограничивает пользователя одной комнатой.",
                    Current = burstCount,
                    KeyboardStep = 1,
                    LabelFormat = value => $"{value} шт.",
                }),
                createActionButton(
                    @"Тест: завершить чужую комнату по room_id",
                    @"Отправляет DELETE /api/v2/rooms/{room_id}. Полезно сразу смотреть серверный лог на отсутствие проверки владельца.",
                    runCloseRoomAudit
                ),
                createActionButton(
                    @"Тест: добавить target_user_id в room_id",
                    @"Отправляет PUT /api/v2/rooms/{room_id}/users/{target_user_id}. Показывает, что сервер доверяет user_id из path.",
                    runForceJoinAudit
                ),
                createActionButton(
                    @"Тест: удалить target_user_id из room_id",
                    @"Отправляет DELETE /api/v2/rooms/{room_id}/users/{target_user_id}. Полезно смотреть лог и participant_count после запроса.",
                    runForcePartAudit
                ),
                createActionButton(
                    @"Тест: повторный self-join в ту же комнату",
                    @"Отправляет два PUT-запроса на вступление собой в одну и ту же room_id и показывает participant_count после каждого шага.",
                    runRepeatSelfJoinAudit
                ),
                createActionButton(
                    @"Тест: создать score token без вступления в комнату",
                    @"Отправляет POST /api/v2/rooms/{room_id}/playlist/{playlist_id}/scores. Если проходит, значит участие в комнате не проверяется перед выдачей токена.",
                    runForeignScoreTokenAudit
                ),
                createActionButton(
                    @"Тест: создать много комнат подряд",
                    @"Создаёт несколько комнат подряд обычным API и показывает их ID. Полезно, чтобы увидеть в логах отсутствие лимита на количество комнат у пользователя.",
                    runBurstCreateAudit
                ),
                createActionButton(
                    @"Тест: создать комнату с подменёнными полями",
                    @"Создаёт комнату с заранее подставленными participant_count, status, type и queue_mode, чтобы посмотреть, что сервер примет и что окажется в логах/ответе.",
                    runForgedRoomAudit
                ),
            });

            Add(new SettingsItemV2(new FormSliderBar<int>
            {
                Caption = @"join/part request count",
                HintText = @"How many times the forced join, forced part, and repeated self-join tests should hit the same room/user pair.",
                Current = roomMutationCount,
                KeyboardStep = 1,
                LabelFormat = value => $"{value}x",
            }));
        }

        private void refreshAutoFilledValues()
        {
            apiEndpoint.Value = api.Endpoints.APIUrl;
            localUser.Value = buildLocalUserText();
            accessToken.Value = maskAccessToken(api.AccessToken);

            if (api.LocalUser.Value.Id > 0)
                targetUserId.Value = api.LocalUser.Value.Id.ToString(CultureInfo.InvariantCulture);

            MultiplayerRoom? room = multiplayerClient?.Room;

            if (room == null)
                return;

            roomId.Value = room.RoomID.ToString(CultureInfo.InvariantCulture);

            MultiplayerPlaylistItem? currentItem = room.Playlist.SingleOrDefault(item => item.ID == room.Settings.PlaylistItemId)
                                              ?? room.Playlist.FirstOrDefault(item => !item.Expired)
                                              ?? room.Playlist.FirstOrDefault();

            if (currentItem == null)
                return;

            playlistId.Value = currentItem.ID.ToString(CultureInfo.InvariantCulture);
            beatmapId.Value = currentItem.BeatmapID.ToString(CultureInfo.InvariantCulture);
            rulesetId.Value = currentItem.RulesetID.ToString(CultureInfo.InvariantCulture);

            string beatmapChecksum = currentItem.BeatmapChecksum;

            if (string.IsNullOrWhiteSpace(beatmapChecksum))
                beatmapChecksum = beatmapManager?.QueryOnlineBeatmapId(currentItem.BeatmapID)?.MD5Hash ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(beatmapChecksum))
                beatmapHash.Value = beatmapChecksum;
        }

        private string buildLocalUserText()
        {
            if (!api.IsLoggedIn)
            {
                if (!string.IsNullOrWhiteSpace(api.ProvidedUsername))
                    return $@"{api.ProvidedUsername} (токен ещё не получен)";

                return @"Не авторизован";
            }

            return $@"{api.LocalUser.Value.Username} ({api.LocalUser.Value.Id.ToString(CultureInfo.InvariantCulture)})";
        }

        private static string maskAccessToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return @"Будет взят из текущей сессии после входа";

            const int visible_chars = 6;

            if (token.Length <= visible_chars * 2)
                return token;

            return $@"{token.Substring(0, visible_chars)}...{token.Substring(token.Length - visible_chars)}";
        }

        private void updateRoomIdField(long? createdRoomId)
        {
            if (createdRoomId == null)
                return;

            Schedule(() => roomId.Value = createdRoomId.Value.ToString(CultureInfo.InvariantCulture));
        }

        private SettingsButtonV2 createActionButton(string title, string tooltip, Func<Task> action)
        {
            return new SettingsButtonV2
            {
                Text = title,
                TooltipText = tooltip,
                Keywords = new[] { @"комнаты", @"multiplayer", @"аудит", @"безопасность", @"api" },
                Action = () => action(),
            };
        }

        private async Task runCloseRoomAudit()
        {
            if (!ensureAuditAllowed())
                return;

            if (!tryGetLong(roomId, @"room_id", out long parsedRoomId))
                return;

            try
            {
                await performRequestAsync(new ClosePlaylistRequest(parsedRoomId)).ConfigureAwait(false);
                Room room = await performRequestAsync(new GetRoomRequest(parsedRoomId)).ConfigureAwait(false);

                showDialog(
                    @"Завершение комнаты выполнено",
                    $"DELETE /api/v2/rooms/{parsedRoomId} завершился без ошибки." +
                    $"\n\nСервер после запроса вернул: id={room.RoomID}, status={room.Status}, ends_at={room.EndDate?.ToString() ?? "null"}." +
                    "\n\nТеперь откройте лог сервера и проверьте, была ли там проверка владельца комнаты."
                );
            }
            catch (Exception ex)
            {
                showDialog(@"Завершение комнаты не прошло", buildErrorText(ex));
            }
        }

        private async Task runForceJoinAudit()
        {
            if (!ensureAuditAllowed())
                return;

            if (!tryGetLong(roomId, @"room_id", out long parsedRoomId))
                return;
            if (!tryGetLong(targetUserId, @"target_user_id", out long parsedUserId))
                return;

            if (roomMutationCount.Value >= 1)
            {
                var report = new StringBuilder();

                try
                {
                    for (int i = 0; i < roomMutationCount.Value; i++)
                    {
                        Room room = await performRequestAsync(new JoinUserToRoomRequest(parsedRoomId, parsedUserId)).ConfigureAwait(false);
                        appendRoomSnapshot(report, i + 1, room);
                    }

                    showDialog(
                        @"Forced join completed",
                        $"PUT /api/v2/rooms/{parsedRoomId}/users/{parsedUserId} succeeded {roomMutationCount.Value} time(s)." +
                        $"\n\n{report}" +
                        "\n\nIf participant_count keeps climbing on repeated requests, the server is trusting repeated room/user mutations from the path."
                    );
                }
                catch (Exception ex)
                {
                    showDialog(@"Forced join failed", buildRepeatedAuditError(report, ex));
                }

                return;
            }

            try
            {
                Room room = await performRequestAsync(new JoinUserToRoomRequest(parsedRoomId, parsedUserId)).ConfigureAwait(false);

                showDialog(
                    @"Принудительное добавление выполнено",
                    $"PUT /api/v2/rooms/{parsedRoomId}/users/{parsedUserId} завершился без ошибки." +
                    $"\n\nСервер вернул room id={room.RoomID}, participant_count={room.ParticipantCount}, status={room.Status}." +
                    "\n\nТеперь проверьте серверный лог и убедитесь, что действие прошло без проверки host/self."
                );
            }
            catch (Exception ex)
            {
                showDialog(@"Принудительное добавление не прошло", buildErrorText(ex));
            }
        }

        private async Task runForcePartAudit()
        {
            if (!ensureAuditAllowed())
                return;

            if (!tryGetLong(roomId, @"room_id", out long parsedRoomId))
                return;
            if (!tryGetLong(targetUserId, @"target_user_id", out long parsedUserId))
                return;

            if (roomMutationCount.Value >= 1)
            {
                var report = new StringBuilder();

                try
                {
                    for (int i = 0; i < roomMutationCount.Value; i++)
                    {
                        await performRequestAsync(new PartUserFromRoomRequest(parsedRoomId, parsedUserId)).ConfigureAwait(false);
                        Room room = await performRequestAsync(new GetRoomRequest(parsedRoomId)).ConfigureAwait(false);
                        appendRoomSnapshot(report, i + 1, room);
                    }

                    showDialog(
                        @"Forced part completed",
                        $"DELETE /api/v2/rooms/{parsedRoomId}/users/{parsedUserId} succeeded {roomMutationCount.Value} time(s)." +
                        $"\n\n{report}" +
                        "\n\nIf participant_count keeps dropping even when the same user is already gone, the server is trusting repeated leave operations too much."
                    );
                }
                catch (Exception ex)
                {
                    showDialog(@"Forced part failed", buildRepeatedAuditError(report, ex));
                }

                return;
            }

            try
            {
                await performRequestAsync(new PartUserFromRoomRequest(parsedRoomId, parsedUserId)).ConfigureAwait(false);
                Room room = await performRequestAsync(new GetRoomRequest(parsedRoomId)).ConfigureAwait(false);

                showDialog(
                    @"Принудительное удаление выполнено",
                    $"DELETE /api/v2/rooms/{parsedRoomId}/users/{parsedUserId} завершился без ошибки." +
                    $"\n\nПосле запроса сервер вернул participant_count={room.ParticipantCount}, status={room.Status}." +
                    "\n\nСверьте это с логом сервера и посмотрите, была ли проверка прав на удаление чужого пользователя."
                );
            }
            catch (Exception ex)
            {
                showDialog(@"Принудительное удаление не прошло", buildErrorText(ex));
            }
        }

        private async Task runRepeatSelfJoinAudit()
        {
            if (!ensureAuditAllowed())
                return;

            if (!tryGetLong(roomId, @"room_id", out long parsedRoomId))
                return;

            if (roomMutationCount.Value >= 1)
            {
                var report = new StringBuilder();

                try
                {
                    var selfRoom = new Room { RoomID = parsedRoomId };

                    for (int i = 0; i < roomMutationCount.Value; i++)
                    {
                        Room room = await performRequestAsync(new JoinRoomRequest(selfRoom, null)).ConfigureAwait(false);
                        appendRoomSnapshot(report, i + 1, room);
                    }

                    showDialog(
                        @"Repeated self-join completed",
                        $"PUT /api/v2/rooms/{parsedRoomId}/users/self ran {roomMutationCount.Value} time(s)." +
                        $"\n\n{report}" +
                        "\n\nIf each repeated self-join bumps participant_count again, the same account can inflate room population without changing identity."
                    );
                }
                catch (Exception ex)
                {
                    showDialog(@"Repeated self-join failed", buildRepeatedAuditError(report, ex));
                }

                return;
            }

            try
            {
                var selfRoom = new Room { RoomID = parsedRoomId };
                Room firstJoin = await performRequestAsync(new JoinRoomRequest(selfRoom, null)).ConfigureAwait(false);
                Room secondJoin = await performRequestAsync(new JoinRoomRequest(selfRoom, null)).ConfigureAwait(false);

                showDialog(
                    @"Повторный self-join выполнен",
                    $"Первый PUT вернул participant_count={firstJoin.ParticipantCount}." +
                    $"\nВторой PUT вернул participant_count={secondJoin.ParticipantCount}." +
                    "\n\nЕсли второй запрос тоже увеличил счётчик, значит participant_count можно раскачать повторными join-запросами." +
                    "\n\nТеперь откройте лог сервера и сравните оба запроса по времени."
                );
            }
            catch (Exception ex)
            {
                showDialog(@"Повторный self-join не прошёл", buildErrorText(ex));
            }
        }

        private async Task runForeignScoreTokenAudit()
        {
            if (!ensureAuditAllowed())
                return;

            if (!tryGetLong(roomId, @"room_id", out long parsedRoomId))
                return;
            if (!tryGetLong(playlistId, @"playlist_id", out long parsedPlaylistId))
                return;
            if (!tryGetLong(beatmapId, @"beatmap_id", out long parsedBeatmapId))
                return;
            if (!tryGetInt(rulesetId, @"ruleset_id", out int parsedRulesetId))
                return;
            if (string.IsNullOrWhiteSpace(beatmapHash.Value))
            {
                showDialog(@"Не заполнен beatmap_hash", @"Для этого теста нужен корректный beatmap_hash.");
                return;
            }

            try
            {
                RoomScoreTokenResponse token = await performRequestAsync(new CreateRoomScoreTokenByIdsRequest(
                    parsedRoomId,
                    parsedPlaylistId,
                    parsedBeatmapId,
                    beatmapHash.Value.Trim(),
                    parsedRulesetId,
                    "fork-security-audit",
                    string.IsNullOrWhiteSpace(rulesetHash.Value) ? null : rulesetHash.Value.Trim()
                )).ConfigureAwait(false);

                showDialog(
                    @"Score token получен",
                    $"POST /api/v2/rooms/{parsedRoomId}/playlist/{parsedPlaylistId}/scores завершился без ошибки и выдал token id={token.ID}." +
                    "\n\nЕсли вы в эту комнату не вступали, это и есть практическое подтверждение, что участие в комнате не проверяется перед выдачей room score token." +
                    "\n\nТеперь откройте лог сервера и посмотрите цепочку запроса."
                );
            }
            catch (Exception ex)
            {
                showDialog(@"Score token не получен", buildErrorText(ex));
            }
        }

        private async Task runBurstCreateAudit()
        {
            if (!ensureAuditAllowed())
                return;

            try
            {
                long[] createdIds = new long[burstCount.Value];
                APICreatedRoom? lastCreatedRoom = null;

                for (int i = 0; i < burstCount.Value; i++)
                {
                    var room = new Room
                    {
                        Name = $"fork-audit-burst-{DateTimeOffset.UtcNow:HHmmss}-{i + 1}",
                        Category = RoomCategory.Normal,
                        Status = RoomStatus.Idle,
                        Type = MatchType.Playlists,
                        Duration = TimeSpan.FromDays(30),
                        MaxAttempts = 0,
                        ParticipantCount = 0,
                        QueueMode = QueueMode.HostOnly,
                        AutoSkip = false,
                        AutoStartDuration = TimeSpan.Zero,
                        Playlist = Array.Empty<PlaylistItem>(),
                    };

                    APICreatedRoom created = await performRequestAsync(new CreateRoomRequest(room)).ConfigureAwait(false);
                    createdIds[i] = created.RoomID ?? 0;
                    lastCreatedRoom = created;
                }

                updateRoomIdField(lastCreatedRoom?.RoomID);

                showDialog(
                    @"Burst-создание комнат выполнено",
                    $"Создано комнат: {burstCount.Value}." +
                    $"\nID: {string.Join(", ", createdIds.Select(id => id.ToString(CultureInfo.InvariantCulture)))}." +
                    "\n\nЭто практический тест на отсутствие серверного лимита по количеству комнат на пользователя." +
                    "\n\nТеперь откройте лог сервера и проверьте, что все POST /rooms прошли подряд."
                );
            }
            catch (Exception ex)
            {
                showDialog(@"Burst-создание не прошло", buildErrorText(ex));
            }
        }

        private async Task runForgedRoomAudit()
        {
            if (!ensureAuditAllowed())
                return;

            try
            {
                var room = new Room
                {
                    Name = $"fork-audit-forged-{DateTimeOffset.UtcNow:HHmmss}",
                    Category = RoomCategory.Normal,
                    Status = RoomStatus.Playing,
                    Type = MatchType.Playlists,
                    Duration = TimeSpan.FromDays(30),
                    MaxAttempts = 9,
                    ParticipantCount = 37,
                    QueueMode = QueueMode.HostOnly,
                    AutoSkip = true,
                    AutoStartDuration = TimeSpan.FromSeconds(13),
                    Playlist = Array.Empty<PlaylistItem>(),
                };

                APICreatedRoom created = await performRequestAsync(new CreateRoomRequest(room)).ConfigureAwait(false);

                updateRoomIdField(created.RoomID);

                showDialog(
                    @"Комната с подменёнными полями создана",
                    $"Сервер вернул id={created.RoomID}, participant_count={created.ParticipantCount}, status={created.Status}, type={created.Type}, queue_mode={created.QueueMode}." +
                    "\n\nЭто практический тест на доверие к клиентским полям при создании комнаты." +
                    "\n\nТеперь откройте лог сервера и посмотрите, какие поля он принял без нормализации."
                );
            }
            catch (Exception ex)
            {
                showDialog(@"Подмена полей не прошла", buildErrorText(ex));
            }
        }

        private bool ensureAuditAllowed()
        {
            if (!api.IsLoggedIn)
            {
                showDialog(@"Нужна авторизация", @"Для практического аудита нужен вход в аккаунт и рабочий access token.");
                return false;
            }

            localUser.Value = buildLocalUserText();
            accessToken.Value = maskAccessToken(api.AccessToken);
            return true;
        }

        private bool tryGetLong(Bindable<string> source, string fieldName, out long value)
        {
            if (long.TryParse(source.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return true;

            showDialog(@"Некорректное число", $"Поле {fieldName} должно быть целым числом.");
            return false;
        }

        private bool tryGetInt(Bindable<string> source, string fieldName, out int value)
        {
            if (int.TryParse(source.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return true;

            showDialog(@"Некорректное число", $"Поле {fieldName} должно быть целым числом.");
            return false;
        }

        private static void appendRoomSnapshot(StringBuilder report, int attempt, Room room)
        {
            if (report.Length > 0)
                report.Append('\n');

            report.Append($"{attempt}. participant_count={room.ParticipantCount}, status={room.Status}, room_id={room.RoomID}");
        }

        private static string buildRepeatedAuditError(StringBuilder report, Exception ex)
        {
            if (report.Length == 0)
                return buildErrorText(ex);

            return $"Completed steps before failure:\n{report}\n\n{buildErrorText(ex)}";
        }

        private Task performRequestAsync(APIRequest request)
        {
            var completion = new TaskCompletionSource<object?>();
            request.Success += () => completion.TrySetResult(null);
            request.Failure += ex => completion.TrySetException(ex);
            _ = api.PerformAsync(request);
            return completion.Task;
        }

        private Task<T> performRequestAsync<T>(APIRequest<T> request)
            where T : class
        {
            var completion = new TaskCompletionSource<T>();
            request.Success += response => completion.TrySetResult(response);
            request.Failure += ex => completion.TrySetException(ex);
            _ = api.PerformAsync(request);
            return completion.Task;
        }

        private void showDialog(string title, string body)
        {
            Schedule(() => dialogOverlay?.Push(new AuditResultDialog(title, body)));
        }

        private static string buildErrorText(Exception ex) =>
            $"Запрос завершился ошибкой: {ex.Message}\n\nЕсли это локальный сервер, сразу откройте его лог и сравните время ошибки с нажатием кнопки.";

        private sealed partial class AuditResultDialog : PopupDialog
        {
            public AuditResultDialog(LocalisableString title, LocalisableString body)
            {
                HeaderText = title;
                BodyText = body;
                Icon = FontAwesome.Solid.ExclamationTriangle;

                Buttons = new PopupDialogButton[]
                {
                    new PopupDialogOkButton
                    {
                        Text = @"OK",
                    },
                };
            }
        }

        private sealed class RoomScoreTokenResponse
        {
            [JsonProperty("id")]
            public long ID { get; set; }
        }

        private sealed class CreateRoomScoreTokenByIdsRequest : APIRequest<RoomScoreTokenResponse>
        {
            private readonly long roomId;
            private readonly long playlistItemId;
            private readonly long beatmapId;
            private readonly string beatmapHash;
            private readonly int rulesetId;
            private readonly string versionHash;
            private readonly string? rulesetHash;

            public CreateRoomScoreTokenByIdsRequest(
                long roomId,
                long playlistItemId,
                long beatmapId,
                string beatmapHash,
                int rulesetId,
                string versionHash,
                string? rulesetHash)
            {
                this.roomId = roomId;
                this.playlistItemId = playlistItemId;
                this.beatmapId = beatmapId;
                this.beatmapHash = beatmapHash;
                this.rulesetId = rulesetId;
                this.versionHash = versionHash;
                this.rulesetHash = rulesetHash;
            }

            protected override WebRequest CreateWebRequest()
            {
                var request = base.CreateWebRequest();
                request.Method = HttpMethod.Post;
                request.AddParameter("version_hash", versionHash);
                request.AddParameter("beatmap_id", beatmapId.ToString(CultureInfo.InvariantCulture));
                request.AddParameter("beatmap_hash", beatmapHash);
                request.AddParameter("ruleset_id", rulesetId.ToString(CultureInfo.InvariantCulture));

                if (!string.IsNullOrWhiteSpace(rulesetHash))
                    request.AddParameter("ruleset_hash", rulesetHash);

                return request;
            }

            protected override string Target => $@"rooms/{roomId}/playlist/{playlistItemId}/scores";
        }
    }
}
