# Steamworks.NET Adapter

This optional package is the only production assembly allowed to reference
Steamworks.NET types. Its assembly is disabled unless the verified internal package
`com.j2m.thirdparty.steamworksnet` is present at version `2025.165.0-j2m.1`.

That internal package contains the complete managed Runtime from official upstream
commit `3c236146fe55e48eb8776a6b912a7675ef591b08` (Steamworks.NET `2025.165.0`,
Steamworks SDK `1.65`) and exactly one native payload: the Valve-signed Windows x64
`steam_api64.dll`. The upstream Editor automation and non-Windows/x86 native payloads
are deliberately excluded so dependency installation cannot create AppID 480,
mutate global scripting defines, or activate a native plugin on the wrong target.

The adapter registers the Steam factory when this assembly is compiled. It does not parse
generic provider arguments or decide fallback. The always-compiled Foundation owns
`-j2mPlatformProvider`, keeps Local as the default only when no provider was requested, and
resolves this factory only for an explicit `steam` request. Package or DLL presence alone
therefore never changes the Local provider default.

Each selected runtime creates one `SteamworksNetNativeApi` instance. That instance
implements both the lifecycle interface and the narrow achievement transport interface.
It translates `UserStatsStored_t` and `UserAchievementStored_t` into Steamworks-free,
privacy-bounded observations. Both handles are registered transactionally and are driven
only by the existing runtime-owned `SteamAPI.RunCallbacks` pump.

The lifecycle interface exposes pack-size preflight, initialization, callback pumping, shutdown,
AppID, boolean identity validity, and login queries. It does not expose Overlay or DLL diagnostic
observations. Removing those observations does not disable Steam Overlay or alter the vendored
SDK/native payload. The achievement callback pair and both observation DTOs retain their transport
contract; product publication does not use unnamed stats results to decide any outcome.

When this assembly is excluded because the verified dependency is absent, Foundation still
preserves an explicit `steam` request and reports `RequestedProviderNotRegistered`; Local is
not selected. The adapter does not call `RestartAppIfNecessary`, create `steam_appid.txt`, or
change scripting defines/ProjectSettings.
