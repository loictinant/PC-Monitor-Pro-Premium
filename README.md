# PC Monitor Pro Premium

Application Windows locale construite en C#, WinUI 3 et .NET 10 pour Windows 10 et 11. Les sources et les scripts de compilation sont dans [`Windows/`](Windows/).

![Aperçu du tableau de bord PC Monitor Pro](Windows/docs/screenshots/dashboard-example.png)

*Illustration du tableau de bord avec des valeurs fictives.*

## Version 0.3.0

La version gratuite suit le processeur, la mémoire, les volumes fixes, les débits réseau par adaptateur et l’activité disque. Premium (PRO) ajoute l’historique local 24 h, des graphiques CPU/mémoire/réseau/disque sur plusieurs périodes, l’export CSV, des alertes configurables et les capteurs disponibles selon le matériel.

L’EXE de test gratuit est publié dans [GitHub Releases](https://github.com/loictinant/PC-Monitor-Pro-Premium/releases/tag/v0.3.0). Il n’est pas signé : Windows peut afficher un avertissement à son lancement. Le build Premium de test simule l’achat et ne doit pas être distribué. L’achat réel Premium s’effectue uniquement dans la version Microsoft Store.

La fiche de l’application Microsoft Store est [PC Monitor Pro Premium](https://apps.microsoft.com/detail/9P34JVNCWKJJ). La mise à jour Store 0.3.0 est en préparation; la version actuellement disponible est indiquée sur la fiche.

Les détails d’installation, de compilation, d’architecture, de confidentialité et de limites matérielles sont dans [`Windows/README.md`](Windows/README.md) et [`Windows/docs/`](Windows/docs/).

## Licence

Le dépôt ne définit pas de licence de réutilisation pour le code de l’application. Les avis tiers et la licence MPL-2.0 de LibreHardwareMonitor sont fournis dans `Windows/src/PCMonitor.App/Assets/`.
