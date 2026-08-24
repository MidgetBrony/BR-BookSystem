using System;
using UnityEditor;
using UnityEngine;

/// <summary>Exports every editable Unity asset required by BR-BookSystem.</summary>
public static class ExportBRBookSystem
{
    /// <summary>
    /// Batch-mode entry point. IncludeDependencies is essential: exporting only
    /// the prefab files omits their meshes, materials, textures, and shaders.
    /// </summary>
    public static void Export()
    {
        string output = Environment.GetEnvironmentVariable("BR_BOOKSYSTEM_UNITYPACKAGE");
        if (string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException("BR_BOOKSYSTEM_UNITYPACKAGE is not set.");

        string[] roots =
        {
            "Assets/Prefabs/BookBox.prefab",
            "Assets/Prefabs/BookReader.prefab",
            "Assets/Prefabs/MediaBox.prefab",
            "Assets/Prefabs/MediaBoxShelf.prefab",
            "Assets/Book-Page Curl - Mine",
            "Assets/BookReaderController.cs",
            "Assets/Editor/BuildBundle.cs",
            "Assets/Editor/ExportBRBookSystem.cs"
        };

        AssetDatabase.ExportPackage(
            roots,
            output,
            ExportPackageOptions.IncludeDependencies | ExportPackageOptions.Recurse);

        Debug.Log("Exported BR-BookSystem Unity assets to: " + output);
    }
}
