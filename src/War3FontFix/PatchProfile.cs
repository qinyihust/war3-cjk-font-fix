using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace War3FontFix
{
    public sealed class PatchSite
    {
        public string id { get; set; }
        public int fileOffset { get; set; }
        public int rva { get; set; }
        public string original { get; set; }
        public string replacement { get; set; }
    }

    public sealed class PatchRevision
    {
        public int revision { get; set; }
        // Zero means the original length (schema 1 compatibility).
        public int fileLength { get; set; }
        public string sha256 { get; set; }
        public string[] patchIds { get; set; }
    }

    public sealed class PatchProfile
    {
        public int schemaVersion { get; set; }
        public string id { get; set; }
        public string gameVersion { get; set; }
        public int currentRevision { get; set; }
        public int fileLength { get; set; }
        public string originalSha256 { get; set; }
        public string backupFileName { get; set; }
        public PatchSite[] patches { get; set; }
        public PatchRevision[] revisions { get; set; }

        public PatchRevision Current { get { return revisions.Single(r => r.revision == currentRevision); } }
        public int RevisionLength(PatchRevision revision) { return revision.fileLength == 0 ? fileLength : revision.fileLength; }

        public static PatchProfile Parse(string json)
        {
            var profile = new JavaScriptSerializer().Deserialize<PatchProfile>(json);
            if (profile == null) throw new InvalidDataException("Empty patch profile.");
            profile.Validate();
            return profile;
        }

        public void Validate()
        {
            if ((schemaVersion != 1 && schemaVersion != 2) || String.IsNullOrWhiteSpace(id) || String.IsNullOrWhiteSpace(gameVersion)
                || fileLength <= 0 || currentRevision <= 0 || !Bytes.IsHash(originalSha256))
                throw new InvalidDataException("Invalid patch profile identity.");
            if (String.IsNullOrWhiteSpace(backupFileName) || backupFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || backupFileName.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || backupFileName == "." || backupFileName == ".."
                || String.Equals(backupFileName, "Game.dll", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Backup must be a distinct local file name.");
            if (patches == null || patches.Length == 0 || revisions == null || revisions.Length == 0)
                throw new InvalidDataException("Profile has no patches or revisions.");

            foreach (var revision in revisions)
                if (revision == null || revision.fileLength < 0 || (schemaVersion == 1 && revision.fileLength != 0)
                    || RevisionLength(revision) < fileLength || (long)RevisionLength(revision) > (long)fileLength + 1024 * 1024)
                    throw new InvalidDataException("Invalid revision file length.");
            int maximumLength = revisions.Max(r => RevisionLength(r));

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var ranges = new List<Tuple<int, int>>();
            foreach (var patch in patches)
            {
                if (patch == null || String.IsNullOrWhiteSpace(patch.id) || !ids.Add(patch.id))
                    throw new InvalidDataException("Duplicate or empty patch id.");
                byte[] before = Bytes.FromHex(patch.original), after = Bytes.FromHex(patch.replacement);
                if (before.Length == 0 || before.Length != after.Length || patch.fileOffset < 0 || patch.rva < 0
                    || (long)patch.fileOffset + before.Length > maximumLength)
                    throw new InvalidDataException("Patch length or file offset is invalid.");
                for (int i = 0; i < before.Length; i++)
                    if ((long)patch.fileOffset + i >= fileLength && before[i] != 0)
                        throw new InvalidDataException("Appended patch space must have a zero original image.");
                int end = patch.fileOffset + before.Length;
                if (ranges.Any(r => patch.fileOffset < r.Item2 && end > r.Item1))
                    throw new InvalidDataException("Patch sites overlap.");
                ranges.Add(Tuple.Create(patch.fileOffset, end));
            }

            var revisionIds = new HashSet<int>();
            var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { originalSha256 };
            foreach (var revision in revisions)
            {
                if (revision == null || revision.revision <= 0 || !revisionIds.Add(revision.revision)
                    || !Bytes.IsHash(revision.sha256) || !hashes.Add(revision.sha256)
                    || revision.patchIds == null || revision.patchIds.Length == 0
                    || revision.patchIds.Distinct(StringComparer.Ordinal).Count() != revision.patchIds.Length
                    || revision.patchIds.Any(p => p == null || !ids.Contains(p))
                    || patches.Where(p => revision.patchIds.Contains(p.id)).Any(p => (long)p.fileOffset + p.original.Length / 2 > RevisionLength(revision)))
                    throw new InvalidDataException("Invalid patch revision.");
            }
            if (!revisionIds.Contains(currentRevision)) throw new InvalidDataException("Current revision is missing.");
        }

        public int Identify(string hash, long length)
        {
            if (length == fileLength && Bytes.SameHash(hash, originalSha256)) return 0;
            foreach (var revision in revisions)
                if (length == RevisionLength(revision) && Bytes.SameHash(hash, revision.sha256)) return revision.revision;
            return -1;
        }

        public IEnumerable<PatchSite> Sites(int revision)
        {
            var selected = revisions.Single(r => r.revision == revision);
            foreach (string patchId in selected.patchIds)
                yield return patches.Single(p => p.id == patchId);
        }
    }

    public static class Bytes
    {
        public static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }

        public static string FileHash(string file)
        {
            using (var stream = File.OpenRead(file))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }

        public static bool SameHash(string a, string b) { return String.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
        public static bool IsHash(string value) { return value != null && value.Length == 64 && value.All(Uri.IsHexDigit); }

        public static byte[] FromHex(string value)
        {
            if (value == null || value.Length % 2 != 0 || !value.All(Uri.IsHexDigit))
                throw new InvalidDataException("Invalid hexadecimal bytes.");
            var result = new byte[value.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
            return result;
        }
    }

    public sealed class PatchCatalog
    {
        public readonly PatchProfile[] Profiles;
        public PatchCatalog(IEnumerable<PatchProfile> profiles)
        {
            Profiles = profiles.ToArray();
            if (Profiles.Length == 0) throw new InvalidDataException("No embedded patch profiles.");
            var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var profile in Profiles)
            {
                profile.Validate();
                if (!ids.Add(profile.id) || !hashes.Add(profile.originalSha256))
                    throw new InvalidDataException("Ambiguous patch profiles.");
                foreach (var revision in profile.revisions)
                    if (!hashes.Add(revision.sha256)) throw new InvalidDataException("Ambiguous revision hash.");
            }
        }

        public static PatchCatalog Embedded(Assembly assembly)
        {
            var profiles = new List<PatchProfile>();
            foreach (string name in assembly.GetManifestResourceNames().OrderBy(n => n, StringComparer.Ordinal))
                if (name.StartsWith("War3FontFix.Profiles.", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
                    using (var reader = new StreamReader(assembly.GetManifestResourceStream(name)))
                        profiles.Add(PatchProfile.Parse(reader.ReadToEnd()));
            return new PatchCatalog(profiles);
        }

        public PatchProfile Find(string hash, long length)
        {
            return Profiles.SingleOrDefault(p => p.Identify(hash, length) >= 0);
        }
    }
}
