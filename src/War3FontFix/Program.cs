using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace War3FontFix
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
#if CONSOLE_APP
            if (args.Length == 0) args = new[] { "--help" };
#endif
            bool gui = args.Length == 0 || (args[0] == "--gui" && args.Length <= 2);
            if (!gui)
            {
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true });
                Console.SetError(new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true });
            }
            try
            {
                var catalog = PatchCatalog.Embedded(Assembly.GetExecutingAssembly());
                var service = new PatchService(catalog);
                if (gui)
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new MainForm(service, args.Length == 2 ? args[1] : null));
                    return 0;
                }
                if (args.Length == 1)
                {
                    switch (args[0])
                    {
                        case "--help": PrintHelp(); return 0;
                        case "--version": Console.WriteLine(Assembly.GetExecutingAssembly().GetName().Version.ToString(3)); return 0;
                        case "--list":
                            foreach (var profile in catalog.Profiles)
                            {
                                Console.WriteLine("Warcraft III " + profile.gameVersion + " / current patch revision " + profile.currentRevision);
                                Console.WriteLine("Original length: " + profile.fileLength);
                                Console.WriteLine("Original SHA-256: " + profile.originalSha256);
                                foreach (var revision in profile.revisions)
                                    Console.WriteLine("Revision " + revision.revision + " length: " + profile.RevisionLength(revision) + "; SHA-256: " + revision.sha256);
                            }
                            return 0;
                    }
                }
                if (args.Length != 2) { PrintHelp(); return 2; }
                switch (args[0])
                {
                    case "--check":
                        var inspection = service.Inspect(args[1]);
                        Console.WriteLine(Describe(inspection));
                        return inspection.Supported ? 0 : 2;
                    case "--apply":
                    case "--restore":
                        bool install = args[0] == "--apply";
                        bool changed = service.Change(args[1], install);
                        Console.WriteLine(changed ? (install ? "Patch installed and verified." : "Original restored and verified.") : "Already in the requested state; no changes made.");
                        Console.WriteLine(Describe(service.Inspect(args[1])));
                        return 0;
                    case "--self-test": Console.WriteLine(service.SelfTest(args[1])); return 0;
                    default: PrintHelp(); return 2;
                }
            }
            catch (Exception error)
            {
                if (gui) MessageBox.Show(error.Message, "War3 CJK Font Fix", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else Console.Error.WriteLine(error.Message);
                return error is UnsupportedFileException || error is ArgumentException ? 2 : 1;
            }
        }

        internal static string Describe(Inspection result)
        {
            string status = !result.Supported ? "Unsupported file; modification is refused."
                : result.Revision == 0 ? "Original; patch available."
                : result.Revision == result.Profile.currentRevision ? "Current patch installed."
                : "Earlier patch revision; upgrade available.";
            return status + Environment.NewLine + result.FilePath + Environment.NewLine
                + (result.Supported ? "Game version: " + result.Profile.gameVersion + "; patch revision: " + result.Revision + Environment.NewLine : "")
                + "Length: " + result.Length + Environment.NewLine + "SHA-256: " + result.Sha256;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("War3 CJK Font Fix\n"
                + "  War3FontFix.exe                         Open the graphical interface\n"
                + "  War3FontFix.exe --gui [game-directory]  Open the graphical interface\n"
                + "  War3FontFix.Cli.exe --check <directory>    Inspect Game.dll (read-only)\n"
                + "  War3FontFix.Cli.exe --apply <directory>    Install or upgrade the patch\n"
                + "  War3FontFix.Cli.exe --restore <directory>  Restore the exact original\n"
                + "  War3FontFix.Cli.exe --self-test <Game.dll> Check transformations in memory\n"
                + "  War3FontFix.Cli.exe --list                 List supported fingerprints\n"
                + "  War3FontFix.Cli.exe --version              Show the installer version\n"
                + "  War3FontFix.Cli.exe --help                 Show this help\n\n"
                + "Quote paths containing spaces. Exit Warcraft III before apply/restore.\n"
                + "Exit codes: 0 success; 1 operation failure; 2 invalid arguments or unsupported file.");
        }
    }
}
