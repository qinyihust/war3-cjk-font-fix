using System;
using System.IO;
using System.Linq;
using System.Reflection;
using War3FontFix;

internal static class LocalGameTests
{
    private static void EqualFile(string path, byte[] expected)
    {
        if (!File.ReadAllBytes(path).SequenceEqual(expected)) throw new Exception("Unexpected file content: " + Path.GetFileName(path));
    }

    private static int Main(string[] args)
    {
        string fixture = null;
        try
        {
            if (args.Length != 1) throw new ArgumentException("Usage: LocalGameTests.exe <local Game.dll>");
            string sourcePath = Path.GetFullPath(args[0]);
            byte[] source = File.ReadAllBytes(sourcePath);
            var catalog = PatchCatalog.Embedded(Assembly.GetExecutingAssembly());
            var profile = catalog.Find(Bytes.Hash(source), source.Length);
            if (profile == null) throw new UnsupportedFileException();
            byte[] original = PatchEngine.Restore(source, profile), patched = PatchEngine.Apply(source, profile);
            fixture = Path.Combine(Path.GetTempPath(), "war3-fontfix-realfile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fixture);
            string target = Path.Combine(fixture, "Game.dll"), backup = Path.Combine(fixture, profile.backupFileName);
            File.WriteAllBytes(target, original);
            var service = new PatchService(catalog);
            if (!service.Change(fixture, true)) throw new Exception("Initial install did not change the fixture.");
            EqualFile(target, patched); EqualFile(backup, original);
            if (service.Change(fixture, true)) throw new Exception("Repeated install was not idempotent.");
            if (!service.Change(fixture, false)) throw new Exception("Restore did not change the fixture.");
            EqualFile(target, original); EqualFile(backup, original);
            File.Delete(backup);
            foreach (var revision in profile.revisions.Where(r => r.revision != profile.currentRevision))
            {
                byte[] legacy = (byte[])original.Clone();
                foreach (var patch in profile.Sites(revision.revision))
                {
                    byte[] replacement = Bytes.FromHex(patch.replacement);
                    Buffer.BlockCopy(replacement, 0, legacy, patch.fileOffset, replacement.Length);
                }
                if (!Bytes.SameHash(Bytes.Hash(legacy), revision.sha256)) throw new Exception("Legacy fixture failed fingerprint verification.");
                File.WriteAllBytes(target, legacy);
                service.Change(fixture, true); EqualFile(target, patched); EqualFile(backup, original);
                File.Delete(backup);
                service.Change(fixture, false); EqualFile(target, original);
            }
            EqualFile(sourcePath, source);
            Console.WriteLine("PASS: real-file install, idempotence, restore, backup preservation, legacy upgrade, missing-backup reconstruction. Input game file unchanged.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
        finally
        {
            if (fixture != null && Directory.Exists(fixture))
            {
                string full = Path.GetFullPath(fixture);
                string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!full.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)
                    || !Path.GetFileName(full).StartsWith("war3-fontfix-realfile-", StringComparison.Ordinal))
                    throw new IOException("Refusing to remove a non-fixture directory.");
                Directory.Delete(full, true);
            }
        }
    }
}
