# Third-Party Notices

This embedded package is derived only from the official
[`rlabrecque/Steamworks.NET`](https://github.com/rlabrecque/Steamworks.NET)
repository at commit `3c236146fe55e48eb8776a6b912a7675ef591b08`.

- Upstream package version: `2025.165.0`
- Upstream Steamworks SDK contract: `1.65`
- J2M package version: `2025.165.0-j2m.1`
- Imported managed scope: the complete upstream `Runtime` directory
- Imported native scope: `Plugins/steam_api64.dll` and its importer metadata only
- Native SHA-256: `8de54d32508e216c9135b8bf025749243d44e404c1c22a8e5fe35acecabe7a9c`

The upstream `Editor` directory is intentionally excluded because its
`RedistInstall` hook creates `steam_appid.txt` containing AppID 480 and mutates
global scripting defines. J2M provider composition uses an asmdef version define
instead and does not create an AppID file.

Non-Windows redistributables and the Windows x86 `steam_api.dll` are excluded from
this Windows x64 package. The retained native binary is byte-identical to
`steamworks_sdk_165/sdk/redistributable_bin/win64/steam_api64.dll` from the
validated Valve SDK 1.65 source used for M7B-2I.
