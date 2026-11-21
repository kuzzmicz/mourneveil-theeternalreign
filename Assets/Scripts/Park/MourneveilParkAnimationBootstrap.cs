using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Mourneveil Park - Character Animation v1.0
// Drop-in support for the runtime UI from Park v0.5.1.
// Add this component to _ParkSystems; keep all existing Park scripts unchanged.
// Expects Sprite PNG files in Assets/Resources/Mourneveil/ParkAnimations/.
[DisallowMultipleComponent]
public sealed class MourneveilParkAnimationBootstrap : MonoBehaviour
{
    [Header("Animation speed (frames per second)")]
    [SerializeField, Range(2f, 16f)] private float npcWalkFps = 7f;
    [SerializeField, Range(2f, 16f)] private float lucienWalkFps = 8f;
    [SerializeField, Range(2f, 18f)] private float lucienRunFps = 12f;
    [SerializeField, Range(2f, 12f)] private float lucienSneakFps = 4f;
    [SerializeField, Range(0.1f, 12f)] private float npcMinMoveSpeed = 2f;

    private const string SpritesFolder = "Mourneveil/ParkAnimations/";
    private readonly Character[] characters = new Character[4];
    private bool connected;

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
        // Park UI is created dynamically by MourneveilParkGame.Start(),
        // so wait for it instead of relying on component execution order.
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
        Debug.LogWarning("[Mourneveil Park] Animation setup could not find the runtime park. " +
                         "Open scene Park and check that Park v0.5.1 is installed.");
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
        Sprite[] right = LoadFrames(key, "R");
        Sprite[] left = isPlayer ? right : LoadFrames(key, "L");
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

    private static Sprite[] LoadFrames(string key, string direction)
    {
        Sprite[] loaded = new Sprite[4];
        for (int i = 0; i < loaded.Length; i++)
        {
            string path = SpritesFolder + key + "_walk_" + direction + "_" + i.ToString("00");
            loaded[i] = Resources.Load<Sprite>(path);
            if (loaded[i] == null)
            {
                Debug.LogWarning("[Mourneveil Park] Missing Sprite at Resources/" + path +
                                 ". Check Texture Type = Sprite (2D and UI).");
                return null;
            }
        }
        return loaded;
    }

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
