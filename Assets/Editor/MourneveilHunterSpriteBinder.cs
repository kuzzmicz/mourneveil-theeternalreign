#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-click assigner for the 96x144 PNGs in Assets/Art/ParkAnimations.
// Menu: Tools > Mourneveil > Assign Hunter Sprites (v0.9b)
public static class MourneveilHunterSpriteBinder
{
    private const string Directory = "Assets/Art/ParkAnimations/";

    [MenuItem("Tools/Mourneveil/Assign Hunter Sprites (v0.9b)")]
    public static void Assign()
    {
        MourneveilHunter hunter = Object.FindFirstObjectByType<MourneveilHunter>(FindObjectsInactive.Include);
        if (hunter == null)
        {
            EditorUtility.DisplayDialog("Mourneveil", "Open the Park scene and add MourneveilHunter to _ParkSystems first.", "OK");
            return;
        }

        SerializedObject serializedHunter = new SerializedObject(hunter);
        int assigned = 0;
        assigned += SetFrames(serializedHunter, "walkRightFrames", "hunter_walk_R_");
        assigned += SetFrames(serializedHunter, "walkLeftFrames", "hunter_walk_L_");
        assigned += SetFrames(serializedHunter, "runRightFrames", "hunter_run_R_");
        assigned += SetFrames(serializedHunter, "runLeftFrames", "hunter_run_L_");
        SetSprite(serializedHunter, "idleRight", "hunter_idle_R.png");
        SetSprite(serializedHunter, "idleLeft", "hunter_idle_L.png");
        SetSprite(serializedHunter, "runRight", "hunter_run_R_00.png");
        SetSprite(serializedHunter, "runLeft", "hunter_run_L_00.png");
        serializedHunter.ApplyModifiedProperties();
        EditorUtility.SetDirty(hunter);
        if (hunter.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(hunter.gameObject.scene);
        Debug.Log("[Mourneveil] Hunter sprite frames assigned: " + assigned + "/16.");
        EditorUtility.DisplayDialog("Mourneveil", "Assigned " + assigned + "/16 hunter animation frames. Save the Park scene!", "OK");
    }

    private static int SetFrames(SerializedObject obj, string field, string prefix)
    {
        SerializedProperty prop = obj.FindProperty(field);
        if (prop == null) return 0;
        prop.arraySize = 4;
        int count = 0;
        for (int i = 0; i < 4; i++)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Directory + prefix + i.ToString("00") + ".png");
            prop.GetArrayElementAtIndex(i).objectReferenceValue = sprite;
            if (sprite != null) count++;
        }
        return count;
    }

    private static void SetSprite(SerializedObject obj, string field, string filename)
    {
        SerializedProperty prop = obj.FindProperty(field);
        if (prop != null)
            prop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Directory + filename);
    }
}
#endif
