using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using War3FontFix;

internal static class CoreTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly byte[] Original = Enumerable.Range(0, 128).Select(i => (byte)i).ToArray();
        public readonly byte[] Legacy;
        public readonly byte[] Current;
        public readonly PatchProfile Profile;
        public readonly string DirectoryPath;
        public string Target { get { return Path.Combine(DirectoryPath, "Game.dll"); } }
        public string Backup { get { return Path.Combine(DirectoryPath, "original.bak"); } }
        public PatchService Service { get { return new PatchService(new PatchCatalog(new[] { Profile }), delegate { }); } }

        public Fixture()
        {
            Legacy = (byte[])Original.Clone(); Legacy[16] = 200; Legacy[17] = 201;
            Current = (byte[])Legacy.Clone(); Current[64] = 202;
            Profile = new PatchProfile
            {
                schemaVersion = 1, id = "synthetic", gameVersion = "test-only", currentRevision = 2,
                fileLength = Original.Length, originalSha256 = Bytes.Hash(Original), backupFileName = "original.bak",
                patches = new[] {
                    new PatchSite { id = "first", fileOffset = 16, rva = 4096, original = "1011", replacement = "C8C9" },
                    new PatchSite { id = "second", fileOffset = 64, rva = 8192, original = "40", replacement = "CA" }
                },
                revisions = new[] {
                    new PatchRevision { revision = 1, sha256 = Bytes.Hash(Legacy), patchIds = new[] { "first" } },
                    new PatchRevision { revision = 2, sha256 = Bytes.Hash(Current), patchIds = new[] { "first", "second" } }
                }
            };
            Profile.Validate();
            DirectoryPath = Path.Combine(Path.GetTempPath(), "war3-fontfix-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllBytes(Target, Original);
        }

        public void Dispose()
        {
            // The fixture owns only a freshly generated temporary directory.
            string full = Path.GetFullPath(DirectoryPath);
            string root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(full).StartsWith("war3-fontfix-tests-", StringComparison.Ordinal))
                throw new IOException("Refusing to remove a non-fixture directory.");
            Directory.Delete(full, true);
        }
    }

    private static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private static void Test(string name, Action<Fixture> action)
    {
        using (var fixture = new Fixture()) action(fixture);
        Console.WriteLine("PASS " + name);
    }

    private static int Main()
    {
        try
        {
            Test("original/current round trip; source is immutable", f => {
                Check(PatchEngine.Apply(f.Original, f.Profile).SequenceEqual(f.Current), "apply");
                Check(PatchEngine.Restore(f.Current, f.Profile).SequenceEqual(f.Original), "restore");
                Check(f.Original[16] == 16, "source mutated");
            });
            Test("legacy upgrade and legacy rollback", f => {
                Check(PatchEngine.Apply(f.Legacy, f.Profile).SequenceEqual(f.Current), "upgrade");
                Check(PatchEngine.Restore(f.Legacy, f.Profile).SequenceEqual(f.Original), "legacy rollback");
            });
            Test("full fingerprint rejects changes outside patch sites", f => {
                byte[] wrong = (byte[])f.Original.Clone(); wrong[100] ^= 1;
                Throws<UnsupportedFileException>(() => PatchEngine.Apply(wrong, f.Profile));
                Throws<UnsupportedFileException>(() => PatchEngine.Apply(new byte[127], f.Profile));
            });
            Test("site and final-hash verification", f => {
                f.Profile.patches[0].original = "FFFF";
                Throws<InvalidDataException>(() => PatchEngine.Apply(f.Original, f.Profile));
                f.Profile.patches[0].original = "1011";
                f.Profile.patches[0].replacement = "ABCD";
                Throws<InvalidDataException>(() => PatchEngine.Apply(f.Original, f.Profile));
            });
            Test("install/idempotence/restore retains original backup", f => {
                Check(f.Service.Change(f.DirectoryPath, true), "install");
                Check(File.ReadAllBytes(f.Target).SequenceEqual(f.Current), "current on disk");
                Check(File.ReadAllBytes(f.Backup).SequenceEqual(f.Original), "backup");
                Check(!f.Service.Change(f.DirectoryPath, true), "idempotent apply");
                Check(f.Service.Change(f.DirectoryPath, false), "restore");
                Check(File.ReadAllBytes(f.Target).SequenceEqual(f.Original), "original on disk");
                Check(File.ReadAllBytes(f.Backup).SequenceEqual(f.Original), "backup retained");
                Check(!f.Service.Change(f.DirectoryPath, false), "idempotent restore");
            });
            Test("upgrade reconstructs missing original backup", f => {
                File.WriteAllBytes(f.Target, f.Legacy);
                Check(f.Service.Change(f.DirectoryPath, true), "upgrade");
                Check(File.ReadAllBytes(f.Backup).SequenceEqual(f.Original), "reconstructed backup");
            });
            Test("restore without backup reconstructs exact original", f => {
                File.WriteAllBytes(f.Target, f.Current);
                Check(f.Service.Change(f.DirectoryPath, false), "restore without backup");
                Check(File.ReadAllBytes(f.Target).SequenceEqual(f.Original), "reconstruction");
            });
            Test("invalid backup is preserved and prevents replacement", f => {
                File.WriteAllBytes(f.Backup, new byte[] { 9, 9 });
                Throws<IOException>(() => f.Service.Change(f.DirectoryPath, true));
                Check(File.ReadAllBytes(f.Target).SequenceEqual(f.Original), "target unchanged");
                Check(File.ReadAllBytes(f.Backup).SequenceEqual(new byte[] { 9, 9 }), "backup unchanged");
            });
            Test("unsupported disk file causes no backup or staging writes", f => {
                File.WriteAllBytes(f.Target, new byte[] { 1, 2, 3 });
                Throws<UnsupportedFileException>(() => f.Service.Change(f.DirectoryPath, true));
                Check(Directory.GetFiles(f.DirectoryPath).Length == 1, "unexpected writes");
            });
            Test("running-game guard blocks changes before backup", f => {
                var service = new PatchService(new PatchCatalog(new[] { f.Profile }), delegate { throw new IOException("game running"); });
                Throws<IOException>(() => service.Change(f.DirectoryPath, true));
                Check(!File.Exists(f.Backup), "backup written");
            });
            Test("external target changes before replacement are preserved", f => {
                int guards = 0;
                byte[] external = (byte[])f.Original.Clone(); external[80] ^= 1;
                var service = new PatchService(new PatchCatalog(new[] { f.Profile }), delegate {
                    if (++guards == 2) File.WriteAllBytes(f.Target, external);
                });
                Throws<IOException>(() => service.Change(f.DirectoryPath, true));
                Check(File.ReadAllBytes(f.Target).SequenceEqual(external), "external change overwritten");
                Check(Directory.GetFiles(f.DirectoryPath, "*.tmp").Length == 0, "staging not cleaned");
            });
            Test("locked target fails without truncating it", f => {
                using (var held = new FileStream(f.Target, FileMode.Open, FileAccess.Read, FileShare.Read))
                    Throws<IOException>(() => f.Service.Change(f.DirectoryPath, true));
                Check(File.ReadAllBytes(f.Target).SequenceEqual(f.Original), "target damaged");
            });
            Test("concurrent installer is rejected while first operation owns lock", f => {
                using (var staged = new ManualResetEvent(false))
                using (var release = new ManualResetEvent(false))
                {
                    int guards = 0; Exception failure = null;
                    var first = new PatchService(new PatchCatalog(new[] { f.Profile }), delegate {
                        if (Interlocked.Increment(ref guards) == 2) { staged.Set(); if (!release.WaitOne(10000)) throw new Exception("release timeout"); }
                    });
                    var thread = new Thread(delegate() { try { first.Change(f.DirectoryPath, true); } catch (Exception e) { failure = e; staged.Set(); } });
                    thread.Start();
                    try { Check(staged.WaitOne(10000), "staging timeout"); if (failure != null) throw failure; Throws<IOException>(() => f.Service.Change(f.DirectoryPath, true)); }
                    finally { release.Set(); Check(thread.Join(10000), "join timeout"); }
                    if (failure != null) throw failure;
                    Check(File.ReadAllBytes(f.Target).SequenceEqual(f.Current), "first install failed");
                }
            });
            Test("malformed profile rejects overlap, bounds, bad ids and hashes", f => {
                string json = new JavaScriptSerializer().Serialize(f.Profile);
                Action<Action<PatchProfile>> rejects = change => {
                    var candidate = PatchProfile.Parse(json); change(candidate); Throws<InvalidDataException>(candidate.Validate);
                };
                rejects(p => p.patches[1].fileOffset = 17);
                rejects(p => p.patches[1].fileOffset = Int32.MaxValue);
                rejects(p => p.patches[1].original = "X0");
                rejects(p => p.patches[1].id = "first");
                rejects(p => p.revisions[1].patchIds = new[] { "missing" });
                rejects(p => p.revisions[1].sha256 = p.originalSha256);
                rejects(p => p.backupFileName = "../outside.bak");
                rejects(p => p.backupFileName = "Game.dll");
            });
            Test("inspection and self-test are read-only", f => {
                Check(f.Service.Inspect(f.DirectoryPath).Revision == 0, "inspection");
                Check(f.Service.SelfTest(f.Target).StartsWith("PASS"), "self-test");
                Check(Directory.GetFiles(f.DirectoryPath).Length == 1, "read-only operation wrote files");
            });
            var catalog = PatchCatalog.Embedded(Assembly.GetExecutingAssembly());
            Check(catalog.Profiles.Length > 0, "embedded profiles missing");
            Console.WriteLine("PASS embedded production profiles validate (no copyrighted game file needed)");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
