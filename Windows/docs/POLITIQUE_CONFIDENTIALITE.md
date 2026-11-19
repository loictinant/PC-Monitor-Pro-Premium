# Déclaration de confidentialité — PC Monitor Pro Premium

Dernière révision : 19 novembre 2026

## Données utilisées et stockées

PC Monitor Pro Premium lit localement les compteurs Windows du processeur, de la mémoire, des volumes, des adaptateurs réseau et de l’activité disque. Si le matériel et le pilote le permettent, la version Premium lit également des informations de charge GPU, mémoire vidéo, températures CPU/GPU et ventilateurs via LibreHardwareMonitor.

Dans l’édition gratuite, les relevés du graphique restent en mémoire pendant la session et sont perdus à la fermeture. Avec une licence Premium valide, l’application garde au plus 24 heures de relevés dans `%LOCALAPPDATA%\PCMonitorPro\history.json`, avec un échantillonnage maximal d’un relevé toutes les 15 secondes. Les valeurs peuvent inclure les taux CPU, mémoire, réseau, activité disque et capteurs matériels disponibles, ainsi que le nom des adaptateurs réseau. Le fichier est stocké dans le profil Windows de l’utilisateur et n’est pas chiffré par l’application. Il peut être supprimé manuellement pour effacer cet historique.

Les réglages sont enregistrés dans `%LOCALAPPDATA%\PCMonitorPro\settings.json`. Ils peuvent contenir le thème, l’intervalle de rafraîchissement, les préférences de démarrage et de zone de notification ainsi que les paramètres des alertes. Les notifications personnalisées sont calculées localement. Un export CSV n’est créé que si l’utilisateur le demande et choisit un emplacement.

## Microsoft Store

Dans la version du Microsoft Store, l’application consulte l’état de la licence et demande l’achat de l’extension Premium via les API Microsoft Store. Microsoft traite le compte et le paiement selon ses propres conditions et sa déclaration de confidentialité. L’application ne collecte pas les informations de paiement. L’EXE autonome dirige vers la fiche Microsoft Store et n’effectue pas d’achat intégré.

## Transmission et suivi

L’application ne comprend pas de télémétrie, de publicité, d’outil d’analyse ni de serveur de compte du développeur. Les relevés, les réglages et le fichier d’historique ne sont pas transmis au développeur. Une connexion Internet peut être nécessaire au Microsoft Store pour consulter une licence, acheter ou installer l’application.

## Gestion et contact

Les réglages peuvent être effacés en supprimant `settings.json`; l’historique Premium peut être effacé en supprimant `history.json`. La désinstallation de l’application peut également retirer les données propres au paquet selon le comportement de Windows. Les données de compte et de transaction Store sont gérées par Microsoft.

Pour toute demande relative à la confidentialité, utilisez la page de contact publique : <https://loic-apps.duckdns.org/index.php/contact/>. La politique publique associée à la fiche Store se trouve à <https://loic-apps.duckdns.org/index.php/politique-de-confidentialite-pc-monitor-pro-premium/>.
