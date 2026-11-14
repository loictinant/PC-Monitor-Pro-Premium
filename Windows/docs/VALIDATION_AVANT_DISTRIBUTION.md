# Validation avant toute distribution

Rien dans cette liste ne signifie que le produit est publié. Elle doit être exécutée avant un envoi Store ou une distribution directe.

## Poste de développement requis

- Windows 10/11 x64, .NET 10 SDK, Visual Studio avec la charge WinUI, Windows SDK 10.0.26100 et outils de packaging MSIX.
- Mode développeur activé pour l'essai local d'applications WinUI/MSIX.
- Connexion réseau uniquement pour restaurer les packages NuGet pendant le build ; l'application elle-même ne requiert pas de réseau.
- `dotnet`, MSBuild, SDK Windows, Windows App SDK et certificat de développement de confiance à vérifier avant de lancer les scripts.

## Validation fonctionnelle

- Faire des relevés CPU pendant repos, charge soutenue, veille/reprise, changement de plan d'alimentation et systèmes multi-cœurs.
- Comparer mémoire et volumes avec Gestionnaire des tâches et Explorateur ; inclure plusieurs volumes, volume presque plein, lecteur absent ou non prêt, et chemins avec caractères accentués.
- Comparer le débit réseau avec une source Windows de référence ; tester Wi-Fi, Ethernet, VPN, changement d'interface et remise à zéro des compteurs.
- Tester chaque intervalle, pause/reprise, actualisation manuelle, fermeture/réouverture et changement de thème/densité.
- Vérifier que les alertes et les autres commandes marquées `(PRO)` sont visibles mais restent inactives dans l’édition gratuite; leur activation doit afficher l’offre Pro et ne doit pas envoyer de notification.
- Vérifier dans la page Historique que chaque entrée correspond au fichier `CHANGELOG.md` livré avec la version.
- Vérifier la zone de notification, le double-clic pour rouvrir la fenêtre et le démarrage avec Windows dans l’EXE autonome.
- Vérifier qu’un clic Pro depuis l’EXE ouvre la fiche Microsoft Store PC Monitor Pro Premium (`9P34JVNCWKJJ`) sur Windows 10 et 11.
- Dans le paquet Store associé, tester l’achat unique de l’extension durable, la restauration de licence et la persistance de l’entitlement hors ligne.
- Laisser le tableau de bord ouvert au moins plusieurs heures pour vérifier le buffer, la consommation mémoire/CPU de l'application et l'absence de chevauchement des collectes.
- Contrôler les scénarios où chaque source système échoue : afficher « indisponible » et maintenir le reste de l'application utilisable.

## Validation MSIX avant publication

- Remplacer nom, éditeur, identité, version, logos, architectures et déclarations du manifeste par les valeurs du produit final.
- Générer le paquet pour chaque architecture voulue, contrôler les dépendances Windows App SDK, installer, lancer, mettre à jour, réparer et désinstaller sur des machines propres.
- Vérifier la signature locale pour les essais. Pour le Store, suivre le flux de signature et de soumission Microsoft correspondant au compte éditeur.
- Exécuter Windows App Certification Kit et vérifier les avertissements/erreurs, le lancement, le cycle de vie et la désinstallation.
- Tester le package sur les versions minimales Windows réellement annoncées et sur la version courante.

## Validation EXE

- Tester la sortie sur une machine Windows propre sans SDK Visual Studio : lancement initial, extraction temporaire, droits standard, antivirus/SmartScreen, lancement depuis un chemin contenant des espaces, mise à jour et suppression.
- Décider si la livraison directe sera un EXE autonome ou un installateur signé. Le binaire produit par ce dépôt est un EXE autonome de test, pas un assistant d'installation complet.
- Signer tout binaire proposé en téléchargement direct et documenter le canal de mise à jour, les versions supportées et la désinstallation.

## Vérifications produit et commerciales

- Avant soumission, définir l’éditeur et la marque, vérifier les URL de confidentialité et de support, et contrôler les textes et paramètres de l’offre Premium.
- La fiche et l’extension durable PC Monitor Pro Premium existent en brouillon dans Partner Center; le prix unique de 1,99 €, la fiche française, les propriétés et l’évaluation d’âge sont enregistrés. Vérifier les IDs configurés par défaut dans `scripts/Build-Msix.ps1`.
- La soumission de l’extension attend la publication de l’application parente et la mise à jour des informations fiscales et de paiement du vendeur.
- L’achat et la licence utilisent `Windows.Services.Store`; le test réel dépend de l’association et de l’installation de la version Store. Voir `docs/LICENCE_MICROSOFT_STORE.md`.
- Vérifier qu'aucun bouton de paiement ne suggère qu'une vente existe tant que le système n'est pas configuré.
- Refaire une revue des licences NuGet et composants tiers avant d'ajouter graphiques, SQLite ou télémétrie GPU/température.
