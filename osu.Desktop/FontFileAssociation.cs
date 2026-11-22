using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using osu.Game.Graphics.Fonts;

namespace osu.Desktop
{
    internal sealed class FontFileAssociation : IFontFileAssociation
    {
        public bool IsSupported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

        public void Register()
        {
            if (OperatingSystem.IsWindows())
            {
                Windows.WindowsAssociationManager.InstallFontAssociation();
                return;
            }
            if (!OperatingSystem.IsLinux())
                throw new PlatformNotSupportedException("Use the installed application bundle's file association on this platform.");
            string home = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string applications = Path.Combine(home, "applications");
            string packages = Path.Combine(home, "mime", "packages");
            Directory.CreateDirectory(applications);
            Directory.CreateDirectory(packages);
            string appImage = Environment.GetEnvironmentVariable("APPIMAGE") ?? string.Empty;
            string exe = Environment.ProcessPath ?? throw new InvalidOperationException("Missing executable path.");
            string assembly = typeof(FontFileAssociation).Assembly.Location;
            string exec = !string.IsNullOrEmpty(appImage) ? quote(appImage)
                : Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
                    ? quote(exe) + " " + quote(assembly) : quote(exe);
            bool launcherChanged = writeIfChanged(Path.Combine(applications, "morasooma-fonts.desktop"),
                "[Desktop Entry]\nType=Application\nName=Morasooma Fonts\nExec=" + exec + " %f\nTerminal=false\nNoDisplay=true\nMimeType=application/x-morasooma-fonts;\n");
            bool mimeChanged = writeIfChanged(Path.Combine(packages, "morasooma-fonts.xml"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><mime-info xmlns=\"http://www.freedesktop.org/standards/shared-mime-info\"><mime-type type=\"application/x-morasooma-fonts\"><comment>Morasooma font settings and atlases</comment><glob pattern=\"*.morafn\"/></mime-type></mime-info>");
            launcherChanged |= writeIfChanged(Path.Combine(applications, "morasooma-resources.desktop"),
                "[Desktop Entry]\nType=Application\nName=Morasooma Resources\nExec=" + exec + " %f\nTerminal=false\nNoDisplay=true\nMimeType=application/x-morasooma-resources;\n");
            mimeChanged |= writeIfChanged(Path.Combine(packages, "morasooma-resources.xml"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><mime-info xmlns=\"http://www.freedesktop.org/standards/shared-mime-info\"><mime-type type=\"application/x-morasooma-resources\"><comment>Morasooma game resources</comment><glob pattern=\"*.morars\"/></mime-type></mime-info>");
            if (mimeChanged)
                run("update-mime-database", Path.Combine(home, "mime"));
            if (launcherChanged)
                run("update-desktop-database", applications);
            run("xdg-mime", "default", "morasooma-fonts.desktop", "application/x-morasooma-fonts");
            run("xdg-mime", "default", "morasooma-resources.desktop", "application/x-morasooma-resources");
        }

        private static bool writeIfChanged(string path, string content)
        {
            if (File.Exists(path) && File.ReadAllText(path) == content)
                return false;
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return true;
        }

        internal static string quote(string value)
        {
            if (value.Contains('\n') || value.Contains('\r')) throw new ArgumentException("Unsafe launcher path.");
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("`", "\\`").Replace("$", "\\$").Replace("%", "%%") + "\"";
        }
        private static void run(string command, params string[] args)
        {
            var start = new ProcessStartInfo(command) { UseShellExecute = false, CreateNoWindow = true };
            foreach (string argument in args) start.ArgumentList.Add(argument);
            using var process = Process.Start(start) ?? throw new IOException("Could not register file association.");
            if (!process.WaitForExit(10000)) { process.Kill(); throw new TimeoutException("File association command timed out."); }
            if (process.ExitCode != 0) throw new IOException("File association command failed: " + command);
        }
    }
}
