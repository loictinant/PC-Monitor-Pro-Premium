# Validation avant distribution — 0.3.0

## Cible et prérequis

- Windows 10 build 17763 ou ultérieur / Windows 11, x64. Le support annoncé doit être vérifié sur les configurations réelles.
- .NET 10 SDK, Visual Studio avec **Développement d’applications WinUI**, Windows SDK 10.0.26100 et outils MSIX pour compiler.
- Partner Center confirme l’identité `TINANTLOIC.PCMonitorProPremium`, éditeur `CN=D67E21A8-DC00-45EF-86D1-375158FB659F`; ID parent `9P34JVNCWKJJ`, extension durable `9MTVFWCM41XS`.

## Contrôles techniques avant soumission

1. Compiler Release; contrôler que l’EXE x64 et le `.msixupload` x64 correspondent tous deux à 0.3.0.
2. Installer localement le MSIX de test signé, ouvrir l’application, puis vérifier l’installation, la mise à jour et la désinstallation. Ne jamais envoyer le paquet auto-signé à Partner Center.
3. Exécuter le Windows App Certification Kit sur le MSIX 0.3.0 signé de test et lire le rapport XML. Le résultat WACK de la version 0.2.3 ne valide pas le nouveau binaire.
4. Sur Windows 10 et 11, contrôler les mesures avec les compteurs système : CPU, mémoire, volumes, débits par interface, activité disque, changements d’adaptateurs et compteurs indisponibles.
5. Vérifier chaque réglage d’actualisation, pause/reprise, thème, démarrage Windows et zone de notification; confirmer que le double-clic rouvre la fenêtre.
6. En mode gratuit, vérifier que chaque entrée (PRO) ouvre le parcours d’achat et qu’aucune alerte ni fonction Premium ne s’exécute.
7. Avec l’app Store et une licence de test/achat valide, vérifier l’activation et la restauration de licence; vérifier les alertes, délai, temporisation, heures calmes, historique local, graphiques, export CSV et navigation après redémarrage.
8. Vérifier les capteurs sur plusieurs appareils GPU/CPU. Les capteurs manquants doivent afficher « non disponible »; aucun droit administrateur ne doit être demandé automatiquement.
9. Examiner l’historique local, l’effacement de `history.json`, l’export CSV choisi par l’utilisateur et la politique de confidentialité publique avant la publication.
10. Tester l’EXE sur un Windows propre : extraction au premier lancement, démarrage standard, SmartScreen/antivirus, chemin contenant des espaces, mise à jour et désinstallation. L’EXE actuel est autonome mais non signé et ne contient pas de programme d’installation.

## Fonction restreinte `runFullTrust`

Si Partner Center redemande la justification (500 caractères maximum), utiliser :

> PC Monitor Pro Premium est une application de bureau WinUI 3 distribuée sous MSIX. runFullTrust est nécessaire pour lancer l’interface Windows, lire localement les compteurs CPU, mémoire, volumes et réseau, surveiller les capteurs accessibles, enregistrer l’historique Premium dans le profil Windows et afficher les notifications. La tâche de démarrage Windows est facultative et désactivable dans les paramètres. Aucune mesure n’est envoyée au développeur.

Ne pas activer `runFullTrust` si l’application ne l’utilise plus. La tâche de démarrage du manifeste doit rester désactivable par l’utilisateur.

## État des validations connues

- La compilation Release de la solution 0.3.0 a été vérifiée avant création des paquets; le résultat WACK, installation locale, essai matériel et certification Store doivent être renseignés à partir des contrôles de cette version uniquement.
- Le rapport WACK 0.2.3 (globalement PASS, avec un échec facultatif informatif) est historique et ne remplace pas un nouveau contrôle de la 0.3.0.
- Le Store signe le paquet après certification. L’EXE autonome publié sur GitHub sera non signé tant qu’une signature de code n’aura pas été obtenue.
