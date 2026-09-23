# Configuration

Product settings and desired-state profiles are separate concepts.

## Product settings
Theme, language, interface level, logging, local installer retention, notifications and provider preferences are local preferences. They never silently authorize dangerous system mutations.

## Profiles
Profiles are versioned declarative desired-state documents containing applications, Windows policies/settings, drivers, WSL, optimizations and conditions.

## Portable configuration
Profiles can be imported/exported. Exports contain no secrets.

## Cloud-ready
The local engine treats a profile as data regardless of whether it originated locally, from an import or from future cloud synchronization.