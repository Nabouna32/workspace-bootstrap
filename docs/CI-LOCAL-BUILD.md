# CI publique et builds locaux

## Décision

Le poste personnel n'est pas un serveur CI.

Les dépôts publics utilisent les runners standard GitHub-hosted pour la validation automatique. GitHub documente actuellement ces runners comme gratuits et illimités pour les dépôts publics. La CI peut donc exécuter les tests complets utiles sans consommer le quota mensuel de minutes des dépôts privés.

Le PC conserve une toolchain locale parce que le coût pertinent n'est pas seulement le temps CI : télécharger à répétition un APK, AAB ou EXE de grande taille depuis GitHub consomme la bande passante disponible à domicile. Un build local permet donc de compiler et tester rapidement sans récupérer l'artefact distant.

## Répartition des responsabilités

### GitHub Actions

La CI publique doit couvrir, selon le projet :

- analyse statique ;
- tests unitaires ;
- tests d'intégration ;
- tests E2E ;
- Playwright ;
- builds Android ;
- tests Android ;
- builds Windows ;
- tests Windows ;
- validations de packaging et contrats.

La CI est la source de vérité pour la validation reproductible.

### PC local

Le PC doit pouvoir réaliser les opérations coûteuses en téléchargement lorsque l'utilisateur veut tester immédiatement :

- build APK/AAB local ;
- build Windows ;
- tests Flutter locaux ;
- tests web locaux ;
- lancement d'un émulateur Android ;
- lancement d'un serveur de développement ;
- diagnostic et reproduction d'un échec CI.

Le résultat local n'a pas besoin d'être publié sur GitHub.

## Artefacts

Un workflow de validation ne doit pas publier automatiquement un gros artefact simplement parce qu'il a compilé un binaire.

Politique cible :

- **PR / validation** : compiler et tester ; ne publier un artefact que si son téléchargement est réellement utile ;
- **build manuel** : publier un artefact lorsque l'utilisateur veut récupérer le binaire ;
- **release** : publier les binaires nécessaires à la release ;
- **debug CI** : conserver uniquement les fichiers nécessaires au diagnostic.

Les artefacts et les logs ont une durée de rétention configurable. Les artefacts inutiles doivent donc être évités plutôt que téléchargés puis supprimés localement.

## Cache

Le cache GitHub sert aux dépendances réutilisées entre exécutions : Gradle, npm/pnpm/Yarn, Pub, etc. Il ne remplace pas un build local et ne doit pas être confondu avec un artefact.

Un cache peut réduire les téléchargements effectués par les runners GitHub, mais il ne rend pas nécessaire le téléchargement du résultat de chaque build vers le PC.

## Règle pratique

Si la question est :

> « Est-ce que le code compile et les tests passent ? »

→ GitHub Actions.

Si la question est :

> « Je veux installer/tester cette version maintenant sur mon PC ou mon téléphone sans télécharger 100+ Mo depuis GitHub. »

→ build local.

Si la question est :

> « Je veux récupérer un binaire pour une release ou un test sur une autre machine. »

→ artefact GitHub / release.

## Exécution CI

La validation du dépôt repose sur les runners GitHub-hosted standards. Le PC reste réservé au développement et aux builds locaux rapides lorsque ceux-ci évitent des téléchargements importants.
