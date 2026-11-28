using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Mourneveil v1.0d — The Living Hall.
// A SAFE additive component. Displays real saved vampire servants inside the Castle
// without altering ParkGame, CastleHub, CastleRestoration, saves or the economy.
// Attach to _ParkSystems alongside the existing three components.
[DisallowMultipleComponent]
public sealed class MourneveilCastleResidents : MonoBehaviour
{
    [Header("Existing game state")]
    [SerializeField] private MourneveilParkGame parkGame;

    [Header("Optional 96x144 art — appearances 0, 1, 2")]
    [Tooltip("Assign student_walk_L_00, wanderer_walk_L_00 and guard_walk_L_00 (or your own vampire sprites). Empty slots display transparent pixel-art silhouettes.")]
    [SerializeField] private Sprite[] appearanceSprites = new Sprite[3];

    [Header("Hall presentation")]
    [SerializeField, Range(1, 4)] private int maxVisibleResidents = 4;
    [SerializeField, Range(0.65f, 1.5f)] private float residentScale = 1.0f;
    [SerializeField] private bool gentlyAnimateResidents = true;
    [SerializeField] private bool showHoverDescriptions = true;
    [SerializeField] private bool showNewArrivalNotice = true;

    // CastleHub uses a 1920x1080 CanvasScaler reference.
    // These are near the floor and away from the top HUD and side tabs.
    private static readonly Vector2[] HallLocations =
    {
        new Vector2(785f, 769f),
        new Vector2(1280f, 772f),
        new Vector2(945f, 799f),
        new Vector2(1130f, 803f)
    };

    private static readonly Color TextGold = new Color(0.96f, 0.83f, 0.75f, 1f);
    private static readonly Color TextMuted = new Color(0.79f, 0.67f, 0.68f, 1f);

    private sealed class ResidentSlot
    {
        public RectTransform root;
        public CanvasGroup group;
        public Image portrait;
        public CanvasGroup tooltip;
        public TMP_Text tooltipText;
        public int followerId = -1;
        public bool hovered;
        public bool pinned;
        public float phase;
    }

    private readonly List<ResidentSlot> slots = new List<ResidentSlot>();
    private RectTransform livingLayer;
    private RectTransform castleRoot;
    private Sprite fallbackSprite;
    private Texture2D fallbackTexture;
    private bool ready;
    private int knownFollowerCount;
    private float nextCheck;
    private CanvasGroup arrivalGroup;
    private TMP_Text arrivalText;
    private float arrivalUntil;
    private int pendingArrivals;

    private IEnumerator Start()
    {
        if (parkGame == null) parkGame = GetComponent<MourneveilParkGame>();
        if (parkGame == null)
        {
            Debug.LogError("[Mourneveil Residents] Add this component to _ParkSystems, alongside MourneveilParkGame.");
            enabled = false;
            yield break;
        }

        // CastleHub creates the Canvas during Start, so wait for the real root.
        for (int attempt = 0; attempt < 240 && castleRoot == null; attempt++)
        {
            GameObject host = GameObject.Find("CastleHub_RuntimeCanvas");
            if (host != null) castleRoot = host.transform.Find("CastleHub_Root") as RectTransform;
            if (castleRoot == null) yield return null;
        }
        if (castleRoot == null)
        {
            Debug.LogError("[Mourneveil Residents] CastleHub_Root not found. Enable MourneveilCastleHub on _ParkSystems.");
            enabled = false;
            yield break;
        }

        BuildHall();
        knownFollowerCount = CountFollowers(); // no fake 'new follower' popup on loading an old save
        RefreshResidents();
        ready = true;
    }

    private void Update()
    {
        if (!ready || parkGame == null) return;

        // Saved game state is the single source of truth.
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + 0.25f;
            int current = CountFollowers();
            if (current > knownFollowerCount && showNewArrivalNotice)
                pendingArrivals += current - knownFollowerCount;
            knownFollowerCount = current;
            RefreshResidents();
        }

        // A newly turned vampire may join while Lucien is in the park.
        // Show the notification ONLY when the player next enters the castle.
        if (pendingArrivals > 0 && castleRoot != null && castleRoot.gameObject.activeInHierarchy)
        {
            arrivalText.text = pendingArrivals == 1
                ? "A NEW SOUL JOINS THE HOUSE"
                : "NEW SOULS JOIN THE HOUSE";
            arrivalUntil = Time.unscaledTime + 3.5f;
            pendingArrivals = 0;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            ResidentSlot slot = slots[i];
            if (slot.followerId < 0) continue;
            float alpha = Mathf.MoveTowards(slot.group.alpha, 1f, 3f * Time.unscaledDeltaTime);
            slot.group.alpha = alpha;
            if (gentlyAnimateResidents)
            {
                // Subtle 1–2px idle breathing; not a movement/combat animation.
                Vector2 pos = HallLocations[i];
                pos.y += Mathf.Sin(Time.unscaledTime * 1.35f + slot.phase) * 1.6f;
                slot.root.anchoredPosition = new Vector2(pos.x, -pos.y);
            }
            slot.tooltip.alpha = Mathf.MoveTowards(slot.tooltip.alpha,
                showHoverDescriptions && (slot.hovered || slot.pinned) ? 1f : 0f,
                12f * Time.unscaledDeltaTime);
        }
        if (arrivalGroup != null)
        {
            float desired = showNewArrivalNotice && Time.unscaledTime < arrivalUntil ? 1f : 0f;
            arrivalGroup.alpha = Mathf.MoveTowards(arrivalGroup.alpha, desired,
                4f * Time.unscaledDeltaTime);
        }
    }

    private int CountFollowers()
    {
        ParkProgress data = parkGame != null ? parkGame.State : null;
        return data != null && data.followers != null ? data.followers.Count : 0;
    }

    private void BuildHall()
    {
        livingLayer = NewRect(castleRoot, "Castle Living Hall — Residents");
        Stretch(livingLayer);

        // Place AFTER the restoration lights but BEFORE the header, HUD, tabs and drawers.
        Transform header = castleRoot.Find("Minimal Header");
        if (header != null) livingLayer.SetSiblingIndex(header.GetSiblingIndex());
        else livingLayer.SetSiblingIndex(Mathf.Min(3, castleRoot.childCount - 1));

        // Always have visible placeholder figures even if the Inspector sprite slots are empty.
        // Unlike imported art, these simple silhouettes are created only at runtime.
        fallbackTexture = GenerateSilhouette();
        fallbackSprite = Sprite.Create(fallbackTexture,
            new Rect(0f, 0f, fallbackTexture.width, fallbackTexture.height),
            new Vector2(0.5f, 0.5f), 96f);
        fallbackSprite.hideFlags = HideFlags.DontSave;

        for (int i = 0; i < HallLocations.Length; i++) CreateSlot(i);

        // Small, temporary toast. Never reserve a full-screen menu area.
        RectTransform toast = RectAt(livingLayer, "A New Soul — Small Notice", 726f, 694f, 468f, 54f);
        Image shade = toast.gameObject.AddComponent<Image>();
        shade.color = new Color(0.095f, 0.025f, 0.045f, 0.84f);
        shade.raycastTarget = false;
        arrivalGroup = toast.gameObject.AddComponent<CanvasGroup>();
        arrivalGroup.alpha = 0f;
        arrivalGroup.blocksRaycasts = false;
        arrivalGroup.interactable = false;
        arrivalText = Label(toast, "Arrival Text", "", 20f, TextGold,
            TextAlignmentOptions.Center, 10f, 6f, 448f, 42f);
    }

    private void CreateSlot(int index)
    {
        Vector2 location = HallLocations[index];
        RectTransform slotRoot = RectAt(livingLayer, "Resident Slot " + (index + 1),
            location.x, location.y, 124f, 184f);
        slotRoot.pivot = new Vector2(0.5f, 1f);
        slotRoot.localScale = Vector3.one * residentScale;
        CanvasGroup group = slotRoot.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = true;
        group.interactable = true;

        Image floorShadow = ImageAt(slotRoot, "Floor Shadow", 22f, 160f, 80f, 8f,
            new Color(0.02f, 0.006f, 0.012f, 0.46f));
        floorShadow.raycastTarget = false;

        Image figure = ImageAt(slotRoot, "Vampire Sprite — Hover", 11f, 0f, 102f, 162f,
            Color.white);
        figure.sprite = fallbackSprite;
        figure.preserveAspect = true;
        figure.raycastTarget = true;

        RectTransform tooltipRect = RectAt(slotRoot, "Small Resident Tooltip", -61f, -66f, 246f, 62f);
        Image background = tooltipRect.gameObject.AddComponent<Image>();
        background.color = new Color(0.085f, 0.028f, 0.054f, 0.94f);
        background.raycastTarget = false;
        CanvasGroup tooltip = tooltipRect.gameObject.AddComponent<CanvasGroup>();
        tooltip.alpha = 0f;
        tooltip.blocksRaycasts = false;
        tooltip.interactable = false;
        TMP_Text info = Label(tooltipRect, "Name and Role", "", 16f, TextGold,
            TextAlignmentOptions.Center, 7f, 3f, 232f, 56f);

        ResidentSlot slot = new ResidentSlot
        {
            root = slotRoot, group = group, portrait = figure,
            tooltip = tooltip, tooltipText = info, phase = index * 1.9f
        };
        slots.Add(slot);
        var hitbox = figure.gameObject.AddComponent<MourneveilResidentHover>();
        int capturedIndex = index;
        hitbox.enter = () => SetHovered(capturedIndex, true);
        hitbox.exit = () => SetHovered(capturedIndex, false);
        hitbox.click = () => TogglePinned(capturedIndex);
        slotRoot.gameObject.SetActive(false);
    }

    private void SetHovered(int index, bool value)
    {
        if (index < 0 || index >= slots.Count) return;
        if (slots[index].followerId < 0) return;
        slots[index].hovered = value;
    }

    private void TogglePinned(int index)
    {
        if (index < 0 || index >= slots.Count || slots[index].followerId < 0) return;
        bool next = !slots[index].pinned;
        for (int i = 0; i < slots.Count; i++) slots[i].pinned = i == index && next;
    }

    private void RefreshResidents()
    {
        ParkProgress progress = parkGame.State;
        List<ParkFollower> followers = progress != null ? progress.followers : null;
        int count = followers == null ? 0 : followers.Count;

        for (int i = 0; i < slots.Count; i++)
        {
            ResidentSlot slot = slots[i];
            ParkFollower ally = i < count && i < maxVisibleResidents ? followers[i] : null;
            if (ally == null)
            {
                slot.followerId = -1;
                slot.hovered = false;
                slot.pinned = false;
                slot.root.gameObject.SetActive(false);
                continue;
            }

            bool changed = slot.followerId != ally.id;
            slot.followerId = ally.id;
            if (changed)
            {
                slot.root.gameObject.SetActive(true);
                slot.group.alpha = 0f;
                slot.pinned = false;
                slot.hovered = false;
            }

            Sprite art = FindAppearanceSprite(ally.appearanceIndex);
            slot.portrait.sprite = art != null ? art : fallbackSprite;
            // Tint ONLY the generated fallback, never change the user's pixel art.
            slot.portrait.color = art != null ? Color.white : FallbackTint(ally.role);
            slot.tooltipText.text = SanitizeTitle(ally.title) + "\n" + RoleLabel(ally.role);
            slot.root.localScale = Vector3.one * residentScale;
        }
    }

    private Sprite FindAppearanceSprite(int appearanceIndex)
    {
        if (appearanceSprites == null || appearanceSprites.Length == 0) return null;
        int n = (appearanceIndex % appearanceSprites.Length + appearanceSprites.Length) % appearanceSprites.Length;
        if (appearanceSprites[n] != null) return appearanceSprites[n];
        // If only one slot was assigned, use it for all appearances.
        for (int i = 0; i < appearanceSprites.Length; i++)
            if (appearanceSprites[i] != null) return appearanceSprites[i];
        return null;
    }

    private static string SanitizeTitle(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "An unnamed vampire";
        return name.Length <= 34 ? name : name.Substring(0, 31) + "...";
    }

    private static string RoleLabel(ParkFollowerRole role)
    {
        switch (role)
        {
            case ParkFollowerRole.Hunter: return "HUNTER  /  Blood gathering";
            case ParkFollowerRole.Spy: return "SPY  /  Concealment";
            case ParkFollowerRole.Scholar: return "SCHOLAR  /  Research";
            case ParkFollowerRole.Guardian: return "GUARDIAN  /  Protection";
            default: return "VAMPIRE OF HOUSE MOURNEVEIL";
        }
    }

    private static Color FallbackTint(ParkFollowerRole role)
    {
        switch (role)
        {
            case ParkFollowerRole.Spy: return new Color(0.84f, 0.74f, 0.95f, 1f);
            case ParkFollowerRole.Scholar: return new Color(0.93f, 0.80f, 0.64f, 1f);
            case ParkFollowerRole.Guardian: return new Color(0.77f, 0.88f, 0.93f, 1f);
            default: return Color.white;
        }
    }

    // Only fallback graphic: low-palette, fully transparent 96x144 RGBA.
    // Can be replaced at any time using the optional Sprite slots in Inspector.
    private static Texture2D GenerateSilhouette()
    {
        const int w = 96, h = 144;
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[w * h];
        Color32 coat = new Color32(37, 28, 42, 255);
        Color32 coatBright = new Color32(70, 44, 58, 255);
        Color32 face = new Color32(183, 162, 162, 255);
        Color32 shadow = new Color32(22, 18, 30, 255);
        Color32 crimson = new Color32(142, 41, 63, 255);
        // Pixel-grid silhouettes: cloak / legs / hood / face / red accents.
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int dy = h - 1 - y; // head to feet coordinates (from top)
            Color32 c = new Color32(0, 0, 0, 0);
            int center = 48;
            if (dy >= 44 && dy <= 126)
            {
                int half = 17 + (dy - 44) / 5;
                if (Math.Abs(x - center) < half) c = coat;
                if (Math.Abs(x - (center - 7)) < 3 && dy < 108) c = coatBright;
            }
            if (dy >= 30 && dy <= 63 && Math.Abs(x - center) < 20) c = shadow;
            if (dy >= 33 && dy <= 55 && Math.Abs(x - center) < 12) c = face;
            if (dy >= 24 && dy <= 41 && Math.Abs(x - center) < (21 - Math.Abs(dy - 34) / 3)) c = shadow;
            if (dy >= 43 && dy <= 51 && Math.Abs(x - center) < 16) c = coatBright;
            if (dy >= 55 && dy <= 90 && (Math.Abs(x - 24) < 6 || Math.Abs(x - 72) < 6)) c = coat;
            if (dy >= 115 && dy <= 137 && (Math.Abs(x - 36) < 9 || Math.Abs(x - 60) < 9)) c = shadow;
            if ((dy == 46 || dy == 47) && (x == 44 || x == 52)) c = crimson;
            if (dy >= 70 && dy <= 75 && Math.Abs(x - center) < 19) c = crimson;
            pixels[y * w + x] = c;
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static RectTransform NewRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform t = go.GetComponent<RectTransform>();
        t.SetParent(parent, false);
        return t;
    }

    private static RectTransform RectAt(Transform parent, string name,
        float x, float y, float width, float height)
    {
        RectTransform r = NewRect(parent, name);
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(width, height);
        return r;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    private static Image ImageAt(Transform parent, string name,
        float x, float y, float width, float height, Color color)
    {
        RectTransform r = RectAt(parent, name, x, y, width, height);
        Image img = r.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static TMP_Text Label(Transform parent, string name, string content,
        float size, Color color, TextAlignmentOptions align,
        float x, float y, float width, float height)
    {
        RectTransform r = RectAt(parent, name, x, y, width, height);
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        return t;
    }

    private void OnDestroy()
    {
        if (fallbackSprite != null) Destroy(fallbackSprite);
        if (fallbackTexture != null) Destroy(fallbackTexture);
    }
}

// Small hover/click hit area only on the NPC illustration. The whole castle
// remains visible, and the original slide-out drawers are not modified.
public sealed class MourneveilResidentHover : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Action enter;
    public Action exit;
    public Action click;
    public void OnPointerEnter(PointerEventData eventData) { if (enter != null) enter(); }
    public void OnPointerExit(PointerEventData eventData) { if (exit != null) exit(); }
    public void OnPointerClick(PointerEventData eventData) { if (click != null) click(); }
}
