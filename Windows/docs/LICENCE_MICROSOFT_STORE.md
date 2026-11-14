# Licence Pro Microsoft Store

## Modèle retenu

- La fiche gratuite est réservée dans Partner Center sous le nom **PC Monitor Pro Premium** (ID Store `9P34JVNCWKJJ`).
- Premium est configuré comme une extension **durable, sans expiration**, achetée une seule fois dans la version Store. Elle déverrouillera les fonctions marquées `(PRO)` à mesure qu’elles sont disponibles dans l’application.
- L’EXE autonome est toujours gratuit. Toute action pour obtenir Pro l’envoie à la fiche PC Monitor Pro Premium du Microsoft Store; l’EXE ne vend pas de licence directement.
- Dans la version 0.2.2, seules les alertes personnalisées sont implémentées. L’historique persistant, les graphiques avancés, le CSV et les données GPU/température restent à développer et ne sont pas annoncés comme disponibles.

## Flux technique

Dans l’application MSIX, `MicrosoftStoreLicenseProvider` crée un `StoreContext` associé à la fenêtre WinUI et lit `StoreAppLicense.AddOnLicenses`. Il compare `StoreLicense.InAppOfferToken` à l’ID produit personnalisé Pro et autorise les alertes seulement si l’extension durable correspondante est présente. La demande d’achat utilise `RequestPurchaseAsync` sur l’ID Store de l’extension. À la réussite, l’application relit l’entitlement et garde les alertes désactivées jusqu’à ce que l’utilisateur les active lui-même.

Dans l’EXE, l’application n’appelle jamais l’achat intégré. Elle ouvre `ms-windows-store://pdp/?ProductId=...` vers l’application Store. Cet ID est obtenu dans Partner Center après création de la fiche de l’application.

## Identifiants à configurer

Les identifiants Store ne sont pas des secrets. Ils ont été relevés dans Partner Center :

- `StoreListingProductId` : `9P34JVNCWKJJ`, ID Store de la fiche PC Monitor Pro Premium. Il sert au renvoi depuis l’EXE.
- `ProAddOnStoreId` : `9MTVFWCM41XS`, ID Store de l’extension Premium. Il sert à lancer l’achat dans le paquet MSIX.
- `ProAddOnProductId` : `PCMONITORPRO_PREMIUM_LIFETIME`, ID produit personnalisé de l’extension. Il sert à identifier l’entitlement relu dans le paquet MSIX.

Les scripts utilisent maintenant ces IDs par défaut :

```powershell
.\scripts\Build-Exe.ps1
.\scripts\Build-Msix.ps1
```

L’extension est créée comme **Durable** dans Partner Center. Dans le brouillon actuel, la durée « Toutes les dates », le prix unique de 1,99 €, la fiche française, les propriétés et l’évaluation d’âge sont enregistrés. Avant de construire le MSIX de distribution, remplacer l’identité et le Publisher du manifeste par les valeurs Partner Center.

## Validation requise avant de vendre

- Avant soumission, vérifier que la durée « Toutes les dates », le prix unique de 1,99 € et la fiche française sont toujours complets dans le brouillon.
- Générer et installer un paquet signé avec l’identité Publisher qui correspond au compte éditeur.
- Vérifier la lecture/restauration de licence, l’achat annulé, les erreurs Store/réseau, l’achat déjà possédé et le lancement hors ligne.
- Tester dans le canal de test Microsoft Store avec l’application installée depuis le Store. Microsoft indique que `Windows.Services.Store` ne fournit pas de simulateur de licences IAP; le flux de licence réel doit donc attendre une association et un téléchargement Store de test.
- Vérifier le renvoi depuis l’EXE vers la bonne fiche Store sur Windows 10 et 11.

À ce stade, la fiche et le module sont créés en brouillon (`9P34JVNCWKJJ` et `9MTVFWCM41XS`). La soumission de l’extension attend la publication de l’application parente et la mise à jour des informations fiscales et de paiement du vendeur. L’identité MSIX est encore `PCMonitorPro.Local` / `CN=PC Monitor Pro Development`; le MSIX précédent est non signé, n’a pas été installé ni certifié par WACK, et aucune soumission Store n’a été faite. Aucun achat et aucune publication Store n’ont été effectués.

Références Microsoft : [achats intégrés et essais](https://learn.microsoft.com/en-us/windows/uwp/monetize/in-app-purchases-and-trials), [lecture des licences d’application et d’extensions](https://learn.microsoft.com/en-us/windows/uwp/monetize/get-license-info-for-apps-and-add-ons), [demande d’achat d’une extension](https://learn.microsoft.com/en-us/windows/uwp/monetize/enable-in-app-purchases-of-apps-and-add-ons), [ouverture d’une fiche Store depuis Windows](https://learn.microsoft.com/fr-fr/windows/apps/develop/launch/launch-store-app).
