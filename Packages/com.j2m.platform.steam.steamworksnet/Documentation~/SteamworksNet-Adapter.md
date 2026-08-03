# Steamworks.NET Adapter

This optional package is the only production assembly allowed to reference
Steamworks.NET types. Its assembly is disabled unless the verified internal package
`com.j2m.thirdparty.steamworksnet` is present at version `2025.165.0-j2m.1`.

The adapter registers the Steam factory when this assembly is compiled. It does not parse
generic provider arguments or decide fallback. The always-compiled Foundation owns
`-j2mPlatformProvider`, keeps Local as the default only when no provider was requested, and
resolves this factory only for an explicit `steam` request. Package or DLL presence alone
therefore never changes the Local provider default.

When this assembly is excluded because the verified dependency is absent, Foundation still
preserves an explicit `steam` request and reports `RequestedProviderNotRegistered`; Local is
not selected. The adapter does not call `RestartAppIfNecessary`, create `steam_appid.txt`, or
change scripting defines/ProjectSettings.
