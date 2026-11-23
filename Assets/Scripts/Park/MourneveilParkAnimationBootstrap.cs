using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Mourneveil Park v0.7 — Shadows in Motion.
// Replacement for the v1.0.2 AnimationBootstrap ONLY.
// Does not modify hunting, AI, confrontation logic, saves, or movement controllers.
// Drag and drop 32 new 96x144 PNGs into Assets/Art/ParkAnimations, then run the
// EDIT MODE component context menu "Assign sprites from Assets/Art/ParkAnimations".
[DisallowMultipleComponent]
public sealed class MourneveilParkAnimationBootstrap : MonoBehaviour
{
    [Header("Animation speed (frames per second)")]
    [SerializeField, Range(2f, 16f)] private float npcWalkFps = 7f;
    [SerializeField, Range(2f, 16f)] private float lucienWalkFps = 8f;
    [SerializeField, Range(2f, 20f)] private float lucienRunFps = 12f;
    [SerializeField, Range(2f, 12f)] private float lucienSneakFps = 5f;
    [SerializeField, Range(2f, 20f)] private float npcFleeFps = 12f;
    [SerializeField, Range(0.1f, 12f)] private float npcMinMoveSpeed = 2f;

    [Header("Existing walk animations (four frames per direction)")]
    [SerializeField] private Sprite[] lucienWalkRight = new Sprite[4];
    [SerializeField] private Sprite[] lucienWalkLeft = new Sprite[4];
    [SerializeField] private Sprite[] studentWalkRight = new Sprite[4];
    [SerializeField] private Sprite[] studentWalkLeft = new Sprite[4];
    [SerializeField] private Sprite[] guardWalkRight = new Sprite[4];
    [SerializeField] private Sprite[] guardWalkLeft = new Sprite[4];
    [SerializeField] private Sprite[] wandererWalkRight = new Sprite[4];
    [SerializeField] private Sprite[] wandererWalkLeft = new Sprite[4];

    [Header("NEW v0.7 — Lucien's own sneak and run cycles")]
    [SerializeField] private Sprite[] lucienSneakRight = new Sprite[4];
    [SerializeField] private Sprite[] lucienRunRight = new Sprite[4];

    [Header("NEW v0.7 — fleeing NPCs, right / left")]
    [SerializeField] private Sprite[] studentRunRight = new Sprite[4];
    [SerializeField] private Sprite[] studentRunLeft = new Sprite[4];
    [SerializeField] private Sprite[] guardRunRight = new Sprite[4];
    [SerializeField] private Sprite[] guardRunLeft = new Sprite[4];
    [SerializeField] private Sprite[] wandererRunRight = new Sprite[4];
    [SerializeField] private Sprite[] wandererRunLeft = new Sprite[4];

    [Header("Confrontation feedback")]
    [SerializeField] private bool showConfrontationExclamation = true;
    [SerializeField, Range(0f, 1f)] private float confrontationRedPulse = 0.22f;

    private const string ArtFolder = "Assets/Art/ParkAnimations/";
    private static readonly string[] Keys = { "lucien", "student", "guard", "wanderer" };
    private readonly Character[] characters = new Character[4];
    private readonly Sprite[][] walkR = new Sprite[4][];
    private readonly Sprite[][] walkL = new Sprite[4][];
    private readonly Sprite[][] runR = new Sprite[4][];
    private readonly Sprite[][] runL = new Sprite[4][];
    private Sprite[] sneakR;
    private bool connected;

    private enum Motion { Idle, Walk, Run, Sneak, Confront }

    private sealed class Character
    {
        public RectTransform root;
        public Image artwork;
        public MourneveilParkPlayer player;
        public MourneveilParkWalker walker;
        public Sprite[] walkRight, walkLeft, runRight, runLeft, sneakRight;
        public Vector2 lastPosition;
        public bool facingRight = true;
        public Motion previousMotion = Motion.Idle;
        public float cycleTime;
        public Sprite currentSprite;
        public TMP_Text confrontMarker;
    }

    private void Start()
    {
        if (!PrepareFrames())
        {
            enabled = false;  // Preserve original sprites if imports are wrong.
            return;
        }
        StartCoroutine(ConnectWhenReady());
    }

    private IEnumerator ConnectWhenReady()
    {
        // Runtime UI is generated AFTER scene load. Frame-count timeout avoids spam.
        for (int attempt = 0; attempt < 240; attempt++)
        {
            GameObject generated = GameObject.Find("Park_Runtime_UI_v05");
            if (generated != null)
            {
                Transform world = generated.transform.Find("Moonlit Park");
                if (world != null && TryConnect(world))
                {
                    connected = true;
                    Debug.Log("[Mourneveil Park] v0.7 Shadows in Motion animations active.", this);
                    yield break;
                }
            }
            yield return null;
        }
        Debug.LogWarning("[Mourneveil Park] v0.7 couldn't connect to the runtime park. " +
                         "Expected Park_Runtime_UI_v05 / Moonlit Park / Lucien_Player and Passerby_1..3.", this);
    }

    private bool TryConnect(Transform world)
    {
        Transform lucienRoot = world.Find("Lucien_Player");
        Transform lucienArt = lucienRoot != null ? lucienRoot.Find("Lucien Sprite") : null;
        Transform studentRoot = world.Find("Passerby_1");
        Transform guardRoot = world.Find("Passerby_2");
        Transform wandererRoot = world.Find("Passerby_3");
        if (lucienArt == null || studentRoot == null || guardRoot == null || wandererRoot == null) return false;

        Character[] found = new Character[4];
        found[0] = CreateCharacter(lucienRoot, lucienArt.GetComponent<Image>(), 0);
        found[1] = CreateCharacter(studentRoot, studentRoot.GetComponent<Image>(), 1);
        found[2] = CreateCharacter(guardRoot, guardRoot.GetComponent<Image>(), 2);
        found[3] = CreateCharacter(wandererRoot, wandererRoot.GetComponent<Image>(), 3);
        for (int i = 0; i < found.Length; i++) if (found[i] == null) return false;
        for (int i = 0; i < found.Length; i++) characters[i] = found[i];
        return true;
    }

    private Character CreateCharacter(Transform root, Image image, int index)
    {
        if (root == null || image == null) return null;
        RectTransform rect = root.GetComponent<RectTransform>();
        if (rect == null) return null;
        var actor = new Character
        {
            root = rect,
            artwork = image,
            player = index == 0 ? root.GetComponent<MourneveilParkPlayer>() : null,
            walker = index == 0 ? null : root.GetComponent<MourneveilParkWalker>(),
            walkRight = walkR[index],
            walkLeft = walkL[index],
            runRight = runR[index],
            runLeft = runL[index],
            sneakRight = index == 0 ? sneakR : null,
            lastPosition = rect.anchoredPosition
        };
        if (index == 0 && actor.player == null) return null;
        if (index != 0 && actor.walker == null) return null;
        // Retain existing static artworks if an optional cycle wasn't assigned.
        actor.currentSprite = image.sprite = walkR[index][0];
        if (index > 0 && showConfrontationExclamation)
            actor.confrontMarker = CreateConfrontMarker(rect);
        return actor;
    }

    private static TMP_Text CreateConfrontMarker(RectTransform parent)
    {
        var g = new GameObject("Confrontation Exclamation", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rt = g.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(42f, 45f);
        rt.anchoredPosition = new Vector2(0f, 244f); // above existing Awareness label
        var t = g.GetComponent<TextMeshProUGUI>();
        t.text = "!";
        t.fontSize = 29f;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(1f, 0.46f, 0.40f, 0.95f);
        t.raycastTarget = false;
        g.SetActive(false);
        return t;
    }

    private bool PrepareFrames()
    {
        walkR[0] = ValidateRequired(lucienWalkRight, "lucien_walk_R");
        walkL[0] = walkR[0]; // Player already flips its own Image for facing left.
        walkR[1] = ValidateRequired(studentWalkRight, "student_walk_R");
        walkL[1] = ValidateRequired(studentWalkLeft, "student_walk_L");
        walkR[2] = ValidateRequired(guardWalkRight, "guard_walk_R");
        walkL[2] = ValidateRequired(guardWalkLeft, "guard_walk_L");
        walkR[3] = ValidateRequired(wandererWalkRight, "wanderer_walk_R");
        walkL[3] = ValidateRequired(wandererWalkLeft, "wanderer_walk_L");
        for (int i = 0; i < 4; i++)
            if (walkR[i] == null || walkL[i] == null) return false;

        // All v0.7 animations are optional: fallback to walk instead of crashing.
        sneakR = ValidateOptional(lucienSneakRight, "lucien_sneak_R", walkR[0]);
        runR[0] = ValidateOptional(lucienRunRight, "lucien_run_R", walkR[0]);
        runL[0] = runR[0];
        runR[1] = ValidateOptional(studentRunRight, "student_run_R", walkR[1]);
        runL[1] = ValidateOptional(studentRunLeft, "student_run_L", walkL[1]);
        runR[2] = ValidateOptional(guardRunRight, "guard_run_R", walkR[2]);
        runL[2] = ValidateOptional(guardRunLeft, "guard_run_L", walkL[2]);
        runR[3] = ValidateOptional(wandererRunRight, "wanderer_run_R", walkR[3]);
        runL[3] = ValidateOptional(wandererRunLeft, "wanderer_run_L", walkL[3]);
        return true;
    }

    private Sprite[] ValidateRequired(Sprite[] frames, string key)
    {
        if (Valid(frames)) return frames;
        Debug.LogError("[Mourneveil Park] Missing required walk sprites: " + key +
                       "_00..03. In Edit Mode use component menu > Assign sprites from Assets/Art/ParkAnimations.", this);
        return null;
    }

    private Sprite[] ValidateOptional(Sprite[] frames, string key, Sprite[] fallback)
    {
        if (Valid(frames)) return frames;
        Debug.LogWarning("[Mourneveil Park] Optional v0.7 sprites missing: " + key +
                         "_00..03. Using previous walk frames until you assign them.", this);
        return fallback;
    }

    private static bool Valid(Sprite[] s)
    {
        if (s == null || s.Length != 4) return false;
        foreach (Sprite frame in s) if (frame == null) return false;
        return true;
    }

#if UNITY_EDITOR
    [ContextMenu("Assign sprites from Assets/Art/ParkAnimations")]
    private void AssignSpritesFromArtFolder()
    {
        // Work only in Unity Editor; serialized references work in the built game.
        lucienWalkRight = ReadEditorFrames("lucien_walk_R");
        lucienWalkLeft = ReadEditorFrames("lucien_walk_L");
        studentWalkRight = ReadEditorFrames("student_walk_R");
        studentWalkLeft = ReadEditorFrames("student_walk_L");
        guardWalkRight = ReadEditorFrames("guard_walk_R");
        guardWalkLeft = ReadEditorFrames("guard_walk_L");
        wandererWalkRight = ReadEditorFrames("wanderer_walk_R");
        wandererWalkLeft = ReadEditorFrames("wanderer_walk_L");
        lucienSneakRight = ReadEditorFrames("lucien_sneak_R");
        lucienRunRight = ReadEditorFrames("lucien_run_R");
        studentRunRight = ReadEditorFrames("student_run_R");
        studentRunLeft = ReadEditorFrames("student_run_L");
        guardRunRight = ReadEditorFrames("guard_run_R");
        guardRunLeft = ReadEditorFrames("guard_run_L");
        wandererRunRight = ReadEditorFrames("wanderer_run_R");
        wandererRunLeft = ReadEditorFrames("wanderer_run_L");

        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("[Mourneveil Park] Animation sprites assigned from " + ArtFolder +
                  ". Save Park scene (Ctrl+S) before Play.", this);
    }

    private static Sprite[] ReadEditorFrames(string prefix)
    {
        var sprites = new Sprite[4];
        for (int i = 0; i < 4; i++)
        {
            string assetPath = ArtFolder + prefix + "_" + i.ToString("00") + ".png";
            sprites[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprites[i] == null)
                Debug.LogError("[Mourneveil Park] Missing sprite: " + assetPath +
                               ". Check import settings: Sprite (2D and UI), Single.");
        }
        return sprites;
    }
#endif

    private void LateUpdate()
    {
        if (!connected) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        foreach (Character actor in characters)
        {
            if (actor == null || actor.root == null || actor.artwork == null) continue;
            Vector2 pos = actor.root.anchoredPosition;
            Vector2 delta = pos - actor.lastPosition;
            actor.lastPosition = pos;
            Motion motion;
            float fps;
            Sprite[] frames;

            if (actor.player != null)
            {
                if (actor.player.EncounterActive || !actor.player.IsMoving) motion = Motion.Idle;
                else if (actor.player.IsSneaking) motion = Motion.Sneak;
                else if (actor.player.IsSprinting) motion = Motion.Run;
                else motion = Motion.Walk;
                fps = motion == Motion.Sneak ? lucienSneakFps :
                      motion == Motion.Run ? lucienRunFps : lucienWalkFps;
                frames = motion == Motion.Sneak ? actor.sneakRight :
                         motion == Motion.Run ? actor.runRight : actor.walkRight;
                // The existing player script handles Image flip. Do not mirror again.
            }
            else
            {
                bool available = actor.walker.IsAvailable;
                bool fleeing = actor.walker.IsFleeing;
                bool confronting = actor.walker.IsConfronting;
                if (fleeing && available) motion = Motion.Run;
                else if (confronting && available) motion = Motion.Confront;
                else if (available && delta.sqrMagnitude >
                         npcMinMoveSpeed * npcMinMoveSpeed * dt * dt) motion = Motion.Walk;
                else motion = Motion.Idle;
                if (delta.x > 0.03f) actor.facingRight = true;
                else if (delta.x < -0.03f) actor.facingRight = false;
                fps = motion == Motion.Run ? npcFleeFps : npcWalkFps;
                if (motion == Motion.Run)
                    frames = actor.facingRight ? actor.runRight : actor.runLeft;
                else frames = actor.facingRight ? actor.walkRight : actor.walkLeft;
                UpdateConfrontationVisual(actor, confronting && available);
            }

            if (motion != actor.previousMotion)
            {
                actor.previousMotion = motion;
                actor.cycleTime = 0f; // Restart each state to avoid an abrupt mid-cycle pose.
            }
            if (motion != Motion.Idle && motion != Motion.Confront) actor.cycleTime += dt;
            int index = motion == Motion.Idle || motion == Motion.Confront ? 0 :
                        Mathf.FloorToInt(actor.cycleTime * fps) % 4;
            Sprite next = frames[index];
            if (actor.currentSprite != next)
            {
                actor.currentSprite = next;
                actor.artwork.sprite = next;
            }
        }
    }

    private void UpdateConfrontationVisual(Character actor, bool confronting)
    {
        // Red heartbeat pulse complements, but does not override, the v0.6 choice panel.
        if (actor.confrontMarker != null)
        {
            if (actor.confrontMarker.gameObject.activeSelf != confronting)
                actor.confrontMarker.gameObject.SetActive(confronting);
            if (confronting)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 11f);
                actor.confrontMarker.color = new Color(1f, Mathf.Lerp(0.24f, 0.7f, wave), 0.44f, 1f);
                actor.confrontMarker.rectTransform.localScale = Vector3.one * (1f + 0.17f * wave);
            }
        }
        if (confronting && confrontationRedPulse > 0f)
        {
            Color original = actor.artwork.color;
            Color tint = new Color(1f, 0.58f, 0.65f, original.a);
            float wave = (0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.time * 9f))) * confrontationRedPulse;
            Color result = Color.Lerp(original, tint, wave);
            result.a = original.a; // Invisible/respawning walkers stay invisible.
            actor.artwork.color = result;
        }
    }
}
