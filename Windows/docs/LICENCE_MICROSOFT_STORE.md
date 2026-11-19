# Licence Pro Microsoft Store

## Modèle retenu

- La fiche gratuite est réservée dans Partner Center sous le nom **PC Monitor Pro Premium** (ID Store `9P34JVNCWKJJ`).
- Premium est configuré comme une extension **durable, sans expiration**, achetée une seule fois dans la version Store. Elle déverrouillera les fonctions marquées `(PRO)` à mesure qu’elles sont disponibles dans l’application.
- L’EXE autonome est toujours gratuit. Toute action pour obtenir Pro l’envoie à la fiche PC Monitor Pro Premium du Microsoft Store; l’EXE ne vend pas de licence directement.
- Dans la version 0.2.3, seules les alertes personnalisées sont implémentées. L’historique persistant, les graphiques avancés, le CSV et les données GPU/température restent à développer et ne sont pas annoncés comme disponibles.

## Flux technique

Dans l’application MSIX, `MicrosoftStoreLicenseProvider` crée un `StoreContext` associé à la fenêtre WinUI et lit `StoreAppLicense.AddOnLicenses`. Il compare `StoreLicense.InAppOfferToken` à l’ID produit personnalisé Pro et autorise les alertes seulement si l’extension durable correspondante est présente. La demande d’achat utilise `RequestPurchaseAsync` sur l’ID Store de l’extension. À la réussite, l’application relit l’entitlement et garde les alertes désactivées jusqu’à ce que l’utilisateur les active lui-même.

Dans l’EXE, l’application n’appelle jamais l’achat intégré. Elle ouvre `ms-windows-store://pdp/?ProductId=...` vers l’application Store. Cet ID est obtenu dans Partner Center après création de la fiche de l’application.

## Identifiants à configurer

Les identifiants Store ne sont pas des secrets. Ils ont été relevés dans Partner Center :

- `StoreListingProductId` : `9P34JVNCWKJJ`, ID Store de la fiche PC Monitor Pro Premium. Il sert au renvoi depuis l’EXE.
- `ProAddOnStoreId` : `9MTVFWCM41XS`, ID Store de l’extension Premium. Il sert à lancer l’achat dans le paquet MSIX.
- `ProAddOnProductId` : `PCMONITORPRO_PREMIUM_LIFETIME`, ID produit personnalisé de l’extension. Il sert à identifier l’entitlement relu dans le paquet MSIX.

Identité de package relevée dans Partner Center :

- `Package/Identity/Name` : `TINANTLOIC.PCMonitorProPremium`
- `Package/Identity/Publisher` : `CN=D67E21A8-DC00-45EF-86D1-375158FB659F`
- `Package/Properties/PublisherDisplayName` : `TINANT LOIC`

Les scripts utilisent maintenant ces IDs par défaut :

```powershell
.\scripts\Build-Exe.ps1
.\scripts\Build-Msix.ps1
```

L’extension est créée comme **Durable** dans Partner Center. Dans le brouillon actuel, la durée « Toutes les dates », le prix unique de 1,99 €, la fiche française, les propriétés et l’évaluation d’âge sont enregistrés. Le manifeste et les propriétés MSBuild utilisent maintenant les valeurs d’identité Partner Center indiquées ci-dessus. Le candidat destiné au Store reste non signé; le paquet auto-signé local est dans un autre dossier et ne doit pas être soumis.

## Validation requise avant de vendre

- Avant soumission, vérifier que la durée « Toutes les dates », le prix unique de 1,99 € et la fiche française sont toujours complets dans le brouillon.
- Générer le paquet Store associé au produit, puis faire un essai local avec un paquet signé par un certificat de développement dont le sujet correspond exactement à l’identité Publisher. Le script d'installation n'installe pas automatiquement de certificat.
- Vérifier la lecture/restauration de licence, l’achat annulé, les erreurs Store/réseau, l’achat déjà possédé et le lancement hors ligne.
- Tester dans le canal de test Microsoft Store avec l’application installée depuis le Store. Microsoft indique que `Windows.Services.Store` ne fournit pas de simulateur de licences IAP; le flux de licence réel doit donc attendre une association et un téléchargement Store de test.
- Vérifier le renvoi depuis l’EXE vers la bonne fiche Store sur Windows 10 et 11.

À ce stade, la fiche et le module sont créés en brouillon (`9P34JVNCWKJJ` et `9MTVFWCM41XS`). Le fichier `.msixupload` x64 0.2.3 se trouve dans `artifacts/PCMonitorPro-msix-upload-v0.2.3/` et n’a pas été soumis. Le WACK du MSIX de test local 0.2.3 indique PASS avec un seul échec facultatif informatif; voir `artifacts/PCMonitorPro-WACK-v0.2.3.xml`. L’URL de confidentialité de la fiche parente manque encore. La soumission de l’extension attend la publication de l’application parente et la mise à jour des informations fiscales et de paiement du vendeur. Le parcours de licence réel doit être vérifié depuis une installation Store. Aucun achat, aucune soumission et aucune publication Store n’ont été effectués.

Références Microsoft : [achats intégrés et essais](https://learn.microsoft.com/en-us/windows/uwp/monetize/in-app-purchases-and-trials), [lecture des licences d’application et d’extensions](https://learn.microsoft.com/en-us/windows/uwp/monetize/get-license-info-for-apps-and-add-ons), [demande d’achat d’une extension](https://learn.microsoft.com/en-us/windows/uwp/monetize/enable-in-app-purchases-of-apps-and-add-ons), [ouverture d’une fiche Store depuis Windows](https://learn.microsoft.com/fr-fr/windows/apps/develop/launch/launch-store-app).
