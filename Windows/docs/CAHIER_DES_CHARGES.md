# Cahier des charges — PC Monitor Pro

## But du produit

Fournir une application Windows de bureau claire et légère qui affiche en direct l'utilisation CPU, la mémoire physique, l'espace des volumes fixes et le débit réseau. L'interface doit rester compréhensible sans connaissance technique, fonctionner hors ligne et ne demander aucun droit administrateur pour le MVP.

## Public et systèmes ciblés

- Utilisateurs de PC Windows 10 et Windows 11.
- Minimum technique proposé : Windows 10 version 1809, build 17763. Le Windows App SDK reste rétrocompatible avec cette version, mais la prise en charge Microsoft dépend du cycle de vie de l'édition Windows ; les éditions Windows 10 Home/Pro sont hors support courant en 2026. Les appareils réellement annoncés devront être vérifiés avant distribution.
- Architecture de départ : x64. ARM64 sera ajouté après compilation et essais sur un appareil ARM64.
- Langue initiale : français ; textes préparés pour une localisation ultérieure.

## Périmètre MVP

| Domaine | Comportement MVP |
| --- | --- |
| CPU | Charge globale en pourcentage, calculée à partir des temps système Windows. |
| Mémoire | Mémoire physique utilisée et disponible, taille totale, barre de progression. |
| Disques | Volumes fixes prêts, espace utilisé/libre, capacité et activité globale de lecture/écriture si les compteurs Windows sont disponibles. |
| Réseau | Débits reçus et envoyés, total et par adaptateur actif. |
| Rafraîchissement | Gratuit : 1, 2, 5 ou 10 secondes. Pro : 15, 30 ou 60 secondes. Choix conservé localement ; pause/reprise et actualisation manuelle. |
| Historique | Gratuit : historique de session en mémoire sur 15 minutes. Pro : historique local persistant limité à 24 h, échantillonné au plus toutes les 15 secondes. Pas d'envoi des relevés ni de collecte quand l'application est fermée. |
| Interface | Tableau de bord, réglages simples, état de collecte lisible, unités adaptées, thèmes automatique/clair/sombre. |
| Historique des versions | Journal versionné conservé dans `CHANGELOG.md` comme documentation du projet; il n’est pas affiché dans l’application. |
| Installation | EXE autonome x64 gratuit pour les essais. Le MSIX x64 doit être reconstruit et revalidé pour chaque version envoyée au Store. |

## Modèle gratuit / Pro proposé

La version gratuite reste utilisable sans achat. Dans le package Microsoft Store, l’application utilise le module durable pour vérifier l’achat Premium. Les EXE de développement n’effectuent aucun achat; le build Premium de test simule la licence localement.

| Gratuit | Premium (PRO), achat unique dans le Microsoft Store |
| --- | --- |
| CPU, mémoire, disques et réseau en direct | Alertes personnalisées CPU, mémoire et espace disque |
| Historique de session en mémoire jusqu'à 15 min | Historique local persistant jusqu'à 24 h |
| Actualisation 1, 2, 5 ou 10 secondes | Actualisation étendue 15, 30 ou 60 secondes |
| Préférences locales et thèmes | Graphiques CPU, mémoire, réseau et activité disque sur 1 h, 6 h ou 24 h |
| — | Export CSV et rapports HTML imprimables |
| — | Charge GPU, mémoire vidéo, températures et ventilateurs si exposés par le matériel/pilote |

Les fonctions de base restent utiles sans paiement. Le système de licence est abstrait dans une bibliothèque réutilisable et l’adaptateur Microsoft Store est séparé de l’interface. La licence réelle doit être vérifiée avec un package Store et un droit Premium actif; l’EXE de test utilise uniquement une licence simulée.

L’offre Premium est un achat unique qui regroupe les fonctions marquées `(PRO)`. Les options Pro restent visibles dans la version gratuite; leur activation affiche le parcours Microsoft Store et ne lance pas la fonction sans licence valide.

## Fonctions hors périmètre actuel

- Surveillance détaillée par processus, contrôle à distance et collecte cloud.
- Intégration Home Assistant; le produit reste un utilitaire Windows local.
- La disponibilité GPU, mémoire vidéo, températures et ventilateurs varie selon le matériel, les pilotes et les droits fournis par Windows.

## Confidentialité et sécurité

- Les valeurs sont collectées localement et affichées dans le processus de l'application.
- Aucune télémétrie, publicité ni envoi de relevés. La vérification/transaction de licence utilise Microsoft Store dans la version empaquetée.
- Aucun contenu personnel n'est lu. Les libellés de volume sont affichés uniquement pour aider à distinguer les lecteurs.
- L'historique gratuit est borné en mémoire et effacé à la fermeture. L’historique Pro est enregistré localement, conservé 24 h et peut être supprimé depuis le profil Windows.

## Critères d'acceptation du MVP

1. L'application démarre sur Windows 10/11 compatible sans privilèges administrateur.
2. Les quatre cartes affichent une valeur ou un état « indisponible », jamais une valeur trompeuse quand une mesure échoue.
3. Le CPU est compris entre 0 et 100 % et se met à jour selon l'intervalle choisi; un utilisateur gratuit ne peut pas activer 15/30/60 secondes.
4. La mémoire et les volumes affichent valeurs et unités cohérentes ; les lecteurs non prêts sont ignorés sans faire échouer le tableau de bord.
5. Les débits réseau sont calculés entre deux relevés, ne sont pas négatifs et repartent à zéro après une remise à zéro des compteurs.
6. Pause, reprise, rafraîchissement manuel, navigation et persistance de l'intervalle fonctionnent.
7. L'historique gratuit reste en mémoire; l'historique Pro persistant est borné à 24 h et écrit uniquement dans le profil local.
8. Les fonctions Premium sont protégées par la licence dans l’application et le parcours gratuit ouvre l’offre du Microsoft Store.
9. Le rapport HTML est un fichier autonome imprimable; il est créé uniquement à la demande et à l’emplacement choisi.
10. L'EXE x64 démarre sur une machine propre avec les composants embarqués; le MSIX est reconstruit et validé sur une machine outillée avant chaque distribution Store.
