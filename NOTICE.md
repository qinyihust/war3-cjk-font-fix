# Third-party notice

Warcraft III, The Frozen Throne, game artwork, fonts, maps, and their trademarks belong to their respective owners. This independent project is not affiliated with or endorsed by Blizzard Entertainment. Its MIT license does not license the game or third-party assets.

The repository distributes patch definitions and an installer, not complete game DLLs, maps, fonts, replays, or memory dumps. Screenshots document observed rendering behavior and remain subject to the underlying artwork's rights.

The installer uses .NET Framework components supplied by Windows. No third-party runtime libraries are bundled. Optional disassembly tools use `capstone` and `pefile` under their own licenses.

External references informed the investigation, not the supported binary's offsets:

- [WarcraftHelper](https://github.com/LoveBeforT/WarcraftHelper): comparison with the window-refresh workaround.
- [whoahq/whoa font-engine reimplementation](https://github.com/whoahq/whoa/tree/master/src/gx/font): structural comparison for related font-cache concepts. This is not Warcraft III's official source or debug symbols.

The patch addresses and failure observations come from the locally verified Warcraft III binary. These referenced projects are not runtime dependencies, and their source code is not bundled into the installer.
