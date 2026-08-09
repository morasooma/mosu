// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace osu.Game.Scoring.Render
{
    internal static class ReplayRenderProcessStartInfoBuilder
    {
        public static ProcessStartInfo Create(IReadOnlyList<string> args)
        {
            string processPath = Environment.ProcessPath
                                 ?? throw new InvalidOperationException("Unable to resolve the current process path.");

            string entryAssemblyPath = Assembly.GetEntryAssembly()?.Location
                                       ?? throw new InvalidOperationException("Unable to resolve the game entry assembly.");

            return Create(processPath, entryAssemblyPath, args, AppContext.BaseDirectory);
        }

        internal static ProcessStartInfo Create(string processPath, string entryAssemblyPath, IReadOnlyList<string> args, string workingDirectory)
        {
            string joinedArgs = string.Join(" ", args.Select(quoteArgument));

            bool launchedViaDotnetHost = string.Equals(
                Path.GetFileNameWithoutExtension(processPath),
                "dotnet",
                StringComparison.OrdinalIgnoreCase);

            var startInfo = new ProcessStartInfo
            {
                FileName = processPath,
                Arguments = launchedViaDotnetHost
                    ? $"{quoteArgument(entryAssemblyPath)} {joinedArgs}"
                    : joinedArgs,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = workingDirectory,
            };

            return startInfo;
        }

        internal static string quoteArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
                return "\"\"";

            return argument.Any(char.IsWhiteSpace) || argument.Contains('"')
                ? $"\"{argument.Replace("\"", "\\\"")}\""
                : argument;
        }
    }
}
