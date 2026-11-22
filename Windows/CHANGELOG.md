# Historique des versions — PC Monitor Pro Premium

Ce fichier conserve le journal de développement du projet. Il n’est plus embarqué ni affiché dans l’application.

## 0.4.0 — 2026-11-22 — Rapports et intervalles Premium

- Premium : choix d’un rafraîchissement plus espacé, toutes les 15, 30 ou 60 secondes. Ces options restent verrouillées sans licence Pro; l’édition gratuite conserve ses choix de 1 à 10 secondes.
- Premium : création d’un rapport HTML autonome à partir de la période sélectionnée (1 h, 6 h ou 24 h), avec statistiques min./moy./max., courbe CPU/mémoire, capteurs disponibles et impression en PDF depuis un navigateur. Le CSV inclut maintenant la vitesse du ventilateur lorsqu’elle est exposée.
- Le rapport est généré localement, enregistré à l’emplacement choisi et indique clairement les capteurs non disponibles.
- Version de l’application incrémentée à 0.4.0. Le `.msixupload` x64 a été généré localement; il n’a pas encore été envoyé à Partner Center.

## 0.3.0 — 2026-11-19 — Nouvelles fonctions gratuites et Premium

- Gratuit : affichage des débits réseau par adaptateur, activité disque en lecture/écriture, historique CPU de session sur 15 minutes et infobulle de zone de notification avec CPU, mémoire et réseau.
- Premium : historique local jusqu’à 24 heures, avec un échantillon toutes les 15 secondes maximum et conservation dans le profil Windows.
- Premium : graphiques CPU, mémoire, réseau et disque réglables sur 1 h, 6 h ou 24 h, et export CSV de la période sélectionnée.
- Premium : alertes CPU, mémoire et espace disque avec délai de déclenchement configurable, temporisation entre alertes et plage calme facultative de 22 h à 7 h.
- Premium : lecture facultative de la charge GPU, de la mémoire vidéo, des températures CPU/GPU et des ventilateurs via LibreHardwareMonitor. Les informations non exposées par le matériel/pilote restent indiquées indisponibles; aucune élévation de privilèges n’est demandée.
- Licence et notices MPL-2.0 de la bibliothèque capteurs ajoutées au paquet.
- Mise à jour de la documentation de confidentialité, de l’architecture et des informations de distribution.
- Statut de publication : candidat local; génération EXE/MSIX et mise à jour des canaux de distribution à consigner après validation de leur résultat.

## 0.2.5 — 2026-11-18 — Offre Premium visible

- Ajout d’une invitation d’achat Premium sur le tableau de bord gratuit; elle disparaît après activation de la licence.
- Le bouton ouvre le parcours d’achat Microsoft Store dans l’application empaquetée; l’EXE propose d’ouvrir l’application du Store.
- Précision des textes : achat unique, alertes CPU/mémoire/disques disponibles, fonctions « à venir » non incluses et prix confirmé par Microsoft Store.
- Compilation réussie de l’EXE gratuit x64 et du `.msixupload` x64 de mise à jour. Aucun paquet n’a été envoyé ni publié; le candidat doit encore être vérifié avant soumission.
- L’archive de symboles `.appxsym` manque, car `mspdbcmf.exe` n’est pas installé avec Visual Studio.

## 0.2.4 — 2026-11-16 — Autorisation des notifications

- Ajout du parcours « Autoriser et tester » pour les alertes Premium : l’application explique l’état des notifications, propose d’ouvrir les paramètres Windows et envoie une notification de test lorsque Windows les autorise.
- L’activation des alertes demande désormais cette vérification immédiatement.
- Compilation de l’EXE gratuit x64, de l’EXE de test Premium (licence simulée, non distribuable) et du paquet `.msixupload` x64.
- Le paquet Store est un candidat de mise à jour de l’application principale; il n’a pas encore été testé avec WACK ni soumis dans Partner Center. Les symboles `.appxsym` n’ont pas été générés, car `mspdbcmf.exe` est absent de l’installation Visual Studio.

## 0.2.3 — 2026-11-14 — Historique intégré retiré

- Retrait de la page **Historique** des versions et de son entrée de navigation dans l’application.
- Conservation de `CHANGELOG.md` comme historique du projet, sans l’ajouter au binaire.
- Version applicative incrémentée à 0.2.3 pour les nouvelles compilations EXE et MSIX.
- Compilation réussie de l’EXE x64, du MSIX de test auto-signé et du `.msixupload` x64 destiné à Partner Center. Aucun paquet n’a été soumis ni publié.
- Windows App Certification Kit exécuté sur le paquet de test : résultat global `PASS`, 24 tests complets et un seul échec facultatif informatif (« Fichiers exécutables bloqués »). Le rapport est conservé dans `artifacts/PCMonitorPro-WACK-v0.2.3.xml`.
- `scripts/Build-Msix.ps1` utilise le mode StoreUpload; le fichier généré est sous `artifacts/PCMonitorPro-msix-upload-v0.2.3/`. La sortie ne contient pas de symboles `.appxsym`, car `mspdbcmf.exe` manque dans l’installation Visual Studio actuelle.
- Création d’un brouillon local de politique de confidentialité; l’URL publique obligatoire reste à ajouter à la fiche Store.

## 0.2.2 — 2026-11-14 — Offre Premium clarifiée

- Configuration de l’identité MSIX avec les valeurs Partner Center : `TINANTLOIC.PCMonitorProPremium`, Publisher `CN=D67E21A8-DC00-45EF-86D1-375158FB659F`, éditeur affiché `TINANT LOIC`.
- Génération d’un candidat MSIX x64 0.2.2 non signé pour le flux Store et d’un paquet auto-signé séparé réservé aux essais locaux. Aucun paquet n’a été soumis; le test WACK reste à exécuter.
- L’achat unique Premium est prévu pour regrouper toutes les fonctions marquées `(PRO)` dans un seul module durable.
- Les alertes sont les seules fonctions Premium prêtes dans cette version; historique persistant, graphiques avancés, export CSV et télémétrie GPU/température restent à venir.
- Le nom affiché dans l’application suit maintenant la fiche réservée PC Monitor Pro Premium.
- Le module Premium durable a été créé dans Partner Center en brouillon (ID produit `PCMONITORPRO_PREMIUM_LIFETIME`, ID Store `9MTVFWCM41XS`); le prix unique de **1,99 €** et la fiche française (nom « Déblocage Premium permanent » et description courte) sont enregistrés.
- Les propriétés sont enregistrées : durée « Toutes les dates », type « Téléchargement de logiciels électroniques » et déclaration de confidentialité « Non »; URL de confidentialité et site de support laissés vides.
- L’évaluation d’âge est enregistrée sans restriction supérieure à celle de l’application parente; Partner Center déterminera la classification après publication du parent.
- La fiche française est complète. La description longue n’a pas de champ sur la fiche de l’extension.
- Le module reste non soumis : Partner Center exige d’abord la publication de l’application parente. Les informations de revenus et fiscales du compte vendeur doivent aussi être à jour pour les paiements. Aucune publication ni transaction n’a été effectuée.

## 0.2.1 — 2026-11-14 — EXE lié à la fiche Store

- L’EXE de test ouvre maintenant la fiche Microsoft Store réservée « PC Monitor Pro Premium » (ID `9P34JVNCWKJJ`) lorsque l’utilisateur demande Pro.
- Le module Pro durable n’est pas encore créé; l’achat intégré et le MSIX de publication restent à configurer et valider.

## 0.2.0 — 2026-11-14 — Candidat de test


- Les alertes CPU, mémoire et disque sont réservées à la version payante Pro. La version gratuite ne peut plus les activer ni envoyer de notifications, y compris si d’anciens réglages d’alerte existent sur le PC.
- Les fonctions payantes sont affichées avec la mention **(PRO)**. Le premier achat Pro est une extension Microsoft Store durable à achat unique; seules les alertes sont implémentées dans cette version.
- Ajout de l’adaptateur de licence Microsoft Store : lecture de l’entitlement Pro, achat/restauration du module durable dans le paquet Store, verrouillage des alertes par licence, et renvoi de l’EXE vers la fiche Store.
- Les IDs Store de l’application et du module Pro restent à configurer dans Partner Center. Aucun achat ni aucune publication n’a été effectué; l’EXE reste en édition gratuite tant que la fiche Store n’existe pas.
- Ajout du thème **Automatique (Windows)**, **Clair** ou **Sombre**, avec mémorisation du choix.
- Ajout de la page Historique dans l’application; elle lit les notes de version de ce fichier.
- Ajout des informations développeur : **LOIC TINANT**.
- EXE autonome x64 généré dans un dossier versionné; un MSIX x64 non signé a aussi été généré et sa structure vérifiée. Son identité éditeur reste provisoire; il n’a pas été installé ni soumis au Windows App Certification Kit.

## 0.1.0 — Prototype initial

- Tableau de bord local CPU, mémoire, volumes fixes et débits réseau.
- Graphique CPU et historique de session borné, effacé à la fermeture.
- Intervalle de rafraîchissement réglable, pause et actualisation manuelle.
- Zone de notification, option de démarrage avec Windows et première page de paramètres.
- Première version des préférences d’alertes, alors exposées dans le prototype gratuit; elles sont désormais verrouillées pour être réservées à Pro.
