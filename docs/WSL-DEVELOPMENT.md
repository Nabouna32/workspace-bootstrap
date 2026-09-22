# Environnement de développement WSL

WSL est l'environnement Linux complémentaire de Windows. Il n'est pas le serveur CI du poste et n'a pas vocation à reproduire toute la toolchain Windows.

La règle d'ownership est simple : Windows reste l'environnement principal ; WSL reçoit les outils qui nécessitent réellement Linux ou dont l'usage Linux apporte une valeur claire.

## Installation

Depuis Ubuntu :

    ./bootstrap/wsl/bootstrap.sh

Le bootstrap interactif installe une base Linux minimale puis permet d'ajouter, uniquement si nécessaire pour un projet Linux local :

- Node.js + pnpm/Yarn ;
- Docker Engine + Compose ;
- Playwright + navigateurs.

Android, Flutter et les outils Windows ne sont pas installés dans WSL par défaut. Ils sont gérés côté Windows lorsqu'un build local est nécessaire, ou directement par GitHub Actions pour la CI.

## Base Linux

La base WSL contient les outils génériques utiles aux workflows Linux :

- Git / Git LFS ;
- GitHub CLI ;
- Bash et outils Unix ;
- curl, wget, jq ;
- Python ;
- build-essential, Clang, CMake, Ninja ;
- ShellCheck.

Les gros SDK multiplateformes ne sont pas installés uniquement pour reproduire l'environnement des runners GitHub.

## Web / Node.js

Node.js est une option WSL, activée lorsqu'un projet doit être exécuté localement dans un environnement Linux. Les projets restent maîtres de leurs versions via leurs lockfiles.

## Android

Android est Windows-first pour le développement local. Le SDK, le JDK et l'émulateur peuvent être installés sur Windows lorsque l'on souhaite compiler/tester localement sans télécharger un gros artefact GitHub.

La CI GitHub-hosted reste indépendante de l'installation locale et exécute les builds/tests Android sur ses propres runners.

## Flutter

Flutter est prioritairement installé sur Windows pour les builds/tests locaux Android et Windows.

Une installation Flutter dans WSL n'est justifiée que pour un projet Linux/Flutter Linux spécifique.

## Playwright

Playwright est prioritairement utilisé via les projets et la CI GitHub-hosted. Une installation WSL est une option de diagnostic ou de test Linux local, pas une dépendance globale du poste.

## Docker

Docker peut être utilisé sur Windows via Docker Desktop lorsque le besoin local existe. Docker Engine natif dans WSL reste une option lorsque le projet nécessite réellement un environnement Linux.

## GitHub Actions

Le poste n'héberge plus de runner GitHub Actions persistant.

Les dépôts publics utilisent les runners GitHub-hosted. Les PR et builds ne dépendent donc jamais du PC allumé.

Les anciens scripts de runner Windows/WSL ont été supprimés de l'architecture cible.

## Permissions et reprise

Le bootstrap ne configure plus KVM pour Android ni de service runner. Les permissions système sont limitées aux composants Linux effectivement sélectionnés.

Les répertoires de staging des installations d'archives sont temporaires et doivent être nettoyés après succès ou échec.
