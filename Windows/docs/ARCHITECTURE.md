# Architecture technique

## Choix

- **C# / .NET 10 / WinUI 3 / Windows App SDK 2.6.5** : version stable actuelle au 14 novembre 2026, interface XAML, adaptée au catalogue Store et au packaging MSIX.
- **Windows SDK Build Tools 10.0.26100.9457** : outils de compilation alignés sur le SDK Windows 26100 ciblé. Les versions du SDK Windows et du Windows App SDK sont indépendantes.
- **MSIX single-project** : le projet WinUI porte son manifeste et produit le paquet sans projet de packaging séparé.
- **EXE autonome x64** : même projet compilé en mode unpackaged, avec runtime .NET et Windows App SDK auto-contenus. Il sert à tester et pourra servir à une distribution directe après décision produit et vérifications de sécurité.
- **Pas de dépendance de graphiques tierce au MVP** : un tracé WinUI léger évite d'ajouter une dépendance et permet de garder le paquet compact.
- **Pas de SQLite au MVP** : les 15 minutes d'historique restent en mémoire. SQLite ne sera ajouté que si l'historique Pro persistant est confirmé.

## Projets et responsabilités

```text
PCMonitorPro.sln
├── src/PCMonitor.Core
│   ├── IMetricsCollector             Contrat des mesures
│   ├── WindowsMetricsCollector       CPU, mémoire, volumes, réseau
│   └── MetricsHistory                Historique borné de la session
├── src/ProductLicensing.Abstractions
│   ├── ILicenseProvider              Source de l'état de licence
│   ├── LicenseState                  Produit, niveau, provenance et échéance
│   └── IFeatureGate                  Décision d'accès aux fonctions
└── src/PCMonitor.App
    ├── MainWindow                    Navigation, tableau de bord et réglages
    ├── AppSettings                   Intervalle, zone de notification, démarrage, thème
    ├── ProductFeatureGate            Verrouillage des droits Pro
    ├── AlertEvaluator                Évaluation locale des seuils d'alerte
    ├── Package.appxmanifest          Identité provisoire et entrée MSIX
    └── Build modes                   MSIX par défaut ou EXE via propriétés MSBuild
CHANGELOG.md                          Journal affiché dans la page Historique
```

La couche Core ne connaît ni WinUI, ni le Microsoft Store, ni le futur service de licence. Les autres produits du catalogue pourront référencer `ProductLicensing.Abstractions` et fournir leur propre adaptateur. Une licence doit être interprétée par produit (identifiant de produit et fonctions) ; il ne faut pas faire confiance à un booléen caché uniquement dans l'interface. Dans le candidat 0.2.0, la licence de développement est toujours gratuite; les alertes sont vérifiées par le FeatureGate avant leur évaluation et sont donc inactives dans le gratuit.

## Collecte des mesures

- **CPU** : P/Invoke `GetSystemTimes`, comparaison de deux relevés. La première mesure sert à initialiser les compteurs et peut afficher 0 %.
- **Mémoire** : P/Invoke `GlobalMemoryStatusEx`, mémoire physique totale et disponible.
- **Disques** : `DriveInfo`, volumes fixes prêts ; mesures de capacité seulement.
- **Réseau** : `NetworkInterface` et compteurs cumulatifs des interfaces actives. Le débit est la différence en octets sur le temps écoulé. Les interfaces loopback et tunnel sont ignorées.
- **Rafraîchissement** : un seul timer UI ; une collecte en cours est empêchée de se chevaucher avec la suivante.
- **Historique** : buffer borné de 900 points ; le graphique présente les 60 points les plus récents. La valeur maximale correspond à 15 minutes avec un rafraîchissement à une seconde.

## Sorties de build prévues

### MSIX

Le projet est configuré en mode packagé par défaut, avec un manifeste et une identité provisoire `PCMonitorPro.Local`. Avant une soumission Store, l'identité, l'éditeur, les logos, les versions, les architectures et la configuration Store devront être remplacés par les données réelles du compte éditeur. La création d'un paquet de test et son installation locale nécessitent un certificat de développement approuvé ; ne pas distribuer cette identité provisoire.

### EXE

`scripts/Build-Exe.ps1` force `WindowsPackageType=None` et publie une application autonome x64 en mono-fichier dans un dossier versionné. Windows App SDK impose l'extraction de composants au premier lancement pour la redirection des DLL; vérifier le comportement sur un PC propre avant de présenter cette sortie comme un installateur.

## Licence et commerce

`ProductLicensing.Abstractions` définit un contrat stable et sans dépendance à Microsoft Store. PC Monitor Pro Premium utilise un adaptateur `Windows.Services.Store`: le paquet Store lit l'entitlement d'une extension durable et demande l'achat unique via `StoreContext`. L'EXE autonome ne tente pas d'achat et ouvre la fiche Store (ID `9P34JVNCWKJJ`). L'extension Premium durable est créée en brouillon (Store ID `9MTVFWCM41XS`, Product ID `PCMONITORPRO_PREMIUM_LIFETIME`); le script MSIX ajoute ces IDs aux métadonnées d'assembly. Voir `docs/LICENCE_MICROSOFT_STORE.md`.

L’offre Premium unique est prévue pour regrouper les fonctions marquées `(PRO)`. Dans la version 0.2.2, seules les alertes sont implémentées et protégées au point d’évaluation et dans l’interface. Les autres fonctions restent affichées comme étant à venir jusqu’à leur implémentation. Ne pas mettre de secret ou de prix en dur côté client.

## GPU et températures : faisabilité séparée

### Utilisation et mémoire GPU

Faisable comme module facultatif, mais pas assez uniforme pour promettre une mesure à tous les utilisateurs. Les compteurs GPU Windows peuvent exposer l'activité par moteur et la mémoire, selon la version de Windows, le pilote WDDM, le matériel et la présence du compteur. Une future implémentation devra énumérer les compteurs réellement disponibles et afficher « indisponible » quand l'information manque. Les métriques d'un GPU intégré, d'un GPU dédié et de systèmes à plusieurs GPU ne se résument pas toujours à une valeur unique.

### Température CPU/GPU et ventilateurs

Non retenu pour le MVP. Windows ne fournit pas une API desktop universelle garantissant ces températures sur toutes les cartes et tous les pilotes. L'ancienne Sensor API de Windows est dépréciée ; le chemin réaliste est un adaptateur optionnel par fournisseur ou une bibliothèque tierce dont la licence, la compatibilité MSIX et la politique de mise à jour auront été examinées. Les capteurs ACPI de carte mère ne garantissent pas la température du CPU ou du GPU. La fonction ne devra apparaître que lorsqu'une source fiable fournit la donnée et l'unité.

## Limites connues

- Les compteurs réseau sont agrégés et peuvent inclure trafic local, VPN ou Wi-Fi selon la classification de l'interface.
- Les volumes réseau et lecteurs amovibles sont exclus du panneau disque initial.
- Les valeurs système reflètent des compteurs Windows ; elles peuvent différer légèrement d'autres outils selon le lissage et l'intervalle.
- L'identité MSIX, l'éditeur, les visuels Store, la signature, les SKU de licence et le support ARM64 ne sont pas prêts pour distribution.
