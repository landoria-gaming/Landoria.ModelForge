# ModelForge
Export static Valheim models as GLB files, then preview edited files locally in the game.
- **Left Shift + F** opens the menu; **Export GLB** saves the model and live material settings in `BepInEx/config/ModelForge/Models`.
- **Generate Unity scene** builds a neutral-background preview with the captured camera, lighting, materials and post-processing; Unity loads game shaders from the local Valheim installation when available.
- **Import** places a selected GLB locally (wheel rotates, click places, Escape cancels); **Erase** removes it. Animated models are unsupported.
