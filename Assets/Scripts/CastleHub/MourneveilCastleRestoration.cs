using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Mourneveil v1.0c — A Dynasty Awakens
// Safe visual addition to the v1.0b Castle Hub, without touching ParkGame, its
// save file, existing objectives, park UI, input, or character controllers.
// Add this component to _ParkSystems alongside MourneveilCastleHub.
[DisallowMultipleComponent]
public sealed class MourneveilCastleRestoration : MonoBehaviour
{
    [Header("Castle progress (read-only from the existing Park save)")]
    [SerializeField] private MourneveilParkGame parkGame;

    [Header("Visuals — subtle by default")]
    [SerializeField, Range(0.10f, 0.65f)] private float initialShadowOpacity = 0.34f;
    [SerializeField, Range(0.10f, 1f)] private float candleGlowStrength = 0.65f;
    [SerializeField] private bool showRestoredHouseSigil = true;
    [SerializeField] private bool showMilestoneNotices = true;
    [SerializeField, Range(1f, 8f)] private float appearanceTransitionSpeed = 2.5f;

    [Header("Play Mode testing ONLY (-1 = real saved progress)")]
    [SerializeField, Range(-1, 5)] private int previewStageOverride = -1;

    private RectTransform restorationLayer;
    private Image veil;
    private Image glowFrontLeft;
    private Image glowFrontRight;
    private Image glowRearLeft;
    private Image glowRearRight;
    private Image coffinGlow;
    private CanvasGroup sigilGroup;
    private CanvasGroup noticeGroup;
    private TMP_Text noticeTitle;
    private TMP_Text noticeSubtitle;

    private Texture2D glowTexture;
    private Texture2D ringTexture;
    private Sprite glowSprite;
    private Sprite ringSprite;

    private float currentStage;
    private int lastSavedStage;
    private int pendingMilestone = -1;
    private float noticeVisibleUntil = -1f;
    private float nextStateCheck;
    private bool ready;

    private IEnumerator Start()
    {
        if (parkGame == null) parkGame = GetComponent<MourneveilParkGame>();
        if (parkGame == null)
        {
            Debug.LogError("[Mourneveil Restoration] Add to _ParkSystems with MourneveilParkGame.");
            enabled = false;
            yield break;
        }

        // CastleHub constructs its own Canvas after ParkGame has created its UI.
        // The Canvas host remains active even while the Castle root is hidden.
        Transform castleRoot = null;
        for (int attempt = 0; attempt < 240 && castleRoot == null; attempt++)
        {
            GameObject canvasHost = GameObject.Find("CastleHub_RuntimeCanvas");
            if (canvasHost != null)
                castleRoot = canvasHost.transform.Find("CastleHub_Root");
            if (castleRoot == null) yield return null;
        }
        if (castleRoot == null)
        {
            Debug.LogError("[Mourneveil Restoration] CastleHub_Root was not found. " +
                "Add MourneveilCastleHub to _ParkSystems first.");
            enabled = false;
            yield break;
        }

        CreateVisuals(castleRoot);
        lastSavedStage = ReadSavedStage();
        currentStage = previewStageOverride >= 0 ? previewStageOverride : lastSavedStage;
        ApplyVisuals(currentStage, true);
        ready = true;
    }

    private void Update()
    {
        if (!ready || parkGame == null) return;

        if (Time.unscaledTime >= nextStateCheck)
        {
            nextStateCheck = Time.unscaledTime + 0.15f;
            int savedStage = ReadSavedStage();
            if (savedStage > lastSavedStage)
                pendingMilestone = savedStage;
            lastSavedStage = savedStage;
        }

        int target = previewStageOverride >= 0 ? previewStageOverride : lastSavedStage;
        currentStage = Mathf.MoveTowards(currentStage, target,
            appearanceTransitionSpeed * Time.unscaledDeltaTime);
        ApplyVisuals(currentStage, false);

        // Milestone attained during a hunt? Wait until the Castle becomes visible.
        if (pendingMilestone >= 0 && previewStageOverride < 0 &&
            restorationLayer != null && restorationLayer.gameObject.activeInHierarchy)
        {
            if (showMilestoneNotices) ShowMilestone(pendingMilestone);
            pendingMilestone = -1;
        }
        if (noticeGroup != null)
        {
            bool visible = showMilestoneNotices &&
                Time.unscaledTime < noticeVisibleUntil && previewStageOverride < 0;
            noticeGroup.alpha = Mathf.MoveTowards(noticeGroup.alpha,
                visible ? 1f : 0f, 3f * Time.unscaledDeltaTime);
        }
    }

    private int ReadSavedStage()
    {
        ParkProgress progress = parkGame != null ? parkGame.State : null;
        if (progress == null) return 0;
        int followers = progress.followers == null ? 0 : progress.followers.Count;
        int routes = progress.supplyNetworkLevel;

        // The same goal order as the v1.0b Castle objective system.
        if (routes < 1) return 0;
        if (followers < 1) return 1;
        if (routes < 2) return 2;
        if (followers < 2) return 3;
        if (routes < 3) return 4;
        return 5; // HOUSE REBORN: two vampires AND Supply Lv. 3
    }

    private void CreateVisuals(Transform castleRoot)
    {
        restorationLayer = NewRect(castleRoot, "Castle Restoration — Living Hall");
        Stretch(restorationLayer);
        // Crucial: just above scene background and mood tint, BELOW all gameplay UI.
        restorationLayer.SetSiblingIndex(Mathf.Min(2, castleRoot.childCount - 1));

        glowTexture = MakeRadialTexture(96, false);
        ringTexture = MakeRadialTexture(96, true);
        glowSprite = MakeSprite(glowTexture);
        ringSprite = MakeSprite(ringTexture);

        veil = NewImage(restorationLayer, "The Unrestored Hall Shadow");
        Stretch(veil.rectTransform);
        veil.color = new Color(0.015f, 0.006f, 0.015f, initialShadowOpacity);

        // Canvas coordinates in the 1920x1080 reference resolution. The generated
        // candle glow follows the candle groups in CastleHub_Background.png.
        glowFrontLeft = GlowAt("Candlelight — Left", 450, 552, 340, 330, new Color(1f, 0.60f, 0.26f, 0f));
        glowFrontRight = GlowAt("Candlelight — Right", 1463, 558, 340, 330, new Color(1f, 0.58f, 0.25f, 0f));
        glowRearLeft = GlowAt("Candlelight — Rear Left", 690, 439, 250, 235, new Color(1f, 0.70f, 0.39f, 0f));
        glowRearRight = GlowAt("Candlelight — Rear Right", 1249, 440, 250, 235, new Color(1f, 0.68f, 0.34f, 0f));
        coffinGlow = GlowAt("Royal Coffin — Awakening", 975, 625, 570, 360,
            new Color(0.82f, 0.13f, 0.19f, 0f));

        // A small architectural sigil in the existing central stained-glass
        // window. It does NOT cover the central park/castle scene or steal clicks.
        RectTransform sigil = RectAt(restorationLayer, "Mourneveil Sigil — Unlocked", 851, 210, 218, 210);
        sigilGroup = sigil.gameObject.AddComponent<CanvasGroup>();
        sigilGroup.alpha = 0f;
        sigilGroup.interactable = false;
        sigilGroup.blocksRaycasts = false;
        Image halo = NewImage(sigil, "Crimson Sigil Halo");
        SetRect(halo.rectTransform, 9, 0, 200, 190);
        halo.sprite = glowSprite;
        halo.color = new Color(0.72f, 0.18f, 0.20f, 0.75f);
        Image ring = NewImage(sigil, "Gilded Mourneveil Circle");
        SetRect(ring.rectTransform, 46, 22, 126, 126);
        ring.sprite = ringSprite;
        ring.color = new Color(0.97f, 0.70f, 0.46f, 0.80f);
        TextAt(sigil, "The Mourneveil M", "M", 68, 0, 45, 218, 82,
            new Color(0.98f, 0.86f, 0.75f), TextAlignmentOptions.Center);
        TextAt(sigil, "Restored Crest Caption", "HOUSE MOURNEVEIL", 16, 0, 152, 218, 27,
            new Color(0.97f, 0.83f, 0.72f), TextAlignmentOptions.Center);

        // A brief, non-blocking notification, not another permanent panel.
        RectTransform message = RectAt(restorationLayer, "Subtle Milestone Toast", 725, 808, 470, 86);
        noticeGroup = message.gameObject.AddComponent<CanvasGroup>();
        noticeGroup.alpha = 0f;
        noticeGroup.interactable = false;
        noticeGroup.blocksRaycasts = false;
        Image messageShade = NewImage(message, "Milestone Velvet Shade");
        Stretch(messageShade.rectTransform);
        messageShade.color = new Color(0.065f, 0.018f, 0.047f, 0.84f);
        noticeTitle = TextAt(message, "Milestone Title", "", 24, 8, 8, 454, 36,
            new Color(1f, 0.85f, 0.77f), TextAlignmentOptions.Center);
        noticeSubtitle = TextAt(message, "Milestone Subtitle", "", 15, 8, 49, 454, 27,
            new Color(0.91f, 0.76f, 0.71f), TextAlignmentOptions.Center);
    }

    private Image GlowAt(string name, float x, float y, float width, float height, Color color)
    {
        Image image = NewImage(restorationLayer, name);
        image.sprite = glowSprite;
        image.color = color;
        image.type = Image.Type.Simple;
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0f, 1f);
        image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        image.rectTransform.anchoredPosition = new Vector2(x, -y);
        image.rectTransform.sizeDelta = new Vector2(width, height);
        return image;
    }

    private void ApplyVisuals(float stage, bool immediately)
    {
        // Stage zero: darkened fallen hall. Stage five: reclaimed hall.
        float phase = Mathf.Clamp01(stage / 5f);
        float shadowAlpha = Mathf.Lerp(initialShadowOpacity, 0.02f, phase);
        if (veil != null)
            veil.color = new Color(0.015f, 0.006f, 0.015f, shadowAlpha);

        float warmGlow = candleGlowStrength * Mathf.Lerp(0.03f, 0.61f, phase);
        SetGlow(glowFrontLeft, warmGlow * 0.55f);
        SetGlow(glowFrontRight, warmGlow * 0.55f);
        SetGlow(glowRearLeft, warmGlow * 0.48f);
        SetGlow(glowRearRight, warmGlow * 0.48f);
        float chapterReveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stage - 4f));
        SetGlow(coffinGlow, chapterReveal * candleGlowStrength * 0.48f);

        if (sigilGroup != null)
        {
            float alpha = showRestoredHouseSigil ? chapterReveal : 0f;
            sigilGroup.alpha = alpha;
        }
    }

    private static void SetGlow(Image image, float alpha)
    {
        if (image == null) return;
        Color c = image.color;
        c.a = Mathf.Clamp01(alpha);
        image.color = c;
    }

    private void ShowMilestone(int stage)
    {
        if (noticeGroup == null || noticeTitle == null || noticeSubtitle == null) return;
        switch (stage)
        {
            case 1:
                noticeTitle.text = "THE FIRST FLAME";
                noticeSubtitle.text = "Your supply routes bring life to the hall.";
                break;
            case 2:
                noticeTitle.text = "A BLOODLINE BEGINS";
                noticeSubtitle.text = "Your first vampire has joined the house.";
                break;
            case 3:
                noticeTitle.text = "THE HALL STIRS";
                noticeSubtitle.text = "The supply network grows stronger.";
                break;
            case 4:
                noticeTitle.text = "NO LONGER ALONE";
                noticeSubtitle.text = "Two vampires now serve House Mourneveil.";
                break;
            default:
                noticeTitle.text = "HOUSE REBORN";
                noticeSubtitle.text = "The first hall of Mourneveil has awakened.";
                break;
        }
        noticeVisibleUntil = Time.unscaledTime + 4f;
    }

    private static Texture2D MakeRadialTexture(int size, bool ring)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
            float dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float a;
            if (ring)
            {
                float d = Mathf.Abs(r - 0.70f);
                a = Mathf.Clamp01(1f - d / 0.060f);
                a = a * a * (3f - 2f * a);
            }
            else
            {
                a = Mathf.Pow(Mathf.Clamp01(1f - r), 2f);
            }
            byte opacity = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);
            // White pixels are consistently tinted by Image.color. Only alpha
            // determines the visible edge; pixels outside the circle are clear.
            pixels[y * size + x] = new Color32(255, 255, 255, opacity);
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return tex;
    }

    private static Sprite MakeSprite(Texture2D tex)
    {
        Sprite result = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f);
        result.hideFlags = HideFlags.DontSave;
        return result;
    }

    private static RectTransform NewRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform r = go.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        return r;
    }

    private static Image NewImage(Transform parent, string name)
    {
        RectTransform r = NewRect(parent, name);
        Image image = r.gameObject.AddComponent<Image>();
        image.raycastTarget = false; // important for the background click-to-dismiss
        return image;
    }

    private static RectTransform RectAt(Transform parent, string name,
        float x, float y, float width, float height)
    {
        RectTransform r = NewRect(parent, name);
        SetRect(r, x, y, width, height);
        return r;
    }

    private static void SetRect(RectTransform r, float x, float y, float width, float height)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    private static TMP_Text TextAt(Transform parent, string name, string text,
        int size, float x, float y, float width, float height,
        Color color, TextAlignmentOptions alignment)
    {
        RectTransform r = RectAt(parent, name, x, y, width, height);
        TextMeshProUGUI label = r.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private void OnDestroy()
    {
        // The generated sprites/textures are runtime-only; no imported assets
        // or PNGs are ever modified. Don't delete them before the Canvas uses
        // them during this frame; Unity's Destroy is deferred to frame end.
        if (glowSprite != null) Destroy(glowSprite);
        if (ringSprite != null) Destroy(ringSprite);
        if (glowTexture != null) Destroy(glowTexture);
        if (ringTexture != null) Destroy(ringTexture);
    }
}
