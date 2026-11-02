# 0001. One repository, a shared C# core, an independent Raycast extension

Status: accepted, 2026-11-01

## Context

Ludex began as a Flow Launcher plugin and a separate Raycast extension, with the same purpose and
diverging behaviour (`<root>` resolution, unmatched games, aliases). A PowerToys Command Palette version
was wanted next. Flow and Command Palette are both C#; Raycast is TypeScript on Node.

## Decision

- One repository containing all three front ends.
- The detection, manifest, matching and path-resolution code lives in `src/Core` and is shared by the two
  C# front ends.
- The Raycast extension lives in `raycast/` and does not share code.

## Consequences

- A fix to the matcher or a detector reaches Flow and Command Palette at once.
- Core must stay free of launcher types and trim-safe, because the Command Palette extension publishes trimmed.
- The Raycast version can drift from the C# behaviour; the known differences are listed in
  [architecture](../architecture.md).
- Flow and Command Palette keep separate caches and settings, because the Command Palette extension is a
  packaged app with its own data folder.

## Alternatives considered

- **Three repositories.** Rejected: two copies of the C# pipeline would diverge, as the first two already had.
- **One implementation for all three.** Rejected: Raycast would have to call a C# binary, which complicates
  its store publishing, and each front end would still need its own UI.
- **Raycast in a separate repository.** Viable, but there was no shared code to justify splitting and a single
  README covers all versions.
