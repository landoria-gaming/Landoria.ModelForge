# Créer le projet Unity à la main

Ce guide reproduit le résultat de `New-ValheimUnityProject.ps1` avec Unity Hub, l'éditeur Unity et l'Explorateur Windows. Le projet démarre sans scène Valheim. Les DLL sont liées aux fichiers Steam et les prefabs et shaders sont lus dans les AssetBundles du jeu, sans copier ces derniers.

## Prérequis

- Unity **6000.0.75f1** installé dans Unity Hub.
- Valheim installé, par défaut dans `C:\Program Files (x86)\Steam\steamapps\common\Valheim`.
- Un dossier de projet **sur le même disque** que Valheim : les liens matériels Windows ne traversent pas les disques.
- Les six fichiers C# et le modèle JSON présents à côté de ce guide.

Unity ne sait pas créer des liens matériels ni parcourir les AssetBundles comme des dossiers depuis la fenêtre **Project**. Les étapes 2 à 4 se font donc dans l'Explorateur Windows et, pour les liens, dans `cmd.exe`. Le reste se fait dans Unity.

## 1. Créer le projet

Dans Unity Hub, clique **New project**, choisis un modèle **3D Built-in Render Pipeline** avec Unity **6000.0.75f1**, puis crée par exemple `C:\Unity\ValheimEmpty`. Évite un modèle URP ou HDRP : Valheim utilise le pipeline Built-in. Ferme Unity après la première ouverture, avant d'ajouter les DLL.

Unity Hub crée `Assets`, `Packages` et `ProjectSettings`. Les modules Unity courants sont déjà fournis par cette version de l'éditeur. Le script PowerShell utilise la commande `-createProject`, donc les fichiers initiaux d'un modèle Unity Hub peuvent différer légèrement.

## 2. Préparer les dossiers et l'index

Dans le nouveau projet, crée ces dossiers :

```text
Assets\Plugins\Valheim
Assets\Editor
ValheimDependencies\Native
```

Copie [ValheimDependencies.template.json](ValheimDependencies.template.json) dans `ValheimDependencies` et renomme la copie `index.json`. Ouvre-la avec un éditeur de texte et adapte `gameData` si Valheim n'est pas installé au chemin indiqué. Le modèle correspond à l'installation Valheim **1.0.17** inspectée : il énumère **48 DLL gérées**, **10 DLL natives** et **61 shaders compilés**.

Le script produit cet index en lisant `valheim_Data\StreamingAssets\SoftRef\manifest_extended`. Pour refaire l'index après une mise à jour du jeu, relève chaque ligne `path in bundle: ... .shader` et le `bundle:` qui la précède, puis actualise `compiledShaders` dans le JSON.

## 3. Lier les DLL du jeu

Lis les listes `managedHardLinks` et `nativeHardLinks` dans `index.json`. Pour **chaque** nom de `managedHardLinks`, crée un lien matériel dans `Assets\Plugins\Valheim` vers le fichier de même nom dans `valheim_Data\Managed`. Pour chaque nom de `nativeHardLinks`, crée le lien sous `ValheimDependencies\Native` vers `valheim_Data\Plugins\x86_64`.

L'Explorateur Windows ne crée pas ces liens. Ouvre `cmd.exe` et utilise `mklink /H` pour chaque DLL. Exemple :

```bat
mklink /H "C:\Unity\ValheimEmpty\Assets\Plugins\Valheim\assembly_postprocessing.dll" "C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed\assembly_postprocessing.dll"
```

N'ajoute pas les DLL `UnityEngine.*` du cœur du jeu ni les DLL `System.*` : Unity fournit déjà ses propres versions. Les trois exceptions `UnityEngine.UI.dll`, `UnityEngine.SpatialTracking.dll` et `UnityEngine.XR.LegacyInputHelpers.dll` figurent dans la liste à lier. Les DLL natives restent hors de `Assets` : elles sont présentes pour inspection, mais ne sont pas chargées automatiquement par l'éditeur.

Si tu préfères une opération dans l'Explorateur, tu peux **copier** les DLL listées dans les mêmes dossiers ; le projet fonctionnera, mais elles occuperont de l'espace et ne référenceront plus directement les fichiers Steam. Il n'existe pas d'action équivalente dans l'interface Unity.

## 4. Ajouter les outils Unity

Copie ces trois fichiers dans `Assets` :

- [ValheimBundleLibrary.cs](ValheimBundleLibrary.cs)
- [ValheimShaderLibrary.cs](ValheimShaderLibrary.cs)
- [ValheimLinkedPrefab.cs](ValheimLinkedPrefab.cs)

Copie ces trois fichiers dans `Assets\Editor` :

- [ValheimShaderMenu.cs](ValheimShaderMenu.cs)
- [ValheimPrefabMenu.cs](ValheimPrefabMenu.cs)
- [ValheimPrefabBrowser.cs](ValheimPrefabBrowser.cs)

Rouvre le projet dans Unity Hub et attends la fin de l'import. Vérifie que la fenêtre **Console** ne montre pas d'erreur de compilation.

## 5. Vérifier et utiliser

1. Dans Unity, clique **Valheim > Load compiled shaders from game**. La Console doit indiquer `Valheim shaders loaded: 61; unavailable: 0`.
2. Fais **File > New Scene**, puis choisis **Basic (Built-in)**.
3. Clique **Valheim > Browse bundle prefabs**, cherche `Pickable_Branch` et clique sur le résultat.
4. Sélectionne `Pickable_Branch` dans **Hierarchy**, place la souris sur la vue **Scene** et appuie sur **F** pour le cadrer.
5. Enregistre la scène avec **Ctrl+S**.

Le navigateur lit les chemins des prefabs dans le manifeste Steam, puis charge le prefab choisi directement depuis son bundle. `ValheimLinkedPrefab` permet de recharger ses références à l'ouverture de la scène. Le prefab n'apparaît pas comme un `.prefab` éditable dans **Project** : les bundles restent des fichiers compilés externes.

Après une mise à jour Steam qui remplace des DLL, recrée les liens matériels concernés. Un lien existant peut continuer à pointer vers l'ancienne version du fichier.
