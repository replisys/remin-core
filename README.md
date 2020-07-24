# ReMin - replicated MinWin
## A project of the _Replicated Systems Division_.

ReMin aims to replicate MinWin/bare-metal Nano Server based on current Windows bits.

![image](https://github.com/replisys/remin-core/blob/master/doc/image.png?raw=true)

**Current supported base build**: 19041.1 ("2004")

## How to run
1. Ensure you have `en_windows_server_version_2004_x64_dvd_765aeb22.iso`.
2. Build the tools using `build.cmd` in their respective directories. Yeah.
3. Install PowerShell 7 or above on modern Windows 10.
4. Make sure `V:\` and `X:\` are not currently mounted to a volume.
5. From an elevated `pwsh`, run `.\build.ps1 -IsoPath "A:\en_windows_server_version_2004_x64_dvd_765aeb22.iso" -SaveRoot "A:\OS\Save" -WorkRoot "A:\OS\Work"`, replacing paths with the path to the ISO and a save directory.
6. You should have `A:\OS\Save\OS.vhdx` if it completed successfully, which _should_ boot in Hyper-V, other hypervisors, or bare metal if applying correctly.

## Known issues
* `msxml6` will crash due to an incorrect `urlmon` stub in `kernelbase`.
* Online servicing requires patched `winload` since `tm.sys` and `clfs.sys` do not get loaded by the Microsoft-signed `ApiSetSchema.dll`, and these needs a custom API set schema extension (`TM-Extension.dll`) to load.
* First boot will have `wininit.exe` crash and error out with `CRITICAL_PROCESS_DIED`, reboot after this if not done automatically.
* The OS itself does not have a login guard and will auto-logon as SYSTEM.

## Todos
Ever-evolving.

* Apply deltas so we don't have to include pre-reverted PSFX forwarders.
* Fix LSA init bug on first boot.
* Be more build-independent.
* Automatically patch `wcp.dll` instead of pre-bundling such.
* More components that one might need/want.
  * Hyper-V host?
  * Hacks to support WAC more cleanly?
  * PowerShell bundled natively?
  * Extra CLI utilities?
  * An installer booting in WinPE mode?
  * Logon!
* Custom CUs to patch components not covered by default package.
* Better way to break CI? Have a custom root added?
* Automatic component generation instead of the current mess of slightly-hacked components?

## Fun tricks
* You can run PowerShell 7 and it'll work. Pretty helpful as many utilities for use with `cmd.exe` are missing.
* Same goes for the 19041.388 CU, the related SSU and the 20H2 enablement package. Do note that `dism /add-package` will want an extracted .cab from the .msu, and some components do _not_ get updated as they don't match original OS package composition.
* `OpenSSH-Server-Package.cab` will also install and run fine.
* `curl.exe` and `tar.exe` are installed and work just as they do on normal Windows.