using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CulturedStartReloaded.Tests
{
    /// <summary>
    ///     The mod's source as v3.28.2 shipped it, read out of the commit the
    ///     published Nexus package was built from.
    ///
    ///     Cultured Start is held to that release rather than to a description of
    ///     it, so the tests that hold it read the release itself. A copy checked in
    ///     beside the tests would be a second account of what shipped, free to drift
    ///     from the one the package was built from; the commit cannot.
    /// </summary>
    internal static class PublishedSource
    {
        /// <summary>The commit v3.28.2 was built from, byte for byte the shipped package.</summary>
        public const string Commit = "9e56ec4";

        private static readonly Dictionary<string, string> Read_ = new(StringComparer.Ordinal);

        public static string Read(string path)
        {
            lock (Read_)
            {
                if (Read_.TryGetValue(path, out var cached)) return cached;
            }

            var start = new ProcessStartInfo("git", $"show {Commit}:{path}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = ModSource.Path(),
                StandardOutputEncoding = new UTF8Encoding(false)
            };

            using var git = Process.Start(start)
                            ?? throw new InvalidOperationException("git could not be started");
            string text = git.StandardOutput.ReadToEnd();
            string error = git.StandardError.ReadToEnd();
            git.WaitForExit();

            if (git.ExitCode != 0)
                throw new InvalidOperationException($"git show {Commit}:{path} failed: {error}");

            lock (Read_)
            {
                Read_[path] = text;
            }

            return text;
        }

        /// <summary>The same path as it stands in the working tree now.</summary>
        public static string Current(string path) => File.ReadAllText(ModSource.Path(path.Split('/')));
    }
}
