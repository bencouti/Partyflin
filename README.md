# Partyflin

Plugin Jellyfin (12.3+) pour choisir le film d'une soirée entre amis : vous créez une soirée, vous envoyez le lien, chacun vote depuis son téléphone, **sans compte Jellyfin**.

## Fonctionnalités

- **Sources de films cumulables** pour chaque soirée :
  - tous les films d'un **genre** ;
  - tous les films d'une **bibliothèque** ;
  - une **playlist** créée manuellement dans Jellyfin ;
  - une **collection** (les collections imbriquées sont parcourues) ;
  - des **films individuels**.
- Aperçu de la liste finale avec possibilité de **retirer** des films.
- Nombre maximum de films par votant (ou illimité), date de clôture automatique, clôture/réouverture manuelle.
- Visibilité des résultats : toujours, après avoir voté, ou seulement à la clôture.
- Page de vote publique responsive : affiches, fiche du film (résumé, durée, genres, note), modification du vote, classement en direct.
- Les films sont recalculés à chaque affichage : un film ajouté au genre/à la playlist apparaît automatiquement dans le vote.

## Installation

### Depuis le dépôt de plugins (recommandé)

1. Tableau de bord → **Plugins** → **Dépôts** → **+**, puis ajoutez :
   `https://raw.githubusercontent.com/bencouti/Partyflin/main/manifest.json`
2. **Catalogue** → **Partyflin** → **Installer**, puis redémarrez Jellyfin.

Les mises à jour apparaissent ensuite dans le catalogue comme pour les plugins officiels.

### Manuellement

```bash
dotnet build Jellyfin.Plugin.Partyflin -c Release
```

Copiez `Jellyfin.Plugin.Partyflin/bin/Release/net10.0/Jellyfin.Plugin.Partyflin.dll` dans
`<dossier de données Jellyfin>/plugins/Partyflin_1.0.0.0/` (par ex. `/config/data/plugins/Partyflin_1.0.0.0/` avec l'image Docker officielle), puis redémarrez Jellyfin.

## Utilisation

1. Tableau de bord → **Partyflin** (aussi présent dans le menu latéral).
2. **+ Nouvelle soirée** : titre, message, règles du vote, puis ajoutez des sources via la recherche.
3. Enregistrez et **copiez le lien** : `https://votre-serveur/Partyflin/Vote/<jeton>`.
4. Suivez les résultats depuis le tableau de bord, clôturez quand c'est l'heure.

Si vos amis passent par une autre adresse que la vôtre (reverse proxy, nom de domaine), renseignez l'**URL publique** dans les paramètres du plugin.

## Publier une version

1. Mettez à jour `changelog` dans [build.yaml](build.yaml).
2. Poussez un tag à quatre nombres :
   ```bash
   git tag v1.0.1.0 && git push origin v1.0.1.0
   ```
3. La GitHub Action [release.yml](.github/workflows/release.yml) compile le plugin, attache `partyflin_1.0.1.0.zip` à une Release, puis ajoute la version dans `manifest.json` sur `main`.

Pour générer le zip et le manifest localement (autre hébergement) :

```bash
dotnet run scripts/package.cs -- --base-url https://exemple.com/partyflin
```

## Sécurité

- Les endpoints d'administration (`/Partyflin/Polls`, `/Search`, `/Preview`) sont réservés aux administrateurs.
- Les endpoints de vote (`/Partyflin/Vote/{jeton}/…`) sont anonymes, protégés uniquement par le jeton aléatoire du lien (96 bits). Ils n'exposent que les films de la soirée (titre, année, résumé, affiche).
- La page de vote est servie avec une Content-Security-Policy stricte (script à nonce, pas d'iframe, pas de `Referer`) : même en cas de faille d'affichage, aucun script injecté ne pourrait lire la session Jellyfin d'un administrateur qui ouvre le lien.
- Limites anti-abus : 200 participants par soirée, requêtes de vote limitées à 16 Ko, prénoms de 40 caractères (caractères de contrôle retirés), liste de films mise en cache 30 s.
- Un votant est reconnu par un identifiant stocké dans son navigateur : c'est pratique entre amis, mais rien n'empêche quelqu'un de voter deux fois depuis deux navigateurs.

## Données

Les soirées et les votes sont stockés dans `<dossier de données>/data/partyflin/polls.json`.

## API

| Méthode | Route | Accès |
|---|---|---|
| GET / POST | `/Partyflin/Polls` | admin |
| GET / PUT / DELETE | `/Partyflin/Polls/{id}` | admin |
| GET | `/Partyflin/Polls/{id}/Results` | admin |
| GET | `/Partyflin/Search?type=Genre\|Library\|Playlist\|Collection\|Movie&term=` | admin |
| POST | `/Partyflin/Preview` | admin |
| GET | `/Partyflin/Vote/{jeton}` (page HTML) | public |
| GET | `/Partyflin/Vote/{jeton}/Data?voterId=` | public |
| POST | `/Partyflin/Vote/{jeton}/Ballot` | public |
| GET | `/Partyflin/Vote/{jeton}/Poster/{itemId}` | public |
