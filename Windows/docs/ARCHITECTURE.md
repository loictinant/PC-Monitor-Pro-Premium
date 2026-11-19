# Architecture technique — PC Monitor Pro Premium 0.3.0

## Plateforme et structure

- **C# / .NET 10 / WinUI 3 / Windows App SDK 2.6.5** : interface native Windows et packaging MSIX à partir d’un seul projet.
- **Cible** : Windows 10 build 17763 ou ultérieur et Windows 11; sorties actuelles x64. La compatibilité effective doit être validée sur les éditions Windows réellement ciblées.
- **Collecte/Core** : code sans dépendance WinUI pour faciliter la réutilisation dans d’autres produits.
- **Licence** : `ProductLicensing.Abstractions` reste indépendante du Store. Chaque application garde son propre adaptateur et sa liste de fonctions implémentées.
- **Données** : paramètres dans `%LOCALAPPDATA%\PCMonitorPro\settings.json`; historique Premium dans `%LOCALAPPDATA%\PCMonitorPro\history.json`, conservé 24 h avec un intervalle minimal de 15 s.

```text
PCMonitorPro.sln
├── src/PCMonitor.Core
│   ├── WindowsMetricsCollector       CPU, mémoire, volumes, réseau, activité disque
│   ├── PdhDiskActivityReader         Compteurs de lecture/écriture disque Windows
│   ├── LibreHardwareMetricsReader    Adaptateur facultatif de capteurs matériels
│   └── MetricsHistory                Échantillons et historique de session
├── src/ProductLicensing.Abstractions
│   ├── ILicenseProvider              Source de l’état de licence
│   ├── LicenseState                  Produit, niveau, provenance et échéance
│   └── IFeatureGate                  Décision d’accès par fonction
└── src/PCMonitor.App
    ├── MainWindow                    Navigation, tableaux de bord, graphiques et paramètres
    ├── PersistentMetricsHistory      Écriture/lecture locale avec rétention 24 h
    ├── AlertEvaluator                Délai, seuil, réarmement, répétition et heures calmes
    ├── MicrosoftStoreLicenseProvider Achat et vérification du module durable
    └── Package.appxmanifest          Identité Store et entrée MSIX
```

## Mesures et limites

- **CPU** : différence entre les compteurs `GetSystemTimes`; la première lecture sert à initialiser les compteurs.
- **Mémoire** : `GlobalMemoryStatusEx`, mémoire physique totale et disponible.
- **Volumes** : `DriveInfo`, volumes fixes prêts; capacité et espace disponible.
- **Réseau** : compteurs cumulatifs de chaque adaptateur actif puis débit par différence; le tableau de bord affiche les deux premiers et le total. Les interfaces loopback et tunnel sont exclues pour limiter les doublons de trafic virtuel.
- **Activité disque** : compteurs PDH anglais `PhysicalDisk(_Total)\Disk Read Bytes/sec` et `Disk Write Bytes/sec`; peut afficher « indisponible » si Windows n’expose pas ces compteurs.
- **Historique gratuit** : buffer mémoire borné; graphique CPU sur 15 minutes. Aucun relevé n’est écrit de façon persistante en mode gratuit.
- **Historique Premium** : échantillonnage local au plus toutes les 15 secondes, rétention 24 h, écriture du fichier environ chaque minute et à la fermeture. Les graphiques couvrent CPU, mémoire, réseau et activité disque sur 1 h, 6 h ou 24 h.
- **Export CSV** : fichier choisi explicitement par l’utilisateur; inclut horodatage, mesures disponibles et noms/débits des adaptateurs. Les données ne sont pas envoyées par l’application.

Les collectes sont locales. L’application n’exige pas d’élévation automatique et ne démarre pas de service. Les permissions Windows de l’utilisateur protègent les fichiers locaux; ils ne sont pas chiffrés par l’application.

## Capteurs GPU, températures et ventilateurs

`LibreHardwareMonitorLib` 0.9.6 est chargé comme source facultative et best-effort. Il expose uniquement les capteurs publiés par le matériel et le pilote. Les appareils sans capteur compatible affichent « non disponible sur ce PC ». Certains capteurs peuvent nécessiter des droits élevés, indisponibles depuis une application Store standard; l’application ne demande pas d’élévation. Les valeurs GPU/CPU/ventilateur ne sont donc pas garanties universellement. Les notices et la licence MPL-2.0 sont dans `src/PCMonitor.App/Assets/`.

## Licence et commerce

Le produit parent Microsoft Store est `9P34JVNCWKJJ`; l’extension Premium durable est `9MTVFWCM41XS` (`PCMONITORPRO_PREMIUM_LIFETIME`). Une licence valide active les fonctions marquées `(PRO)` via `ProductFeatureGate`; toute fonction livrée doit être ajoutée à sa liste d’implémentation et vérifiée indépendamment du rendu UI.

L’EXE autonome reste gratuit et ouvre la fiche Store pour l’achat. Le `.msixupload` est construit pour Partner Center et sera signé par le Store après certification. Aucun certificat privé n’est nécessaire dans le dépôt.

## Historique et versionnement

Le journal des versions reste dans `CHANGELOG.md` et n’est pas embarqué comme page de l’application. Les versions Windows et Store sont incrémentées dans `PCMonitor.App.csproj` et `Package.appxmanifest`.
