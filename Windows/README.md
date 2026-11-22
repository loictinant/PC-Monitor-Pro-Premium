# PC Monitor Pro Premium

Moniteur système local pour Windows 10/11, construit en C# avec WinUI 3. La version gratuite suit CPU, mémoire, volumes, activité disque et réseau. L’achat unique Microsoft Store débloque l’historique local 24 h, les graphiques avancés, l’export CSV, les rapports HTML imprimables, les intervalles prolongés, les alertes configurables et les capteurs matériels pris en charge.

## Version 0.4.0

### Gratuit

- Tableau de bord CPU, mémoire vive, volumes fixes et réseau.
- Débits réseau totaux et par adaptateur actif.
- Activité disque globale en lecture et écriture.
- Graphique de session CPU sur les 15 dernières minutes, intervalle de 1, 2, 5 ou 10 secondes.
- Thème automatique/clair/sombre, démarrage avec Windows et réduction dans la zone de notification.

### Premium (PRO)

- Alertes personnalisées CPU, mémoire et espace disque, avec délai de déclenchement, délai entre répétitions et heures calmes facultatives (22 h–7 h).
- Historique local jusqu’à 24 heures et graphiques CPU, mémoire, réseau et disque sur 1 h, 6 h ou 24 h.
- Export CSV de la période sélectionnée.
- Rapports HTML locaux avec statistiques min./moy./max. et courbe CPU/mémoire; impression en PDF depuis un navigateur.
- Rafraîchissement étendu à 15, 30 ou 60 secondes.
- Charge GPU, mémoire vidéo, températures CPU/GPU et ventilateurs lorsque le matériel, les pilotes et les permissions les exposent.

L’EXE autonome est gratuit et dirige vers la fiche Store pour l’offre Premium. Les fonctions GPU et température ne sont pas garanties sur tous les PC; l’application n’élève pas ses droits automatiquement. Les relevés et l’historique restent sur l’appareil.

## Compiler et essayer

Prérequis : Windows 10 build 17763 ou ultérieur, .NET 10 SDK et Visual Studio avec la charge de travail **Développement d’applications WinUI** et les outils Windows SDK/MSIX.

```powershell
dotnet build PCMonitorPro.sln --configuration Release
.\scripts\Build-Exe.ps1
.\scripts\Build-Exe.ps1 -PremiumTest
.\scripts\Build-Msix.ps1
```

Les sorties sont créées dans `artifacts/`. L’EXE Premium de test simule une licence locale; il ne réalise pas d’achat et ne doit pas être distribué. Le `.msixupload` est la sortie destinée à Partner Center; un paquet de test local signé séparément peut être produit avec les scripts `Sign-Test-Msix.ps1` et `Install-Test-Msix.ps1`.

## Structure

```text
src/PCMonitor.Core/                Collecte Windows et historique typé
src/ProductLicensing.Abstractions/ Contrats de licence réutilisables
src/PCMonitor.App/                 Interface WinUI 3, paramètres et intégrations Windows
scripts/                           Compilation EXE/MSIX et installation d’essai
docs/                              Architecture, confidentialité et distribution
CHANGELOG.md                       Historique du projet, non affiché dans l’application
```

La bibliothèque LibreHardwareMonitor est utilisée pour les capteurs facultatifs. Sa licence MPL-2.0 et les avis tiers sont fournis dans `src/PCMonitor.App/Assets/`.

## Distribution

Le produit Microsoft Store est identifié par `9P34JVNCWKJJ`; l’extension Premium durable par `9MTVFWCM41XS`. Le Store gère le paiement et la licence de l’extension. Aucune clé de signature privée n’est stockée dans le dépôt. La validation avant distribution figure dans [docs/VALIDATION_AVANT_DISTRIBUTION.md](docs/VALIDATION_AVANT_DISTRIBUTION.md), et la conception dans [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
