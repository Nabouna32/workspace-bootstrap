# Workspace Bootstrap — Windows package

Ce dossier contient les ressources déclaratives du produit Windows : catalogue des composants et profils de provisioning.

## Structure

- `components/catalog.json` : catalogue des composants disponibles ;
- `components/<id>/component.json` : manifeste de chaque composant ;
- `profiles/*.json` : profils composés de composants déclarés dans le catalogue.

Le moteur .NET charge ces ressources depuis le répertoire du package. Elles sont incluses dans les publications Windows du CLI et restent donc disponibles en mode portable.

## Provisioning

Le provisioning est orchestré exclusivement par le moteur C#/.NET 10. Les composants privilégient leurs sources officielles ; WinGet n'est utilisé qu'en fallback lorsqu'il est explicitement déclaré dans le manifeste.

Les opérations prennent en charge la planification, la vérification des artefacts, le cache local, la reprise et l'historique. Une modification destructive ou irréversible doit faire l'objet d'une confirmation explicite dans l'interface.

## Évolution

Les anciennes implémentations scriptées et les dépendances Linux/WSL ne font plus partie du produit. Toute nouvelle capacité Windows doit être intégrée au moteur .NET partagé par le bureau et le CLI.
