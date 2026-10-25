# ModelForge

Export static Valheim models to Blender and preview edited GLB files in the game.

## Controls

- Press **Left Shift + F** to open ModelForge. Change `OpenShortcut` in `Landoria.ModelForge.cfg` to use another key; edits to the config file are reloaded while the game runs.
- Aim at an object and select **Export** to write a `.glb` and `.jpg` thumbnail.
- Select a `.glb` from the dropdown and choose **Import**. Aim, scroll to rotate, click to place, or press Escape to cancel.
- Aim at a placed ModelForge model and choose **Erase** to remove it.

The models folder is `BepInEx/config/ModelForge/Models` in the active profile. Open an exported GLB in Blender with **File > Import > glTF 2.0**. Export edited models from Blender as GLB into the same folder.

Placed models are local and temporary. They disappear when the game closes and are not sent to other players or saved in the world.

## Current limits

- Exports static `MeshFilter` and `MeshRenderer` objects. Skinned meshes, animation, terrain, particles, and game scripts are excluded.
- Base color and main textures are exported. Valheim shader effects, normal maps, and other material properties are not reproduced.
- Import supports embedded GLB meshes with triangle primitives, float positions, optional float normals and UVs, PNG/JPEG base color images, and common index formats. Some Blender options and external images are unsupported.
- Test placement on a disposable world. Imported objects are visual previews with a local box collider.

## Build

Set `ValheimGamePath` and `BepInExPath` to accessible game and BepInEx directories, then run `dotnet msbuild Landoria.ModelForge.csproj -p:Configuration=Release`. Copy the built DLL to the active client profile's `BepInEx/plugins/ModelForge` folder. The mod does not require a server installation.
