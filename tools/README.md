# Empty Valheim Unity project

For the equivalent manual setup, see [MANUAL_SETUP.md](MANUAL_SETUP.md).

Run from this folder in Windows PowerShell:

```powershell
.\New-ValheimUnityProject.ps1 -Destination 'C:\Unity\ValheimEmpty'
```

The script uses Unity 6000.0.75f1 and the local Steam installation by default. Pass `-Unity` or `-Game` to override either path. The destination must be empty and on the same drive as Valheim because DLLs use hard links. Open the project in Unity, then choose **Valheim > Load compiled shaders from game**. `ValheimShaderLibrary.Find("Custom/Player")` returns a loaded game shader.

The scene starts empty. Use **Valheim > Browse bundle prefabs** to search the game's prefabs and place one in the scene, or **Valheim > Add Pickable_Branch to scene** to place a branch at the origin. The project's game shaders load from the installed AssetBundles; they are compiled shaders, so Unity cannot save them as editable shader source assets. Native game DLLs are linked under `ValheimDependencies/Native` for inspection and are not loaded into the Editor automatically. After a Steam update that replaces game files, create a fresh project to refresh the hard links.
