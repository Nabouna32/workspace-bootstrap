# Workspace Control

Workspace Control is a local-first Windows 11 control center for understanding, configuring and maintaining a PC over time.

The product is built around **My Workspace**: a user-owned desired state describing what the user wants Workspace Control to manage.

## Core flow

**Observe → choose desired state → compare → review → confirm → apply → verify**

Interactive operations do not silently mutate the system.

## Current technology

- Windows 11 x64
- C# / .NET 10
- WinUI 3 / Windows App SDK
- Shared application/domain engine
- Windows/provider infrastructure
- .NET CLI

The desktop is self-contained, unpackaged and portable. Application-owned mutable data stays under the portable application root and does not silently fall back to AppData.

## Documentation

- [Product vision](docs/PRODUCT-VISION.md)
- [UX](docs/UX.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Portability](docs/PORTABILITY.md)
- [Decisions](docs/DECISIONS.md)
- [Status](docs/STATUS.md)
- [Roadmap](docs/ROADMAP.md)
- [Documentation index](docs/README.md)

Historical brainstorming is preserved separately:

- [BRAINSTORMING-RAW.md](BRAINSTORMING-RAW.md) — immutable archive.
- [BRAINSTORMING.md](BRAINSTORMING.md) — structured synthesis.

## Development

GitHub is the source of truth. Changes are developed on branches, validated by CI and integrated through pull requests.

See the repository and its canonical documentation for the current implementation state and development constraints.
