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
| Disques | Volumes fixes prêts, espace utilisé/libre, capacité. Pas de mesure d'IO disque au MVP. |
| Réseau | Débits agrégés reçus et envoyés, calculés à partir des compteurs des interfaces actives. |
| Rafraîchissement | 1, 2, 5 ou 10 secondes, choix conservé localement ; pause/reprise et actualisation manuelle. |
| Historique | Historique de session borné à 15 minutes, affichage des 60 derniers points. Pas d'envoi réseau ni de collecte en arrière-plan lorsque l'application est fermée. |
| Interface | Tableau de bord, réglages simples, état de collecte lisible, unités adaptées, thèmes automatique/clair/sombre. |
| Historique des versions | Journal versionné conservé dans `CHANGELOG.md` comme documentation du projet; il n’est pas affiché dans l’application. |
| Installation | EXE autonome x64 pour les essais et paquet MSIX x64 préparé pour validation Store. |

## Modèle gratuit / Pro proposé

Le MVP livré dans ce dépôt reste gratuit et n'effectue aucun achat.

| Gratuit | Pro, phase ultérieure |
| --- | --- |
| CPU, mémoire, disques et réseau en direct | Historique persistant jusqu'à 24 h, après validation de l'impact disque |
| Historique de session jusqu'à 15 min | Historique persistant jusqu'à 24 h (PRO), après validation de l'impact disque |
| Rafraîchissement configurable | Alertes CPU, mémoire et disque (PRO), verrouillées dans l’édition gratuite |
| Préférences locales et thèmes | Graphiques avancés, intervalle étendu et export CSV (PRO) |
| — | GPU, mémoire vidéo, températures et ventilateurs (PRO), uniquement si une source fiable existe pour le matériel |

Les fonctions de base restent utiles sans paiement. Le système de licence est abstrait dans une bibliothèque réutilisable et un adaptateur Microsoft Store est présent. L’extension durable est créée en brouillon; l’activation payante réelle ne peut pas être validée avant publication de l’application parente et installation d’une version de test via le Store.

L’offre Premium est un achat unique qui regroupe les fonctions marquées `(PRO)`. Les options Pro sont visibles afin d’expliquer l’offre. Les fonctions encore à venir restent identifiées comme telles; leur activation affiche un message d’information au lieu de prétendre les débloquer.

## Hors MVP

- La collecte réelle des mesures GPU/température, l’historique persistant et l’export CSV ne sont pas implémentés.
- Les alertes ont une interface et un service local, mais restent verrouillées dans l’édition gratuite. Leur déverrouillage réel dépend de l’extension durable Microsoft Store, créée en brouillon mais pas encore soumise.
- Surveillance par processus, services Windows, contrôle à distance, collecte cloud.
- Connexion ou intégration Home Assistant ; ce produit reste un utilitaire Windows local.

## Confidentialité et sécurité

- Les valeurs sont collectées localement et affichées dans le processus de l'application.
- Aucune permission réseau, élévation, compte utilisateur, télémétrie ou publicité au MVP.
- Aucun contenu personnel n'est lu. Les libellés de volume sont affichés uniquement pour aider à distinguer les lecteurs.
- L'historique est borné en mémoire et est effacé à la fermeture.

## Critères d'acceptation du MVP

1. L'application démarre sur Windows 10/11 compatible sans privilèges administrateur.
2. Les quatre cartes affichent une valeur ou un état « indisponible », jamais une valeur trompeuse quand une mesure échoue.
3. Le CPU est compris entre 0 et 100 % et se met à jour selon l'intervalle choisi.
4. La mémoire et les volumes affichent valeurs et unités cohérentes ; les lecteurs non prêts sont ignorés sans faire échouer le tableau de bord.
5. Les débits réseau sont calculés entre deux relevés, ne sont pas négatifs et repartent à zéro après une remise à zéro des compteurs.
6. Pause, reprise, rafraîchissement manuel, navigation et persistance de l'intervalle fonctionnent.
7. L'historique reste borné, s'efface à la fermeture et ne déclenche aucune écriture disque.
8. L'EXE x64 démarre sur une machine propre avec les composants embarqués, et le MSIX est généré et validé sur une machine outillée.
