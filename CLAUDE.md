# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Wartergen is a Windows desktop (WPF) app that automates a Warcraft III map terrain-editing workflow. It merges what used to be three separate manual tools (an MPQ archive editor, the `wc3maptranslator` JS library, and a BMP→JSON script) into a single "Draw Terrain" action: extract `war3map.w3e` from a `.w3x`/`.w3m` map, convert it to JSON, overwrite the ground texture using colors read from a `.bmp` image, convert back to binary, and repack it into the map.

## Commands

- Build: `dotnet build Wartergen.sln`
- Run the app: `dotnet run --project src/Wartergen.App` (Windows only)
- Run all tests: `dotnet test Wartergen.sln`
- Run one test project: `dotnet test tests/Wartergen.Wc3Terrain.Tests`
- Run a single test: `dotnet test tests/Wartergen.Wc3Terrain.Tests --filter FullyQualifiedName~RoundTripOracleTests.WarToJsonToWar_ProducesByteIdenticalOutput`

## Architecture

`Wartergen.sln` has four src projects plus one xUnit test project per src project:

- `src/Wartergen.App` (net9.0-windows) — WPF UI, MVVM via CommunityToolkit.Mvvm. `MainViewModel` drives `DrawTerrainWorkflow`.
- `src/Wartergen.Wc3Terrain` (net9.0, platform-independent) — binary `war3map.w3e` ⇄ `TerrainModel` JSON translator (`TerrainTranslator`, `LittleEndianReader`/`Writer`).
- `src/Wartergen.Bmp` (net9.0-windows, `System.Drawing.Common`) — reads BMP pixels and converts them into ground-texture indices (`BmpGroundTextureConverter`).
- `src/Wartergen.Mpq` (net9.0-windows) — wraps the external `MPQEditor.exe` process (`MpqEditorService`). Argument-list construction is isolated in `MpqCommandArgs`, kept separate from process invocation so it's directly testable.

The pipeline lives in `DrawTerrainWorkflow` (`src/Wartergen.App/Services/DrawTerrainWorkflow.cs`), which runs five steps in a temp directory, wrapping each step's exceptions in a `DrawTerrainWorkflowException` labeled by step number:

1. Extract `war3map.w3e` from the map via `IMpqEditorService.ExtractFileAsync`.
2. Parse it to a `TerrainModel` via `TerrainTranslator.WarToJson` (also writes a debug-only `terrain.json` breadcrumb that the app never reads back).
3. Overwrite `TerrainModel.GroundTexture` via `BmpGroundTextureConverter`: pixels are read row-major, top-left first; each unique color gets a sequential index in first-appearance order; the texture array is overwritten positionally.
4. Serialize back to bytes via `TerrainTranslator.JsonToWar`.
5. Repack into the map via `IMpqEditorService.AddOrReplaceFileAsync`.

Important: `Wartergen.Wc3Terrain` and `Wartergen.Bmp` are from-scratch C# ports, not wrappers. The app has no runtime dependency on Node or Python. `WC3Translator/` (a vendored copy of the `wc3maptranslator` npm package) and `PythonBMP2Json/bmp_to_wc3_terrain.py` exist only as behavioral references the C# code was ported from — don't shell out to them or treat them as live dependencies.

`MPQEditor/MPQEditor.exe` is the one real external-process dependency; it's copied into each consuming project's output directory via `CopyToOutputDirectory` in the `.csproj` files, and `MpqEditorService.CreateDefault()` resolves it relative to `AppContext.BaseDirectory`.

Terrain round-trip fidelity is verified in `tests/Wartergen.Wc3Terrain.Tests/RoundTripOracleTests.cs` against real fixture files in `TestData/` (`war3map.w3e`, `terrain.json`): JSON→war→JSON must be model-equivalent, and war→JSON→war must be byte-identical.
