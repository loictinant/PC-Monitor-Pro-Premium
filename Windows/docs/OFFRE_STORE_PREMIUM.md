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

Débloquez les fonctions marquées (PRO) de PC Monitor Pro Premium avec un achat unique. Les fonctions indiquées « à venir » ne sont pas encore disponibles.

## Description détaillée proposée

L’achat unique PC Monitor Pro Premium donne accès aux fonctions marquées (PRO) dès qu’elles sont disponibles dans l’application. La version actuelle comprend les alertes personnalisées du processeur, de la mémoire et des disques. L’historique persistant, les graphiques avancés, l’export CSV et les données GPU/température sont prévus pour des versions ultérieures et ne sont pas encore disponibles. Les capteurs dépendront du matériel et des pilotes pris en charge.

L’offre est achetée uniquement dans la version Microsoft Store. L’EXE autonome renvoie vers la fiche de l’application dans le Microsoft Store.

## Champs réellement disponibles dans Partner Center

La fiche de localisation de l’extension propose un nom de produit (100 caractères maximum), une description (200 caractères maximum) et une image PNG de 300 × 300 px. Elle ne contient pas de champ pour la description détaillée ci-dessus. Le nom « Déblocage Premium permanent » et la description courte proposée ont été enregistrés en français; la description longue reste une référence de rédaction et n’est pas dans cette fiche.

## Fonctions Premium prévues

- Alertes personnalisées CPU, mémoire et disques — implémentées, activation via licence Store à valider.
- Historique persistant jusqu’à 24 h — à développer.
- Graphiques avancés et intervalle étendu — à développer.
- Export CSV et rapports — à développer.
- Surveillance GPU, mémoire vidéo, températures et ventilateurs — à étudier selon les API, matériels et pilotes disponibles.

## Préparation de la déclaration de confidentialité

La revue du code actuel montre que les mesures CPU, mémoire, disques et débits réseau sont lues localement. Aucune télémétrie, bibliothèque d’analytics ou requête HTTP d’envoi n’a été trouvée. La vérification et l’achat de licence utilisent `StoreContext` dans l’application empaquetée. La déclaration « Non » a été enregistrée dans Partner Center; aucune URL de confidentialité ou de support n’a été renseignée. Un texte de politique basé sur le code actuel est préparé dans `docs/POLITIQUE_CONFIDENTIALITE.md`; il faut encore fournir un contact, le relire puis le publier sur une page publique avant d’enregistrer son URL.

## Étapes Partner Center restantes

- Les propriétés et l’évaluation d’âge sont terminées dans le brouillon. La classification d’âge de l’extension dépend de l’application parente.
- Le module ne peut pas être soumis tant que l’application parente n’est pas publiée.
- Les informations fiscales et de paiement du compte vendeur doivent être à jour avant tout versement.
- Ne pas soumettre ni publier avant validation du MSIX, du paquet et de l’ensemble de la fiche.

