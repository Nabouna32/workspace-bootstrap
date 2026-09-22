# Workspace Bootstrap Desktop

Native Windows desktop application for Workspace Bootstrap.

## Architecture

- UI: WPF on .NET 10.
- Application logic: shared `WorkspaceBootstrap.Engine`.
- Provisioning: Engine-owned; the desktop does not execute scripts.
- CLI and desktop share the same domain and provisioning implementation.
- Persistent state: `C:\Dev\WorkspaceBootstrap`.
- Installer cache: `C:\DevCache`.

## Development

From the repository root:

```text
dotnet restore desktop/WorkspaceBootstrap.Desktop.csproj
dotnet build desktop/WorkspaceBootstrap.Desktop.csproj --configuration Debug
dotnet run --project desktop/WorkspaceBootstrap.Desktop.csproj --configuration Debug
```

## UX direction

The desktop is a real Windows 11 management application:

- dashboard-first navigation;
- consistent cards and state indicators;
- profile/component selection;
- plan preview before mutation;
- visible progress and recovery;
- operation history;
- diagnostics and inventory;
- guarded maintenance/optimization;
- keyboard-friendly, accessible controls;
- dark/light-ready design tokens.

The presentation layer must not duplicate Engine behavior.

## Apparence et sémantique visuelle

Le bureau propose trois modes persistants : **Système**, **Clair** et **Sombre**.

- **Système** suit le thème des applications Windows au lancement.
- **Clair** et **Sombre** forcent la palette correspondante.
- La préférence est stockée dans `C:\Dev\WorkspaceBootstrap\desktop-settings.json`.
- Les couleurs sont sémantiques et ne servent pas uniquement à décorer l'interface :
  - vert : succès / état conforme ;
  - jaune : avertissement / redémarrage requis ;
  - rouge : erreur / échec ;
  - bleu : information / action principale ;
  - texte secondaire : métadonnées et informations non critiques.
- Les mêmes rôles de couleur sont utilisés par les vues de provisioning afin qu'un état important reste identifiable sans dépendre uniquement du texte.

Le changement de thème ne modifie aucune logique du moteur et ne nécessite aucun redémarrage.
