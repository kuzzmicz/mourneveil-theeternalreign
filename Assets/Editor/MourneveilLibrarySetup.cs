#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// v1.1 optional setup tool. Run only AFTER the source patch has been applied.
public static class MourneveilLibrarySetup
{
    private const string BackgroundPath = "Assets/Art/CastleHub/ForgottenLibrary_Background.png";

    [MenuItem("Tools/Mourneveil/Setup Forgotten Library (v1.1)")]
    public static void Setup()
    {
        GameObject systems = GameObject.Find("_ParkSystems");
        if (systems == null)
        {
            EditorUtility.DisplayDialog("Mourneveil Library", "Open your Park scene first. _ParkSystems was not found.", "OK");
            return;
        }
        if (systems.GetComponent<MourneveilParkGame>() == null ||
            systems.GetComponent<MourneveilCastleHub>() == null)
        {
            EditorUtility.DisplayDialog("Mourneveil Library",
                "_ParkSystems must have MourneveilParkGame and MourneveilCastleHub already installed.", "OK");
            return;
        }

        TextureImporter importer = AssetImporter.GetAtPath(BackgroundPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
        Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
        if (background == null)
        {
            EditorUtility.DisplayDialog("Mourneveil Library", "Background not found at " + BackgroundPath, "OK");
            return;
        }

        MourneveilForgottenLibrary library = systems.GetComponent<MourneveilForgottenLibrary>();
        if (library == null) library = Undo.AddComponent<MourneveilForgottenLibrary>(systems);
        SerializedObject serialized = new SerializedObject(library);
        serialized.FindProperty("parkGame").objectReferenceValue = systems.GetComponent<MourneveilParkGame>();
        serialized.FindProperty("libraryBackground").objectReferenceValue = background;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(library);
        EditorSceneManager.MarkSceneDirty(systems.scene);
        Selection.activeGameObject = systems;
        Debug.Log("[Mourneveil Library] Installed on _ParkSystems. Save the scene and press Play.");
        EditorUtility.DisplayDialog("Mourneveil Library",
            "Library configured! Save your Park scene and press Play. Unlock condition: 2 vampires + Supply Lv. 3.", "OK");
    }
}
#endif
