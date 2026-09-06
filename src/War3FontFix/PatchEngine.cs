using System;
using System.IO;

namespace War3FontFix
{
    public sealed class UnsupportedFileException : IOException
    {
        public UnsupportedFileException() : base("Unsupported Game.dll. An exact supported SHA-256 and file length are required; no changes were made.") { }
    }

    // Pure byte transformations. No disk writes, process attachment, or external profile loading.
    public static class PatchEngine
    {
        public static byte[] Restore(byte[] source, PatchProfile profile)
        {
            int revision = profile.Identify(Bytes.Hash(source), source.Length);
            if (revision < 0) throw new UnsupportedFileException();
            var result = (byte[])source.Clone();
            if (revision > 0)
                foreach (var patch in profile.Sites(revision))
                    Replace(result, patch.fileOffset, Bytes.FromHex(patch.replacement), Bytes.FromHex(patch.original));
            if (!Bytes.SameHash(Bytes.Hash(result), profile.originalSha256))
                throw new InvalidDataException("Restored file failed full SHA-256 verification.");
            return result;
        }

        public static byte[] Apply(byte[] source, PatchProfile profile)
        {
            // Restore any recognized revision first, so upgrades do not depend on edit ordering across releases.
            byte[] result = Restore(source, profile);
            foreach (var patch in profile.Sites(profile.currentRevision))
                Replace(result, patch.fileOffset, Bytes.FromHex(patch.original), Bytes.FromHex(patch.replacement));
            if (!Bytes.SameHash(Bytes.Hash(result), profile.Current.sha256))
                throw new InvalidDataException("Patched file failed full SHA-256 verification.");
            return result;
        }

        private static void Replace(byte[] target, int offset, byte[] expected, byte[] replacement)
        {
            for (int i = 0; i < expected.Length; i++)
                if (target[offset + i] != expected[i]) throw new InvalidDataException("Patch-site verification failed.");
            Buffer.BlockCopy(replacement, 0, target, offset, replacement.Length);
        }
    }
}
