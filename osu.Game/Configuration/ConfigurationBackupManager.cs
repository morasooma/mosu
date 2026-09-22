// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Online.API.Requests;

namespace osu.Game.Configuration
{
    public static class ConfigurationBackupManager
    {
        public const int FORMAT_VERSION = 1;
        public const int MAXIMUM_FILE_BYTES = 256 * 1024;
        public const int MAXIMUM_TOTAL_BYTES = 512 * 1024;

        private const string pending_restore_filename = ".config-restore.pending.json";

        private static readonly string[] config_files =
        {
            "input.json",
            "game.ini",
            "mosu.ini",
            "server_profiles.json",
        };

        public static ConfigBackupUpload CreateUpload(Storage storage)
        {
            ArgumentNullException.ThrowIfNull(storage);

            string?[] files = new string?[config_files.Length];
            int totalBytes = 0;

            for (int i = 0; i < config_files.Length; i++)
            {
                if (!storage.Exists(config_files[i]))
                    continue;

                byte[] data = readFile(storage, config_files[i]);
                validateSize(data, ref totalBytes);
                files[i] = Convert.ToBase64String(data);
            }

            if (Array.TrueForAll(files, static file => file is null))
                throw new InvalidOperationException("No configuration files exist in the current storage.");

            return new ConfigBackupUpload
            {
                FormatVersion = FORMAT_VERSION,
                Files = new ConfigBackupFiles
                {
                    Input = files[0],
                    Game = files[1],
                    Mosu = files[2],
                    ServerProfiles = files[3],
                },
            };
        }

        public static void StageRestore(Storage storage, ConfigBackupResponse backup)
        {
            ArgumentNullException.ThrowIfNull(storage);
            ArgumentNullException.ThrowIfNull(backup);
            validate(backup.FormatVersion, backup.Files);

            using var stream = storage.CreateFileSafely(pending_restore_filename);
            using var writer = new StreamWriter(stream);
            writer.Write(JsonConvert.SerializeObject(new ConfigBackupUpload
            {
                FormatVersion = backup.FormatVersion,
                Files = backup.Files,
            }));
        }

        /// <summary>
        /// Applies a downloaded backup before the host creates any configuration managers.
        /// The pending file is retained on failure, allowing the next launch to finish a partially applied restore.
        /// </summary>
        public static bool ApplyPendingRestore(Storage? storage, bool restoreInputConfiguration = true)
        {
            if (storage == null)
            {
                Logger.Log("Skipped pending configuration restore because game storage is not initialised.",
                    LoggingTarget.Runtime, LogLevel.Error);
                return false;
            }

            if (!storage.Exists(pending_restore_filename))
                return false;

            try
            {
                ConfigBackupUpload pending;

                using (var stream = storage.GetStream(pending_restore_filename, FileAccess.Read, FileMode.Open))
                using (var reader = new StreamReader(stream))
                {
                    pending = JsonConvert.DeserializeObject<ConfigBackupUpload>(reader.ReadToEnd())
                              ?? throw new InvalidDataException("Pending configuration restore is empty.");
                }

                byte[]?[] decoded = validate(pending.FormatVersion, pending.Files);

                for (int i = 0; i < config_files.Length; i++)
                {
                    // Input handlers are created by the host before the game can apply a pending restore.
                    // Restoring a desktop input.json on mobile can therefore disable touch on the next launch.
                    if (i == 0 && !restoreInputConfiguration)
                        continue;

                    if (decoded[i] is null)
                    {
                        if (storage.Exists(config_files[i]))
                            storage.Delete(config_files[i]);

                        continue;
                    }

                    using var output = storage.CreateFileSafely(config_files[i]);
                    output.Write(decoded[i]!);
                }

                storage.Delete(pending_restore_filename);
                Logger.Log("Applied the pending server configuration backup.", LoggingTarget.Runtime, LogLevel.Important);
                return true;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Failed to apply the pending server configuration backup");
                return false;
            }
        }

        private static byte[]?[] validate(int formatVersion, ConfigBackupFiles? files)
        {
            if (formatVersion != FORMAT_VERSION)
                throw new InvalidDataException($"Unsupported configuration backup format {formatVersion}.");
            if (files == null)
                throw new InvalidDataException("Configuration backup does not contain a file manifest.");

            string?[] encoded = { files.Input, files.Game, files.Mosu, files.ServerProfiles };
            var decoded = new byte[]?[encoded.Length];
            int totalBytes = 0;

            for (int i = 0; i < encoded.Length; i++)
            {
                if (encoded[i] is null)
                    continue;

                try
                {
                    decoded[i] = Convert.FromBase64String(encoded[i]!);
                }
                catch (FormatException exception)
                {
                    throw new InvalidDataException($"Configuration file '{config_files[i]}' is not valid base64.", exception);
                }

                validateSize(decoded[i]!, ref totalBytes);
            }

            if (Array.TrueForAll(decoded, static file => file is null))
                throw new InvalidDataException("Configuration backup contains no files.");

            return decoded;
        }

        private static byte[] readFile(Storage storage, string filename)
        {
            using var stream = storage.GetStream(filename, FileAccess.Read, FileMode.Open);
            if (stream.Length > MAXIMUM_FILE_BYTES)
                throw new InvalidDataException($"Configuration file '{filename}' exceeds the {MAXIMUM_FILE_BYTES}-byte limit.");

            using var memory = new MemoryStream((int)stream.Length);
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        private static void validateSize(byte[] data, ref int totalBytes)
        {
            if (data.Length > MAXIMUM_FILE_BYTES)
                throw new InvalidDataException($"A configuration file exceeds the {MAXIMUM_FILE_BYTES}-byte limit.");

            totalBytes = checked(totalBytes + data.Length);
            if (totalBytes > MAXIMUM_TOTAL_BYTES)
                throw new InvalidDataException($"Configuration backup exceeds the {MAXIMUM_TOTAL_BYTES}-byte limit.");
        }
    }
}
