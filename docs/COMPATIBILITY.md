# Compatibility

The installer checks file length and SHA-256, then verifies every edited byte and the complete output hash. A matching file-version label is not sufficient.

| State | Length | SHA-256 |
|---|---:|---|
| Original 1.25.1.6397 | 12,042,240 | `F504ED38F43BCCF011BF55DC8339EFACD689F4DCD6AF4AFD1815F01B5D026D07` |
| Patch revision 1 | 12,042,240 | `7435968F413C72E78ADD12CB4F6B6CD1A050D47C142A44AEB47BF023BC237B3E` |
| Patch revision 2 — current | 12,042,240 | `77BB9CBA5EA090775D02995003BADE493707E4D987B08DF2AE93B8B0B95CEB98` |

The current patch changes 102 actual bytes across 11 regions. File size remains unchanged. The single version profile is [war3-1.25.1.6397.json](../patches/war3-1.25.1.6397.json).

Supported operations:

| Input | Install | Restore |
|---|---|---|
| Original | Apply revision 2 | No change |
| Revision 1 | Upgrade to revision 2 | Reconstruct original |
| Revision 2 | No change | Reconstruct original |
| Anything else | Reject | Reject |

The patch operates on engine code, so it has no hardcoded map path. IMBA 3.86b AI is the tested scenario. Other game builds, graphics backends, replacement fonts, multiplayer platforms, and anti-cheat integrations remain unverified.

There is no fixed 1920 × 1080 condition in the patch. Higher-resolution font rasterization creates more cache pressure; character variety, glyph widths, and fragmentation also matter. Preserving the original 32-pixel rasterization limit and atlas dimensions avoids changing the intended font metrics.

For unsupported files, include only `--check` output and version/environment details in an issue. Do not replace the accepted hash with your own hash or force the known offsets onto another binary. See [porting requirements](PORTING.md).
