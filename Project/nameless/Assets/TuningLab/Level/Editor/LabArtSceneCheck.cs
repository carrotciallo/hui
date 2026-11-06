using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Verify selected art imports and every checked-in level's shared preview path.</summary>
public static class LabArtSceneCheck
{
    public static void RunBatch()
    {
        try
        {
            int textures = 0, scenes = 0, presenters = 0;
            foreach (string path in Directory.GetFiles("Assets/TuningLab/Art/Resources/HuiArt", "*.png", SearchOption.AllDirectories))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.spritePixelsPerUnit != 16 || importer.filterMode != FilterMode.Point
                    || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed)
                    throw new Exception("Invalid pixel import: " + path);
                if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null) throw new Exception("Missing sprite: " + path);
                textures++;
            }
            foreach (string path in Directory.GetFiles("Assets/Scenes/Levels", "*.unity"))
            {
                if (path.EndsWith("Level1_Playtest.unity")) continue; // local user playtest, not part of this PR
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var level in UnityEngine.Object.FindObjectsByType<LabLevel>())
                {
                    level.RebuildPreview();
                    foreach (var art in level.GetComponentsInChildren<LabArtPresenter>(true))
                    {
                        art.Refresh();
                        var child = art.transform.Find("SelectedArt");
                        if (child == null || child.GetComponent<SpriteRenderer>().sprite == null)
                            throw new Exception("Missing art in " + path + ": " + art.name);
                        presenters++;
                    }
                }
                scenes++;
            }
            if (textures < 20 || presenters == 0) throw new Exception("No selected art coverage");
            Debug.Log($"[LabArtSceneCheck] PASS: {textures} sprites, {scenes} scenes, {presenters} skinned preview objects.");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}
