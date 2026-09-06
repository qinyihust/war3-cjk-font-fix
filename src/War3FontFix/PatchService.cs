using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace War3FontFix
{
    public sealed class Inspection
    {
        public string FilePath;
        public string Sha256;
        public long Length;
        public PatchProfile Profile;
        public int Revision;
        public bool Supported { get { return Profile != null; } }
    }

    public sealed class PatchService
    {
        private readonly PatchCatalog catalog;
        private readonly Action requireStopped;

        public PatchService(PatchCatalog catalog) : this(catalog, RequireGameStopped) { }
        public PatchService(PatchCatalog catalog, Action requireStopped)
        {
            this.catalog = catalog;
            this.requireStopped = requireStopped;
        }

        public static void RequireGameStopped()
        {
            foreach (var process in Process.GetProcessesByName("war3"))
                using (process)
                {
                    try { if (!process.HasExited) throw new IOException("Exit Warcraft III before installing or restoring the patch."); }
                    catch (InvalidOperationException) { /* The process exited during enumeration. */ }
                }
        }

        public static string Target(string directory)
        {
            if (String.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Select the game directory containing Game.dll.");
            return Path.Combine(Path.GetFullPath(directory), "Game.dll");
        }

        public Inspection Inspect(string directory)
        {
            string target = Target(directory);
            using (var stream = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                var profile = catalog.Find(hash, stream.Length);
                return new Inspection { FilePath = target, Sha256 = hash, Length = stream.Length, Profile = profile,
                    Revision = profile == null ? -1 : profile.Identify(hash, stream.Length) };
            }
        }

        public bool Change(string directory, bool install)
        {
            requireStopped();
            string target = Target(directory);
            string key = Bytes.Hash(Encoding.UTF8.GetBytes(target.ToUpperInvariant()));
            using (var mutex = new Mutex(false, "Local\\War3FontFix-" + key))
            {
                bool acquired = false;
                try
                {
                    try { acquired = mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired) throw new IOException("Another patch operation is using this game directory.");
                    return ChangeLocked(target, install);
                }
                finally { if (acquired) mutex.ReleaseMutex(); }
            }
        }

        private bool ChangeLocked(string target, bool install)
        {
            byte[] source = File.ReadAllBytes(target);
            string inputHash = Bytes.Hash(source);
            var profile = catalog.Find(inputHash, source.Length);
            if (profile == null) throw new UnsupportedFileException();
            string expectedHash = install ? profile.Current.sha256 : profile.originalSha256;
            if (Bytes.SameHash(inputHash, expectedHash)) return false;

            byte[] original = PatchEngine.Restore(source, profile);
            byte[] result = install ? PatchEngine.Apply(source, profile) : original;
            string folder = Path.GetDirectoryName(target);
            string backup = Path.Combine(folder, profile.backupFileName);
            if (File.Exists(backup))
            {
                if (new FileInfo(backup).Length != profile.fileLength || !Bytes.SameHash(Bytes.FileHash(backup), profile.originalSha256))
                    throw new IOException("The existing backup does not match the supported original. The backup and Game.dll were not overwritten.");
            }
            else if (install) WriteNew(backup, original);

            string temporary = Path.Combine(folder, "war3-fontfix-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                // Stage on the same volume; retain the target's file attributes and flush before replacement.
                File.Copy(target, temporary, false);
                using (var stream = new FileStream(temporary, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    stream.Write(result, 0, result.Length);
                    stream.SetLength(result.Length);
                    stream.Flush(true);
                }
                if (!Bytes.SameHash(Bytes.FileHash(temporary), expectedHash)) throw new IOException("Staged-file verification failed.");
                requireStopped();
                if (!Bytes.SameHash(Bytes.FileHash(target), inputHash))
                    throw new IOException("Game.dll changed during the operation. Replacement was cancelled.");
                File.Replace(temporary, target, null);
                if (!Bytes.SameHash(Bytes.FileHash(target), expectedHash))
                    throw new IOException("Final verification failed. Keep the original backup.");
                // Unlike the early local installer, restoration preserves the original backup.
                return true;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { Trace.WriteLine("Could not remove staging file: " + temporary); }
                catch (UnauthorizedAccessException) { Trace.WriteLine("Could not remove staging file: " + temporary); }
            }
        }

        private static void WriteNew(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        public string SelfTest(string gameFile)
        {
            byte[] source = File.ReadAllBytes(gameFile);
            var profile = catalog.Find(Bytes.Hash(source), source.Length);
            if (profile == null) throw new UnsupportedFileException();
            byte[] original = PatchEngine.Restore(source, profile);
            byte[] patched = PatchEngine.Apply(original, profile);
            byte[] restored = PatchEngine.Restore(patched, profile);
            if (!Bytes.SameHash(Bytes.Hash(restored), profile.originalSha256)) throw new InvalidDataException("Round-trip failed.");
            var unknown = (byte[])original.Clone(); unknown[0] ^= 1;
            bool rejected = false;
            try { PatchEngine.Apply(unknown, profile); } catch (UnsupportedFileException) { rejected = true; }
            if (!rejected) throw new InvalidDataException("Unknown-file rejection failed.");
            int changed = 0;
            for (int i = 0; i < original.Length; i++) if (original[i] != patched[i]) changed++;
            return String.Format("PASS: {0}, revision {1}, {2} changed bytes; exact hashes, round-trip restoration, unknown-file rejection. No files were changed.",
                profile.gameVersion, profile.currentRevision, changed);
        }
    }
}
