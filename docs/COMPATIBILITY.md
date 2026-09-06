# Compatibility

The installer checks full file length and SHA-256, every edited byte, and the complete output hash. A matching version label is not sufficient. Installer 2.1.0 contains the following eight revision-2 profiles.

| Label | File version | Original bytes | Patched bytes | Placement |
|---|---|---:|---:|---|
| 1.20e | [1.20.4.6074](../patches/war3-1.20.4.6074.json) | 9375804 | 9375804 | text-tail |
| 1.21 | [1.21.0.6263](../patches/war3-1.21.0.6263.json) | 9375804 | 9383936 | new-section |
| 1.22 | [1.22.0.6328](../patches/war3-1.22.0.6328.json) | 11956224 | 11956224 | text-tail |
| 1.23 | [1.23.0.6352](../patches/war3-1.23.0.6352.json) | 12062720 | 12062720 | text-tail |
| 1.24b | [1.24.1.6374](../patches/war3-1.24.1.6374.json) | 12140544 | 12140544 | text-tail |
| 1.24e | [1.24.4.6387](../patches/war3-1.24.4.6387.json) | 12140544 | 12144640 | new-section |
| 1.25b | [1.25.1.6397](../patches/war3-1.25.1.6397.json) | 12042240 | 12042240 | text-tail |
| 1.26 | [1.26.0.6401](../patches/war3-1.26.0.6401.json) | 12042240 | 12042240 | text-tail |

## Exact accepted fingerprints

### 1.20e (1.20.4.6074)

- Original: `6B5B406861A56449C604C15CF0971DF7D6D26F679EFF7AA69A34946C7D91764B`
- Revision 2: `4740FD87318724ECA050D18B5F2B071E7AE6DB97783D3FF4292275EBD5B55942`

### 1.21 (1.21.0.6263)

- Original: `3265E78DD68B1AB6C54957409E472C077F9FE2ABCCE4CFD279E56AF66A6EF6F3`
- Revision 2: `8554C02E37DD02E5FC95A00055D8CF73BD48B127B6511C703E75B31DD2221E58`

### 1.22 (1.22.0.6328)

- Original: `5F866BD8A45951CD2C4B22B1C80A0329246DBED847540C85430376E8C4310C84`
- Revision 2: `BDB926D9B01189FD329E9814B7F5A110E68C3CB6302C264790431DB78E06A27C`

### 1.23 (1.23.0.6352)

- Original: `EE6175947A8792CD672253B37DEB4C5D0577E4E145FFD90FED9021C64C31AE1E`
- Revision 2: `529734F41002F4FDD372C272F58EF38E6982EB7FA284B9B9913ECB6A3446DF2C`

### 1.24b (1.24.1.6374)

- Original: `EE23DD7795EA82DDFC292FDD2A05D5A76D3ABEF801CEC01F0EFF68F521DF0441`
- Revision 2: `1A615BA5FF62B18626CF22091FB9721C3D13A39B0FC60AA38E3C4B352210E6FC`

### 1.24e (1.24.4.6387)

- Original: `5F7D8B25005BC12F8453839474EB37ADE8869ABA0570EAD2B76B6A7979C904CC`
- Revision 2: `FD9773CAC34EB6BA2200823B9E9ECBD447177C8FE330844C0EC1D54B84FA5423`

### 1.25b (1.25.1.6397)

- Original: `F504ED38F43BCCF011BF55DC8339EFACD689F4DCD6AF4AFD1815F01B5D026D07`
- Revision 1: `7435968F413C72E78ADD12CB4F6B6CD1A050D47C142A44AEB47BF023BC237B3E`
- Revision 2: `77BB9CBA5EA090775D02995003BADE493707E4D987B08DF2AE93B8B0B95CEB98`

### 1.26 (1.26.0.6401)

- Original: `6D21BD9A0F9FBC8446F455C9E89AC994FED68174426FE608FCB9BAEFD4DEC53C`
- Revision 2: `242E412E56A9E8D6285F130C51DDECD1DBAA5BC9DE2E37277F20ACFD02E26C0B`

## Operations and limits

| Input | Install | Restore |
|---|---|---|
| Exact supported original | Apply revision 2 | No change |
| 1.25b revision 1 | Upgrade to revision 2 | Reconstruct original |
| Exact supported revision 2 | No change | Reconstruct original |
| Anything else | Reject | Reject |

1.21 and 1.24e append a read/execute `.cjk` section because their executable tail has insufficient space. Restoration removes only the fully fingerprinted generated extension and restores the original headers, length, overlay, and SHA-256. Other builds retain their original length. The 1.25b revision-2 DLL is identical to the v2.0.0 release.

The shared generator uses two reviewed compiler families and per-build layouts. It is not an installer that guesses offsets in arbitrary DLLs. Version converters replace game files and therefore remove this patch: exit the game, convert, then run the installer again. Each profile keeps a separate original backup.

The patch has no hardcoded map or display-resolution condition. Higher-resolution rasterization increases cache pressure; glyph variety and fragmentation also matter. Atlas dimensions and the original 32-pixel rasterization limit remain unchanged. Map size and scripting-version restrictions are separate from font support.

See [validation](VALIDATION.md) for actual test coverage. Only 1.25b has the recorded 45-minute IMBA session; short runtime pressure checks of other builds do not establish long-session stability. Other graphics backends, replacement fonts, multiplayer platforms, and anti-cheat integrations remain unverified.

For unsupported files, include only `--check` output and environment details in an issue. Do not force another hash or known offsets onto an unknown binary. See [porting requirements](PORTING.md).
