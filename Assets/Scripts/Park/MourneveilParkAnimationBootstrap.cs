using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Mourneveil Park - Character Animation v1.0.2 (Art path + serialized sprites)
// Drop-in support for the runtime UI from Park v0.5.1.
// Add this component to _ParkSystems; keep all existing Park scripts unchanged.
// Sprites are serialized and assigned from Assets/Art/ParkAnimations/ in EDIT MODE.
// No Resources.Load: works in both Editor and standalone builds.
[DisallowMultipleComponent]
public sealed class MourneveilParkAnimationBootstrap : MonoBehaviour
{
    [Header("Animation speed (frames per second)")]
    [SerializeField, Range(2f, 16f)] private float npcWalkFps = 7f;
    [SerializeField, Range(2f, 16f)] private float lucienWalkFps = 8f;
    [SerializeField, Range(2f, 18f)] private float lucienRunFps = 12f;
    [SerializeField, Range(2f, 12f)] private float lucienSneakFps = 4f;
    [SerializeField, Range(0.1f, 12f)] private float npcMinMoveSpeed = 2f;

    [Header("Animation frames (96 x 144 PNG, 4 per direction)")]
    [Tooltip("Use component menu > Assign sprites from Art/ParkAnimations, then save the scene.")]
    [SerializeField] private Sprite[] lucienWalkRight = new Sprite[4];
    [SerializeField] private Sprite[] lucienWalkLeft = new Sprite[4];
    [SerializeField] private Sprite[] studentWalkRight = new Sprite[4];
    [SerializeField] private Sprite[] studentWalkLeft = new Sprite[4];
    [SerializeField] private Sprite[] guardWalkRight = new Sprite[4];
    [SerializeField] private Sprite[] guardWalkLeft = new Sprite[4];
    [SerializeField] private Sprite[] wandererWalkRight = new Sprite[4];
    [SerializeField] private Sprite[] wandererWalkLeft = new Sprite[4];

    private const string ArtFolder = "Assets/Art/ParkAnimations/";
    private readonly Character[] characters = new Character[4];
    private bool connected;
    private readonly Sprite[][] rightSets = new Sprite[4][];
    private readonly Sprite[][] leftSets = new Sprite[4][];
    private static readonly string[] CharacterKeys = { "lucien", "student", "guard", "wanderer" };

    private sealed class Character
    {
        public RectTransform root;
        public Image image;
        public MourneveilParkPlayer player;
        public MourneveilParkWalker walker;
        public Sprite[] right;
        public Sprite[] left;
        public Vector2 lastPosition;
        public bool facingRight = true;
        public float cycleTime;
        public Sprite appliedSprite;
    }

    private void Start()
    {
        // Validate serialized references once. Do not use Resources.Load here:
        // PNGs are intentionally in Assets/Art/ParkAnimations, not Resources.
        if (!PrepareFrames())
        {
            enabled = false; // Existing static character sprites remain visible.
            return;
        }

        // Park UI is generated dynamically; wait for it instead of relying on script order.
        StartCoroutine(ConnectWhenReady());
    }

    private IEnumerator ConnectWhenReady()
    {
        for (int attempt = 0; attempt < 240; attempt++)
        {
            GameObject generated = GameObject.Find("Park_Runtime_UI_v05");
            if (generated != null)
            {
                Transform world = generated.transform.Find("Moonlit Park");
                if (world != null && TryConnect(world))
                {
                    connected = true;
                    Debug.Log("[Mourneveil Park] 96x144 character walking animations are active.");
                    yield break;
                }
            }
            yield return null;
        }
        Debug.LogWarning("[Mourneveil Park] Animation sprites loaded, but required runtime " +
                         "objects were not found within 240 frames. Expand Canvas/Park_Runtime_UI_v05/" +
                         "Moonlit Park and check Lucien_Player, Passerby_1, Passerby_2, Passerby_3.");
    }

    private bool TryConnect(Transform world)
    {
        Transform lucienRoot = world.Find("Lucien_Player");
        Transform lucienArt = lucienRoot != null ? lucienRoot.Find("Lucien Sprite") : null;
        if (lucienRoot == null || lucienArt == null) return false;

        Transform studentRoot = world.Find("Passerby_1");
        Transform guardRoot = world.Find("Passerby_2");
        Transform wandererRoot = world.Find("Passerby_3");
        if (studentRoot == null || guardRoot == null || wandererRoot == null) return false;

        // Fail safely before touching ANY existing artwork, if a sprite is missing.
        Character[] found = new Character[4];
        found[0] = CreateCharacter(lucienRoot, lucienArt.GetComponent<Image>(), "lucien", true);
        found[1] = CreateCharacter(studentRoot, studentRoot.GetComponent<Image>(), "student", false);
        found[2] = CreateCharacter(guardRoot, guardRoot.GetComponent<Image>(), "guard", false);
        found[3] = CreateCharacter(wandererRoot, wandererRoot.GetComponent<Image>(), "wanderer", false);
        for (int i = 0; i < found.Length; i++)
            if (found[i] == null) return false;
        for (int i = 0; i < found.Length; i++) characters[i] = found[i];
        return true;
    }

    private Character CreateCharacter(Transform root, Image art, string key, bool isPlayer)
    {
        if (art == null) return null;
        int spriteIndex = System.Array.IndexOf(CharacterKeys, key);
        if (spriteIndex < 0) return null;
        Sprite[] right = rightSets[spriteIndex];
        Sprite[] left = leftSets[spriteIndex];
        if (right == null || left == null) return null;

        RectTransform position = root.GetComponent<RectTransform>();
        if (position == null) return null;
        var entry = new Character
        {
            root = position,
            image = art,
            player = isPlayer ? root.GetComponent<MourneveilParkPlayer>() : null,
            walker = isPlayer ? null : root.GetComponent<MourneveilParkWalker>(),
            right = right,
            left = left,
            lastPosition = position.anchoredPosition
        };
        if ((isPlayer && entry.player == null) || (!isPlayer && entry.walker == null)) return null;
        art.sprite = right[0];
        entry.appliedSprite = right[0];
        return entry;
    }

    // Runtime uses serialized Sprite references: works even after making a game build.
    // ParkPlayer already mirrors Lucien's right-facing Image when moving left,
    // so we deliberately reuse right frames to avoid mirroring twice.
    private bool PrepareFrames()
    {
        rightSets[0] = ValidateFrames(lucienWalkRight, "lucien_walk_R");
        leftSets[0] = rightSets[0];
        rightSets[1] = ValidateFrames(studentWalkRight, "student_walk_R");
        leftSets[1] = ValidateFrames(studentWalkLeft, "student_walk_L");
        rightSets[2] = ValidateFrames(guardWalkRight, "guard_walk_R");
        leftSets[2] = ValidateFrames(guardWalkLeft, "guard_walk_L");
        rightSets[3] = ValidateFrames(wandererWalkRight, "wanderer_walk_R");
        leftSets[3] = ValidateFrames(wandererWalkLeft, "wanderer_walk_L");

        for (int i = 0; i < CharacterKeys.Length; i++)
            if (rightSets[i] == null || leftSets[i] == null) return false;
        return true;
    }

    private static Sprite[] ValidateFrames(Sprite[] frames, string key)
    {
        if (frames == null || frames.Length != 4)
        {
            Debug.LogError("[Mourneveil Park] " + key +
                " needs exactly 4 sprites. Use component menu > " +
                "Assign sprites from Assets/Art/ParkAnimations (EDIT MODE), then save scene.");
            return null;
        }
        for (int i = 0; i < 4; i++)
        {
            if (frames[i] == null)
            {
                Debug.LogError("[Mourneveil Park] Missing Sprite reference: " + key + "_" +
                    i.ToString("00") + ".png. Expected Asset: " + ArtFolder + key + "_" +
                    i.ToString("00") + ".png. Assign from component menu in Edit Mode, " +
                    "then save scene. No Resources folder is needed.");
                return null;
            }
        }
        return frames;
    }

#if UNITY_EDITOR
    [ContextMenu("Assign sprites from Assets/Art/ParkAnimations")]
    private void AssignSpritesFromArtFolder()
    {
        // UnityEditor is compiled only in the Editor. References are serialized,
        // which means runtime and standalone builds NEVER need AssetDatabase.
        lucienWalkRight = ReadEditorFrames("lucien", "R");
        lucienWalkLeft = ReadEditorFrames("lucien", "L");
        studentWalkRight = ReadEditorFrames("student", "R");
        studentWalkLeft = ReadEditorFrames("student", "L");
        guardWalkRight = ReadEditorFrames("guard", "R");
        guardWalkLeft = ReadEditorFrames("guard", "L");
        wandererWalkRight = ReadEditorFrames("wanderer", "R");
        wandererWalkLeft = ReadEditorFrames("wanderer", "L");

        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("[Mourneveil Park] Animation sprite references assigned from " +
                  ArtFolder + ". Save the Park scene (Ctrl+S) before Play/Build.", this);
    }

    private static Sprite[] ReadEditorFrames(string key, string dir)
    {
        Sprite[] result = new Sprite[4];
        for (int i = 0; i < result.Length; i++)
        {
            string file = key + "_walk_" + dir + "_" + i.ToString("00") + ".png";
            string path = ArtFolder + file;
            result[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (result[i] == null)
                Debug.LogError("[Mourneveil Park] Could not assign " + path +
                    ". Check file location, capitalization and Texture Type = Sprite (2D and UI).");
        }
        return result;
    }
#endif

    private void LateUpdate()
    {
        if (!connected) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        for (int i = 0; i < characters.Length; i++)
        {
            Character actor = characters[i];
            if (actor == null || actor.image == null || actor.root == null) continue;

            Vector2 position = actor.root.anchoredPosition;
            Vector2 delta = position - actor.lastPosition;
            actor.lastPosition = position;
            bool moving;
            float rate;

            if (actor.player != null)
            {
                moving = actor.player.IsMoving && !actor.player.EncounterActive;
                rate = actor.player.IsSneaking ? lucienSneakFps
                    : actor.player.IsSprinting ? lucienRunFps : lucienWalkFps;
                // The original player script already flips only Lucien's Image
                // when moving left. Don't flip him again in this animation script.
            }
            else
            {
                moving = actor.walker != null && actor.walker.IsAvailable &&
                         delta.sqrMagnitude > npcMinMoveSpeed * npcMinMoveSpeed * dt * dt;
                rate = npcWalkFps;
                if (delta.x > 0.03f) actor.facingRight = true;
                else if (delta.x < -0.03f) actor.facingRight = false;
            }

            if (!moving) actor.cycleTime = 0f;
            else actor.cycleTime += dt;

            int frameIndex = moving ? Mathf.FloorToInt(actor.cycleTime * rate) % 4 : 0;
            Sprite[] frames = actor.facingRight ? actor.right : actor.left;
            Sprite chosen = frames[frameIndex];
            if (actor.appliedSprite != chosen)
            {
                actor.image.sprite = chosen;
                actor.appliedSprite = chosen;
            }
        }
    }
}
