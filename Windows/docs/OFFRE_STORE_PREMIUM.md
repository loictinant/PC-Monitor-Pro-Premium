# Brouillon de l’offre Microsoft Store Premium

Ce document prépare les champs à compléter dans Partner Center. Il ne constitue pas une soumission ni une publication.

## Produit

- Application parente : PC Monitor Pro Premium
- ID Store de l’application : `9P34JVNCWKJJ`
- Nom proposé pour l’extension : **Déblocage Premium permanent**
- Type : **Durable**
- Durée : **Toutes les dates** (module sans expiration), enregistrée dans Partner Center le 2026-11-14
- ID produit : `PCMONITORPRO_PREMIUM_LIFETIME`
- ID Store de l’extension : `9MTVFWCM41XS`
- Prix unique : **1,99 €** (EUR; enregistré dans le brouillon Partner Center le 2026-11-14)
- Nom et description courte **Français (France)** : enregistrés dans le brouillon le 2026-11-14.
- Propriétés enregistrées le 2026-11-14 : téléchargement de logiciels électroniques; déclaration de confidentialité « Non »; URL de confidentialité et site de support laissés vides.
- Évaluation d’âge enregistrée : l’extension ne demande pas de restriction supérieure à celle de l’application parente; Partner Center déterminera la classification à partir du parent après sa publication.
- État : brouillon non soumis; aucune publication ni transaction

## Description courte proposée

Débloquez les fonctions marquées (PRO) de PC Monitor Pro Premium avec un achat unique : historique, graphiques, CSV, rapports, intervalles avancés et alertes. Les capteurs dépendent du matériel.

## Description détaillée proposée

L’achat unique PC Monitor Pro Premium donne accès aux fonctions marquées (PRO) : alertes personnalisées du processeur, de la mémoire et des disques; historique local jusqu’à 24 heures; graphiques CPU, mémoire, réseau et activité disque sur 1 h, 6 h ou 24 h; export CSV; rapports HTML imprimables; et actualisation toutes les 15, 30 ou 60 secondes. La charge GPU, la mémoire vidéo, les températures et les ventilateurs apparaissent lorsque le matériel et les pilotes les exposent. Les relevés restent sur l’appareil.

L’offre est achetée uniquement dans la version Microsoft Store. L’EXE autonome renvoie vers la fiche de l’application dans le Microsoft Store.

## Champs réellement disponibles dans Partner Center

La fiche de localisation de l’extension propose un nom de produit (100 caractères maximum), une description (200 caractères maximum) et une image PNG de 300 × 300 px. Elle ne contient pas de champ pour la description détaillée ci-dessus. Le nom « Déblocage Premium permanent » et la description courte proposée ont été enregistrés en français; la description longue reste une référence de rédaction et n’est pas dans cette fiche.

## Fonctions Premium prévues

- Alertes personnalisées CPU, mémoire et disques — implémentées; activation par licence Store dans le code.
- Historique persistant jusqu’à 24 h — implémenté; rétention locale et échantillonnage limité.
- Graphiques avancés sur 1 h, 6 h et 24 h — implémentés.
- Intervalle étendu 15/30/60 secondes — implémenté et protégé par la licence Pro.
- Export CSV et rapport HTML imprimable — implémentés.
- Surveillance GPU, mémoire vidéo, températures et ventilateurs — lecture best-effort; valeurs tributaires du matériel, des pilotes et des droits disponibles.

## Préparation de la déclaration de confidentialité

La revue du code actuel montre que les mesures CPU, mémoire, disques et débits réseau sont lues localement. Aucune télémétrie, bibliothèque d’analytics ou requête HTTP d’envoi n’a été trouvée. La vérification et l’achat de licence utilisent `StoreContext` dans l’application empaquetée. La déclaration « Non » a été enregistrée dans Partner Center; aucune URL de confidentialité ou de support n’a été renseignée. Un texte de politique basé sur le code actuel est préparé dans `docs/POLITIQUE_CONFIDENTIALITE.md`; il faut encore fournir un contact, le relire puis le publier sur une page publique avant d’enregistrer son URL.

## Étapes Partner Center restantes

- Les propriétés et l’évaluation d’âge sont terminées dans le brouillon. La classification d’âge de l’extension dépend de l’application parente.
- Le module ne peut pas être soumis tant que l’application parente n’est pas publiée.
- Les informations fiscales et de paiement du compte vendeur doivent être à jour avant tout versement.
- Ne pas soumettre ni publier avant validation du MSIX, du paquet et de l’ensemble de la fiche.

