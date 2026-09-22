# Bouna Dev Environment — application desktop

Cette application WPF constitue l'interface graphique native de `dev-environment`.

## Principe architectural

- Le moteur PowerShell reste la source de vérité pour le provisioning, les diagnostics et les optimisations.
- L'application graphique n'implémente pas une seconde logique système : elle appelle des commandes non interactives exposées par le moteur.
- Les commandes graphiques doivent rester idempotentes, explicites et journalisées.
- L'interface est pensée pour évoluer vers un vrai dashboard : état du PC, provisioning, diagnostic, optimisation, historique et reprise après redémarrage.

## Développement local

Depuis `desktop/` avec le SDK .NET installé :

`dotnet build .\\BounaDevEnvironment.Desktop.csproj`

`dotnet run --project .\\BounaDevEnvironment.Desktop.csproj`

Le dépôt ne télécharge aucun SDK ou runtime. Le provisioning de la machine décidera officiellement de la façon de fournir le toolchain .NET nécessaire.
