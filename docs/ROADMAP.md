# Roadmap — Dev Environment

Last updated: 2026-09-22

Ce fichier est la roadmap produit durable de l'environnement. Il reste dans GitHub afin que l'intention produit survive aux changements de conversation.

## Vision produit

dev-environment n'est pas seulement un installateur. C'est un gestionnaire d'environnement Windows 11 + WSL2 : provisionner, détecter les dérives, réparer, mettre à jour, diagnostiquer et optimiser un PC polyvalent.

Le produit doit aussi rester une base technique évolutive : lorsqu'une meilleure approche d'architecture, d'outillage, de sécurité ou d'expérience développeur apparaît, elle doit pouvoir être proposée et intégrée si elle reste cohérente avec la vision.

Objectifs :
- provisioning reproductible ;
- détection et réparation de drift ;
- mises à jour via les mécanismes officiels ;
- centre de maintenance interactif ;
- optimisation générale des performances du PC, pas uniquement du gaming ;
- debloat contrôlé jusqu'au niveau agressif ;
- profils gaming et développement étendu ;
- extensibilité pour de futurs projets, notamment le modding Minecraft ;
- UX claire et prévisible ;
- architecture capable d'accueillir de nouveaux toolchains sans devenir un monolithe.

## Principe de direction technique

Le produit n'est pas figé par une liste de tâches initiale. Le user fixe les objectifs produit et les contraintes ; l'assistant peut proposer une meilleure direction technique ou produit lorsqu'elle apporte une amélioration concrète.

Une proposition importante doit préciser :
1. le problème qu'elle résout ;
2. le bénéfice attendu ;
3. le coût/risque ou la complexité ajoutée ;
4. pourquoi elle reste cohérente avec la vision.

Les décisions irréversibles, coûteuses ou fortement orientées UX restent à arbitrer avec le user. Les décisions techniques routinières et réversibles peuvent être prises directement.

## Product identity and naming

- Product-facing name: **Dev Environment**.
- Canonical technical identity: **DevEnvironment**.
- GitHub organization: **NabounaLab** only; it is not part of the product namespace, AppData path or user-facing identity.
- Do not introduce new `Bouna*` or `Nabouna*` product identifiers.
- Canonical Windows per-user data root: `%LOCALAPPDATA%\\DevEnvironment`.
- Historical names/paths must be migrated deliberately and never cause user-data loss.

## Strategic execution architecture

- **C#/.NET is the strategic application and orchestration language.**
- WPF remains the current Windows-native presentation layer.
- C# owns application/domain/job/execution orchestration; Windows APIs, SQLite, Registry, package managers, WSL and external tools are infrastructure boundaries.
- PowerShell is a transitional compatibility/execution backend. Do not deepen accidental PowerShell coupling when a maintainable C# implementation is appropriate.
- Migrate incrementally behind explicit interfaces; do not perform a big-bang rewrite or duplicate installer implementations.
- C++ is reserved for concrete native requirements such as drivers, native ABI dependencies or capabilities unavailable through .NET.
- Rust is a valid option for isolated memory-safe native/system helpers, but is not the strategic replacement for the C# application core.
- Introducing C++ or Rust requires a concrete technical requirement and a stable boundary, not a language preference.
- Current strategic decision: **C#/.NET first; native code only at justified boundaries**.

### Pourquoi C#/.NET

Aucune autre technologie n'est actuellement un meilleur choix global pour ce produit Windows-first. C#/.NET fournit une pile cohérente pour WPF, SQLite, APIs Windows, Registry, processus/CLI, contrats JSON, jobs persistants, récupération après reboot et intégration des package managers. Rust est intéressant pour des helpers natifs memory-safe et C++ pour certains besoins ABI/driver, mais ni l'un ni l'autre ne justifie un remplacement du cœur applicatif C#.

## Décisions verrouillées

| Domaine | Décision |
|---|---|
| Windows Update | Diagnostic/statut + ouverture/lancement explicite de Windows Update ; pas d'automatisation silencieuse |
| Debloat | Niveau agressif disponible, mais explicite, documenté et ciblé |
| Optimisation | Performance générale du PC ; gaming comme profil complémentaire |
| Développement | Profil Development Extended, avec outillage futur Minecraft/modding |
| Maintenance | Centre de maintenance complet et interactif |
| Architecture | Modèle déclaratif composants/profils + états pilotant les actions |
| Provisioning | Source officielle fournisseur + cache local durable des installateurs |
| CI | GitHub-hosted standard runners for public repositories; local builds remain available on Windows |
| Source de vérité | GitHub |
| Direction technique | Initiative tech lead autorisée pour les choix techniques et les améliorations de produit à faible risque |

## État actuel

### Déjà opérationnel

- [x] Orchestrateur Windows interactif avec reprise après interruption
- [x] Profils Base / Development / Development Extended / Gaming
- [x] Plan de composants avec dépendances et détection d'état
- [x] États MISSING / OUTDATED / CURRENT / INSTALLED / CONFIG-INCOMPLETE / REPAIRABLE / FAILED
- [x] Provisioning online-first avec staging temporaire uniquement pour les archives
- [x] Update manager WinGet + Flutter
- [x] Official-vendor installer cache with verified immutable entries
- [x] Portable cache export and explicit cache-only restore
- [x] Maintenance Windows interactive
- [x] Diagnostic/lancement Windows Update
- [x] DISM / SFC / Component Store
- [x] Nettoyage stockage/temporaire
- [x] Diagnostic/réparation réseau avec confirmation
- [x] Rapport diagnostic local
- [x] Santé WSL / Docker / Android / Flutter
- [x] Optimisation Safe / Advanced / Aggressive
- [x] État persistant et rollback des optimisations réversibles
- [x] Plan Hautes performances enregistré/restaurable
- [x] Debloat AppX agressif par allowlist explicite
- [x] Java 21 disponible pour le modding tout en gardant Java 17 par défaut Android/Flutter
- [x] CI GitHub-hosted pour ce dépôt


### Application desktop

- [x] Première application WPF native : dashboard, navigation et baseline réel
- [x] Commandes moteur non interactives pour baseline et optimisation Safe
- [x] Actions Safe / rollback accessibles depuis l'interface
- [x] Contrat moteur desktop JSON versionné + modèles C# typés
- [x] Première séparation MVVM du shell desktop
- [x] Contrat desktop structuré pour les profils et le plan de provisionnement
- [x] Opération de provisionnement asynchrone avec état, progression, reprise et redémarrage requis
- [ ] Écran provisioning avec profils et dry-run graphiques
- [ ] Écran diagnostic avec rapports structurés et niveaux de gravité
- [ ] Écran optimisation avec catalogue, détails, plan, confirmation et drift
- [x] Historique des opérations
- [ ] Détails d'opération et logs filtrables
- [x] Queue persistante SQLite et orchestration par capacités — foundation
- [x] Reprise automatique — foundation/reconciliation
- [x] Jobs indépendants avec prérequis/capacités et verrous de ressources — foundation
- [ ] Migration progressive du provisioning vers le scheduler
- [x] Détection de reprise au démarrage et reprise explicite d'opération
- [ ] Notifications utilisateur riches pour redémarrage et reprise
- [ ] État global de l'environnement : Windows / WSL / Git / VS / Flutter / Android
- [ ] Packaging Windows propre et installation/désinstallation reproductible
- [ ] Tests UI automatisés sur GitHub-hosted Windows runners when the desktop test surface is ready

### À faire ensuite

## Phase 2 — PC Management Center v2

- [ ] Baseline matériel : CPU, RAM, GPU, stockage, firmware/driver signalement
- [x] Baseline logicielle : startup, espace disque et état matériel de base
- [ ] Baseline enrichie : services utiles et températures si exposées proprement
- [ ] Plan d'optimisation prévisualisable avant application
- [ ] Affichage de l'impact, du risque, du besoin de redémarrage et de la réversibilité
- [x] Vérification post-optimisation
- [x] Détection de dérive après modification manuelle
- [ ] Inspection startup/background sans suppression automatique
- [ ] Politique Game Mode / capture documentée et vérifiable
- [ ] Maintenance réseau enrichie : DNS, routes, interfaces, connectivité locale/internet
- [ ] Rapport diagnostic enrichi avec versions et états des composants
- [ ] Export diagnostic lisible pour un futur support/issue GitHub

## Phase 3 — Development Extended

- [x] Python
- [x] Rust / Cargo
- [x] Go
- [x] LLVM / Clang
- [x] VS Code / IntelliJ IDEA
- [x] Java 21
- [ ] Gradle + diagnostics orientés modding
- [ ] JDK/toolchain matrix pour Minecraft
- [ ] Helpers génériques de projets sans couplage à un seul framework
- [ ] Profils de projet pour Flutter / Next.js / Node / Python / Rust / Go / modding

## Phase 4 — Gaming

- [x] Steam / Prism Launcher de base
- [ ] Profil gaming plus complet
- [ ] Réglages performance partagés avec le moteur d'optimisation
- [ ] Game Mode / Game Bar / politique de capture
- [ ] Diagnostics GPU/pilotes sans remplacement dangereux
- [ ] Maintenance gaming et stockage
- [ ] Baseline/benchmark lorsque la mesure est fiable

## Phase 5 — Moteur déclaratif mature

### Job orchestration foundation

- [x] JobStore SQLite persistant
- [x] Job lifecycle et récupération des jobs interrompus
- [x] Capability resolver : requires / provides
- [x] Reboot-aware scheduling foundation
- [x] Resource locks filtering foundation
- [x] Resource policies : pause/limit/concurrency foundation
- [ ] Distinction pause / annulation / retry / reprise et raisons d'attente explicites
- [x] Contrôle de débit : contract/foundation, enforcement only for controlled download paths
- [ ] Migration du provisioning existant vers le job scheduler sans perte de reprise

- [ ] Validation stricte du schéma des manifests
- [ ] Capacités et ownership explicites : Windows / WSL / user / admin
- [ ] Actions de réparation standardisées
- [ ] Checks de diagnostic standardisés
- [ ] plan / dry-run avant modification
- [ ] Impact/risque/redémarrage dans chaque plan
- [x] État transactionnel de chaque optimisation réversible
- [ ] Détection CI des composants/docs morts
- [ ] Tests d'idempotence et de reprise renforcés

## Phase 6 — Extensibilité

Préparer l'architecture pour Minecraft/modding, C/C++, game dev, émulateurs/appareils et nouveaux workflows CI/E2E sans transformer le dépôt en monolithe de scripts.

Pistes :
- toolchains Minecraft par version ;
- Java/Gradle configurables par projet ;
- CMake/Ninja/LLVM pour C/C++ ;
- outils de modding et mapping sans imposer un launcher ;
- diagnostics Android/ADB plus riches ;
- profils de projets utilisant WSL pour les workflows Linux-first ;
- éventuellement une interface TUI/GUI si le moteur CLI est suffisamment stable.

## Règles d'ingénierie

1. Cache local des installateurs officiels, avec fallback WinGet explicite.
2. Ne jamais masquer une erreur pour rendre CI verte.
3. Pas de suppression aveugle de services, tâches planifiées ou paquets système.
4. Le debloat agressif utilise une allowlist explicite et affiche l'impact avant exécution.
5. Toute modification réversible conserve son état précédent.
6. Toute modification irréversible est clairement signalée et demande confirmation.
7. Les mécanismes officiels restent propriétaires des installations et mises à jour.
8. Les tests valident l'architecture autant que la syntaxe.
9. GitHub reste la source de vérité.
10. Une optimisation ne doit être ajoutée que si son effet est compréhensible, mesurable ou raisonnablement documenté ; pas de « FPS boost » magique.
11. La direction technique peut évoluer lorsque les faits, l'écosystème ou les contraintes du projet montrent une meilleure solution.


## Product backlog — durable ideas captured from design discussions

The following ideas are intentionally recorded here even when implementation is deferred. They must not disappear from conversation history.

### Package removal / debloat
- [ ] First-class remove/uninstall job type using the same persistent scheduler as installation/update.
- [ ] Inventory installed software/packages with source, scope (user/system), version and ownership.
- [ ] Distinguish **uninstall** (remove a known component) from **debloat** (remove optional/non-essential components according to an explicit policy).
- [ ] Preview removal plans before execution, including dependencies, impact, reboot requirement and reversibility.
- [ ] Safe default: never remove core Windows components or required development/runtime capabilities implicitly.
- [ ] Explicit allowlist/denylist policy for aggressive debloat; Microsoft Store remains protected unless the user explicitly changes policy.
- [ ] Capability-aware removal: removing a provider capability must surface which desired-state jobs/configuration entries would become unsatisfied.
- [ ] Detect whether a package was installed by the product, by another package manager, or manually; never assume ownership from name alone.
- [ ] Use official package-manager uninstall mechanisms where available (WinGet/MSI/AppX/installer-specific uninstallers).
- [ ] Persist uninstall outcome, exact package/version/source, logs and correlation IDs in SQLite history.
- [ ] Handle uninstall failures/reboots/recovery as normal job states rather than hiding them.
- [ ] Support reinstall/restore through desired-state configuration when a removed component is still declared as desired.

### Queue / scheduler
- [ ] Atomic job claiming and transactional resource-lock acquisition.
- [ ] Scheduler execution loop with worker lifecycle and persisted transitions.
- [ ] Capability evidence with source, observed time and optional version/details.
- [ ] Verify provided capabilities after successful execution before unblocking dependents.
- [ ] Dynamic job creation when a job discovers additional required work.
- [ ] Safe retry policy with explicit retry limits/backoff and idempotency rules.
- [ ] Separate pause, cancellation, failure, interruption, recovery and reboot-required semantics.
- [ ] Scheduler notifications for blocked, waiting, recoverable and reboot-required jobs.
- [ ] Queue-wide and per-job pause/resume controls.
- [ ] Resource budgets: bandwidth, concurrent downloads, installer concurrency and scheduled download windows.
- [ ] Safe handling of app close/crash/Windows restart without falsely reporting failed work.
- [ ] Persistent authoritative job/run history in SQLite, with detailed installer logs kept separately.
- [ ] Operation correlation IDs linking imported configuration, jobs, runs and logs.
- [ ] Concurrency tests for SQLite state transitions and lock ownership.

### Portable machine restore
- [ ] Desired-state import/export with schema/version compatibility and migration.
- [ ] Desired-vs-actual diff before execution, with preview and explicit conflicts.
- [ ] Component ownership/capability model covering Windows, WSL, user scope and administrator scope.
- [ ] Machine-specific values kept separate from portable desired configuration.
- [ ] Fresh-machine bootstrap mode that can rebuild a known development environment from JSON.
- [ ] Exact version resolution and source/artifact provenance persisted in job/run history.
- [ ] No silent substitution when a requested version is unavailable.
- [ ] Secure secret references; never put credentials/tokens in ordinary JSON or logs.
- [ ] Optional encrypted local configuration backup.
- [ ] Account synchronization only after the local model is stable; account is a sync/backup layer, never the machine-state authority.
- [ ] Multi-machine configuration history and restore.

### Download / network UX
- [ ] Global network policy UI: unlimited / limited / paused.
- [ ] Controlled download throttling where the application owns the download stream.
- [ ] For external package managers/installers, pause before start rather than pretending to throttle their internal transfer.
- [ ] Safe pause points instead of forcibly killing installers.
- [ ] Download queue visibility: current transfer, waiting downloads, estimated state when measurable.

### Diagnostics / UX
- [ ] Dedicated queue center with filters, search, status explanations and job detail.
- [ ] Human-readable explanation for every non-running job.
- [ ] Structured logs with safe viewer, filtering and correlation to jobs/runs.
- [ ] Reboot notification and post-reboot automatic reconciliation.
- [ ] Global environment health dashboard: Windows / WSL / Git / Visual Studio Build Tools / Windows SDK / Node / Java / Flutter / Android.
- [ ] FR/EN/system-language localization with English engine/operator messages.
- [ ] Accessibility and keyboard navigation for the desktop application.
- [ ] Exportable diagnostics bundle without secrets.
- [ ] Crash/recovery telemetry stored locally and privacy-safe.

### Developer workstation scope
- [ ] First-class Windows development stack: Visual Studio Build Tools, MSVC, MSBuild, CMake integration, Windows SDK and the local toolchains needed for practical Android/Windows/web builds.
- [ ] Minimal first-class WSL environment for Linux-first tools and workflows.
- [ ] Keep ownership boundaries explicit and avoid duplicating large toolchains between Windows and WSL without a concrete local need.

- [ ] Keep local build capability so large Android/Windows artifacts do not need to be downloaded from GitHub for every iteration.
- [ ] Restore comprehensive GitHub-hosted public CI coverage, including E2E and platform builds where useful.


### Public product / account
- [ ] Public-release readiness review before exposing account features.
- [ ] Optional authenticated account for encrypted configuration backup/synchronization.
- [ ] Device registration and multi-machine configuration views only after local-first architecture is proven.
- [ ] No backend/cloud dependency for the core bootstrap product.
- [ ] Privacy/security review before any cloud synchronization.
- [ ] Clear import/export ownership and recovery semantics for account disconnect/offline use.

## Portable configuration / fresh-machine bootstrap

- [ ] Versioned configuration bundle schema
- [ ] Per-component version selection: latest / exact / minimum / range / channel
- [ ] Version conflict detection without silent version substitution
- [ ] JSON export of portable desired configuration
- [ ] JSON import with schema validation and preview
- [ ] Desired-vs-actual diff and machine-specific reconciliation
- [ ] Generate persistent independent jobs from imported configuration
- [ ] Import operation ID linked to job history and logs
- [ ] Idempotent restore/convergence across fresh Windows installations
- [ ] Secure secret references; never place credentials in ordinary JSON
- [ ] Optional encrypted configuration backup
- [ ] Authenticated account synchronization for encrypted configuration and non-secret preferences
- [ ] Multi-machine configuration history and restore


### Inventory / installed software intelligence
- [ ] Read-only inventory engine covering supported Windows installation sources (Registry uninstall entries, WinGet, MSI/AppX where appropriate) without claiming literal completeness.
- [ ] Canonical inventory model: identity, display name, version, source/provider, scope, install location, ownership, capabilities, evidence and detection timestamp.
- [ ] Provider-based inventory architecture so Windows, WSL and future ecosystems can expose facts without duplicating ownership logic.
- [ ] Deduplicate cross-provider observations while preserving provider/source provenance.
- [ ] Preserve all installed versions for versioned families such as Java/JDK, .NET SDK, Node, Python, Flutter and Android SDK.
- [ ] Distinguish **installed**, **active**, **used/required**, **older version**, **unused detected**, **orphan candidate** and **unknown**; never equate installed with unused.
- [ ] Evidence-based usage/dependency detection (PATH, JAVA_HOME/toolchain pointers, known project/config references where explicitly enabled); do not infer usage from names alone.
- [ ] Desired-vs-actual comparison that respects explicit version policies and prevents cleanup recommendations for components still required by desired configuration.
- [ ] Cleanup recommendation engine: propose old/orphan candidates only when a newer version exists, no supported usage/dependency is detected, no desired-state requirement exists, ownership is sufficiently known and an official removal mechanism is available.
- [ ] Explain every cleanup recommendation with evidence and confidence; recommendations are never automatic deletion.
- [ ] Generate `package.remove` jobs from an approved cleanup plan, then rescan and verify the postcondition.
- [ ] Inventory snapshots/history and correlation with queue jobs without mixing observation data with authoritative desired state.
- [ ] Respect Windows/WSL ownership boundaries: a Windows inventory must not silently manage WSL-owned Node/Java/Android/Flutter components.


### Inventory foundation implemented
- [x] Canonical inventory models with provider provenance, ownership, scope, evidence and removal mechanism.
- [x] Read-only Windows Registry uninstall provider covering 32-bit and 64-bit views.
- [x] Inventory normalization/deduplication foundation.
- [x] Version-family cleanup analysis foundation with explicit approval requirement.
- [x] Initial WPF inventory page and manual rescan action.
- [ ] WinGet/AppX inventory providers and broader toolchain-specific providers.
- [ ] Evidence-based active/required detection and richer version-family analysis.

## Execution core migration

- [x] Establish a neutral C#/.NET execution boundary for provisioning via `IProvisioningBackend`.
- [x] Keep the existing PowerShell provisioning engine behind a compatibility backend without duplicating installer logic.
- [x] Persist external provisioning operation IDs before long-running polling.
- [x] Reconnect/resume external provisioning operations through the persistent job context.
- [ ] Introduce a validated executor registry for persistent job types instead of ad-hoc executor wiring.
- [ ] Add first-class executor contracts for progress/state persistence, cancellation and recovery semantics.
- [ ] Migrate individual provisioning responsibilities from PowerShell to C# implementations only when equivalent verification/recovery exists.
- [ ] Retire obsolete PowerShell paths only after their C# replacements are proven equivalent.

