# Windows bootstrap

Le bootstrap Windows prépare les capacités natives Windows et WSL.

## Entrée principale

    .\bootstrap\windows\dev-env.ps1

Le menu interactif propose Base, Développement, Développement étendu, Gaming, Vérification, Réparation, Optimisation et Maintenance.

## Arborescence locale

Le bootstrap utilise `C:\\dev` comme racine de développement :

- `C:\\dev\\Projects` : dépôts Git et projets ;
- `C:\\dev\\SDK` : SDK locaux, dont `C:\\dev\\SDK\\flutter` ;
- `C:\\dev\\Tools` : outils portables ou outillage local lorsqu'un composant en a besoin.

Les applications installées par WinGet restent dans leurs emplacements système officiels. Le SDK Android reste géré exclusivement dans WSL (`~/Android/Sdk`).

## Développement Windows

Le profil développement installe GitHub CLI, Visual Studio Build Tools, WSL2/Ubuntu, Flutter pour Windows, PowerToys, Everything et ShareX.

Le profil développement étendu ajoute VS Code, IntelliJ IDEA Community, Python, Rust, Go, LLVM/Clang et JDK 21 pour les projets plus larges, notamment le modding Minecraft.

Visual Studio Build Tools utilise config/windows/buildtools.vsconfig. Cette configuration définit la toolchain C++ nécessaire : MSVC x64/x86, CMake, outils de test et Windows SDK.

## Maintenance et optimisation

Maintenance.ps1 fournit le centre interactif Windows Update, DISM/SFC, Component Store, stockage, santé WSL/Docker/Android/Flutter, réseau et rapport diagnostic.

Optimization.ps1 fournit Safe, Advanced, Aggressive et le debloat AppX par allowlist explicite. Les changements réversibles sont enregistrés pour rollback.

## Frontière Windows / WSL

Android n'est pas installé par le bootstrap Windows.

Le SDK Android, l'émulateur, Java Android/Gradle, Flutter Android, Node.js, Playwright et Docker sont gérés dans WSL.

Flutter Windows reste natif Windows afin de produire et tester l'application Windows avec la toolchain Windows.

## Provisioning

Le provisioning est online-first. Il n'existe pas de cache d'artefacts personnalisé dans le bootstrap.

## Logs

Chaque exécution crée un journal sous bootstrap\windows\logs\<run-id>\. Les erreurs restent visibles.
