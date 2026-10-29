using UnityEditor;

internal static class ValheimShaderMenu
{
    [MenuItem("Valheim/Load compiled shaders from game")]
    private static void LoadCompiledShaders()
    {
        ValheimShaderLibrary.LoadAll();
    }
}
