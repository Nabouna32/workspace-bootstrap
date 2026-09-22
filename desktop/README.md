# Workspace Bootstrap Desktop

Native Windows desktop application for Workspace Bootstrap.

## Architecture

- UI: WPF on .NET 10.
- Application logic: shared `WorkspaceBootstrap.Engine`.
- Provisioning: Engine-owned.
- CLI and desktop share the same domain and provisioning implementation.
- Local state and cache live inside the portable application package.

## Development

From the repository root:

```text
dotnet restore desktop/WorkspaceBootstrap.Desktop.csproj
dotnet build desktop/WorkspaceBootstrap.Desktop.csproj --configuration Debug
dotnet run --project desktop/WorkspaceBootstrap.Desktop.csproj --configuration Debug
```

## UX direction

- dashboard-first navigation;
- consistent cards and state indicators;
- profile/component selection;
- plan preview before mutation;
- visible progress and recovery;
- operation history;
- diagnostics and inventory;
- guarded maintenance/optimization;
- keyboard-friendly, accessible controls;
- system/light/dark theme.

The presentation layer must not duplicate Engine behavior.

## Apparence et sémantique visuelle

Le bureau propose trois modes : **Système**, **Clair** et **Sombre**.

- **Système** suit le thème des applications Windows au lancement.
- **Clair** et **Sombre** forcent la palette correspondante.
- La préférence est stockée dans `desktop-settings.json` à côté de l'application.
- vert : succès / état conforme ;
- jaune : avertissement / redémarrage requis ;
- rouge : erreur / échec ;
- bleu : information / action principale.

Le changement de thème ne modifie aucune logique du moteur.
