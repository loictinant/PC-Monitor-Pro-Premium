# Historique des versions — PC Monitor Pro Premium

Ce fichier est la source du journal affiché dans la page **Historique** de l’application.

## 0.2.2 — 2026-11-14 — Offre Premium clarifiée

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
