# PC Monitor Pro Premium

Première application du catalogue Windows : un moniteur local, sans compte ni télémétrie distante, conçu pour Windows 10 et 11.
Sa fiche Microsoft Store est réservée sous le nom **PC Monitor Pro Premium** (ID `9P34JVNCWKJJ`).

## État du projet

- Version 0.2.2 : EXE de test x64 généré localement et configuré pour ouvrir la fiche Store ci-dessus. Le MSIX précédent conserve l’identité de développement `PCMonitorPro.Local` et n’est pas distribuable.
- Modèle choisi : achat unique dans le Microsoft Store. Le code lit l’entitlement d’un module Pro durable dans l’application packagée; l’EXE renvoie vers la fiche Store et reste gratuit.
- L’EXE ouvre la fiche Store. Le module Premium durable existe en brouillon (ID Store `9MTVFWCM41XS`); son prix de 1,99 €, sa fiche française et ses propriétés sont enregistrés. La soumission attend la publication de l’application parente et la mise à jour du profil fiscal/paiement du compte vendeur. Aucun achat ni aucune publication Store n’a été effectué.
- L’offre Premium à achat unique regroupe les fonctions marquées `(PRO)`. Les alertes sont les seules actuellement prêtes; l’historique persistant, les graphiques avancés, l’export CSV et les capteurs GPU/température restent à développer et valider.

## Ouvrir et essayer

Prérequis : Windows 10 build 17763 ou ultérieur, .NET 10 SDK, Visual Studio 2026 avec la charge de travail **Développement d'applications WinUI**, les outils Windows SDK 10.0.26100 et le mode développeur Windows activé. Le projet peut aussi se compiler via le SDK .NET en ligne de commande si les outils Windows App SDK/MSIX sont présents. Le minimum technique 17763 ne signifie pas que chaque édition de Windows 10 reste prise en charge par Microsoft ; la compatibilité sur les postes réellement visés doit être validée avant publication.

1. Ouvrir `PCMonitorPro.sln` dans Visual Studio.
2. Choisir `PCMonitor.App` et lancer en configuration Debug pour démarrer l’application.
3. Pour produire l’EXE autonome de test x64, exécuter `scripts/Build-Exe.ps1`. Le fichier sera créé dans `artifacts/PCMonitorPro-exe-x64-v0.2.2/PCMonitorPro.exe` et son lien Premium ouvrira la fiche Microsoft Store réservée.
4. `scripts/Build-Msix.ps1` génère un MSIX local non signé pour validation de structure. Il faut l’identité Partner Center et la signature adaptées avant toute installation de test certifiée ou distribution.

L'EXE autonome embarque .NET et Windows App SDK. Au premier démarrage, la publication mono-fichier extrait des composants dans le dossier temporaire Windows ; ce n'est pas un binaire sans dépendances ni extraction.

## Structure

```text
docs/                              Cahier des charges, architecture, validation
src/PCMonitor.Core/                Mesures et historique indépendant de l'interface
src/ProductLicensing.Abstractions/ Contrats de licence réutilisables entre produits
src/PCMonitor.App/                 Interface WinUI 3 et intégration Windows
scripts/                           Génération de l'EXE et du MSIX
```

La documentation de validation avant distribution est dans [docs/VALIDATION_AVANT_DISTRIBUTION.md](docs/VALIDATION_AVANT_DISTRIBUTION.md).
Le journal des versions est dans [CHANGELOG.md](CHANGELOG.md) et est également affiché dans l’application.
Le flux d’achat Microsoft Store et les identifiants nécessaires sont décrits dans [docs/LICENCE_MICROSOFT_STORE.md](docs/LICENCE_MICROSOFT_STORE.md).
