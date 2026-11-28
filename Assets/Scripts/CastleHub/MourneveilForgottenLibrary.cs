using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Mourneveil v1.1 — The Forgotten Library.
// This is an additive room inside the EXISTING CastleHub canvas.
// Research is purchased through MourneveilParkGame and saved as a bitmask
// INSIDE the existing ParkProgress JSON: no second economy / no second save.
// IMPORTANT: apply Tools/ApplyLibraryPatch.py to MourneveilParkGame.cs and
// MourneveilCastleHub.cs before importing this component.
[DisallowMultipleComponent]
public sealed class MourneveilForgottenLibrary : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MourneveilParkGame parkGame;
    [SerializeField] private Sprite libraryBackground;

    [Header("Interaction")]
    [SerializeField, Range(0.25f, 1.5f)] private float hoverCloseDelay = 0.55f;
    [SerializeField, Range(400f, 2800f)] private float slideSpeed = 1550f;

    private static readonly int[] ResearchBits = { 1, 2, 4 };
    private static readonly int[] Prices = { 140, 190, 260 };
    private static readonly string[] ResearchTitles =
    {
        "CRIMSON LEDGER", "VEIL OF SILENCE", "FORBIDDEN VITAE"
    };
    private static readonly string[] Effects =
    {
        "+15% to all Blood/sec (including offline)",
        "+0.15 Suspicion recovery per second",
        "+8 percentage points to Turning Chance"
    };

    private static readonly Color Ink = new Color(0.055f, 0.019f, 0.034f, 0.92f);
    private static readonly Color InkFaint = new Color(0.055f, 0.019f, 0.034f, 0.67f);
    private static readonly Color Burgundy = new Color(0.42f, 0.11f, 0.21f, 0.97f);
    private static readonly Color Gold = new Color(0.96f, 0.84f, 0.77f, 1f);
    private static readonly Color Muted = new Color(0.79f, 0.66f, 0.68f, 1f);
    private static readonly Color Green = new Color(0.68f, 0.90f, 0.75f, 1f);

    private RectTransform castleRoot;
    private RectTransform libraryRoot;
    private RectTransform drawer;
    private CanvasGroup drawerGroup;
    private CanvasGroup hintGroup;
    private CanvasGroup messageGroup;
    private TMP_Text doorLabel;
    private CanvasGroup hallToastGroup;
    private TMP_Text hallToastText;
    private float hallToastUntil;
    private TMP_Text headerResource;
    private TMP_Text archiveProgress;
    private TMP_Text hintText;
    private TMP_Text messageText;
    private TMP_Text[] stateLabels = new TMP_Text[3];
    private TMP_Text[] purchaseLabels = new TMP_Text[3];
    private Button[] purchaseButtons = new Button[3];
    private bool ready;
    private bool isOpen;
    private bool drawerVisible;
    private bool drawerPinned;
    private bool pointerOverTable;
    private bool pointerOverDrawer;
    private float closeAt = -1f;
    private float nextRefresh;
    private float messageUntil;
    private bool previousUnlocked;

    public bool IsOpen { get { return isOpen; } }

    private IEnumerator Start()
    {
        if (parkGame == null) parkGame = GetComponent<MourneveilParkGame>();
        if (parkGame == null)
        {
            Debug.LogError("[Mourneveil Library] Attach this component to _ParkSystems with MourneveilParkGame.");
            enabled = false;
            yield break;
        }

        for (int attempts = 0; attempts < 240 && castleRoot == null; attempts++)
        {
            GameObject host = GameObject.Find("CastleHub_RuntimeCanvas");
            if (host != null) castleRoot = host.transform.Find("CastleHub_Root") as RectTransform;
            if (castleRoot == null) yield return null;
        }
        if (castleRoot == null)
        {
            Debug.LogError("[Mourneveil Library] CastleHub_Root missing. Install MourneveilCastleHub first.");
            enabled = false;
            yield break;
        }
        BuildRoom();
        previousUnlocked = IsUnlocked();
        RefreshUI();
        ready = true;
    }

    private bool IsUnlocked()
    {
        ParkProgress progress = parkGame != null ? parkGame.State : null;
        if (progress == null) return false;
        int count = progress.followers == null ? 0 : progress.followers.Count;
        return count >= 2 && progress.supplyNetworkLevel >= 3;
    }

    private int CountResearched()
    {
        if (parkGame == null || parkGame.State == null) return 0;
        int mask = parkGame.State.libraryResearchMask;
        int count = 0;
        foreach (int bit in ResearchBits) if ((mask & bit) != 0) count++;
        return count;
    }

    private void Update()
    {
        if (!ready) return;

        if (hallToastGroup != null)
            hallToastGroup.alpha = Mathf.MoveTowards(hallToastGroup.alpha,
                Time.unscaledTime < hallToastUntil ? 1f : 0f, 5f * Time.unscaledDeltaTime);

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 0.18f;
            bool unlocked = IsUnlocked();
            if (unlocked != previousUnlocked && unlocked && castleRoot.gameObject.activeInHierarchy)
                ShowMessage("THE FORGOTTEN LIBRARY IS NOW OPEN", 3.2f);
            previousUnlocked = unlocked;
            RefreshUI();
        }
        if (!isOpen) return;
        if (castleRoot == null || !castleRoot.gameObject.activeInHierarchy)
        {
            CloseLibrary(); // H can return to Park; no ghost room on next visit.
            return;
        }

        if (drawerVisible && !drawerPinned && !pointerOverTable && !pointerOverDrawer &&
            closeAt >= 0f && Time.unscaledTime > closeAt)
            HideResearch();

        if (drawer != null && drawerGroup != null)
        {
            Vector2 destination = new Vector2(drawerVisible ? 1425f : 1965f, -173f);
            drawer.anchoredPosition = Vector2.MoveTowards(drawer.anchoredPosition,
                destination, slideSpeed * Time.unscaledDeltaTime);
            drawerGroup.alpha = Mathf.MoveTowards(drawerGroup.alpha,
                drawerVisible ? 1f : 0f, 7f * Time.unscaledDeltaTime);
            drawerGroup.blocksRaycasts = drawerVisible;
            drawerGroup.interactable = drawerVisible;
        }
        if (hintGroup != null)
            hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha,
                drawerVisible ? 0f : 0.85f, 5f * Time.unscaledDeltaTime);
        if (messageGroup != null)
        {
            messageGroup.alpha = Mathf.MoveTowards(messageGroup.alpha,
                Time.unscaledTime < messageUntil ? 1f : 0f, 5f * Time.unscaledDeltaTime);
        }
    }

    private void TryEnterLibrary()
    {
        if (!IsUnlocked())
        {
            ShowMessage("SEALED — RECRUIT 2 VAMPIRES AND REACH SUPPLY LV. 3", 3f);
            return;
        }
        if (castleRoot == null || !castleRoot.gameObject.activeInHierarchy) return;
        isOpen = true;
        libraryRoot.gameObject.SetActive(true);
        libraryRoot.SetAsLastSibling();
        HideResearch();
        RefreshUI();
    }

    public void CloseLibrary()
    {
        isOpen = false;
        HideResearch();
        if (libraryRoot != null) libraryRoot.gameObject.SetActive(false);
    }

    private void ShowResearch(bool pin)
    {
        if (!isOpen) return;
        if (pin && drawerPinned && drawerVisible) { HideResearch(); return; }
        drawerVisible = true;
        if (pin) drawerPinned = true;
        closeAt = -1f;
        RefreshUI();
    }

    private void HideResearch()
    {
        drawerVisible = false;
        drawerPinned = false;
        pointerOverTable = false;
        pointerOverDrawer = false;
        closeAt = -1f;
    }

    private void ScheduleClose()
    {
        if (!drawerPinned) closeAt = Time.unscaledTime + hoverCloseDelay;
    }

    private void Purchase(int index)
    {
        if (index < 0 || index >= ResearchBits.Length) return;
        bool succeeded = parkGame.BuyLibraryResearch(ResearchBits[index]);
        if (succeeded)
        {
            ShowMessage("RESEARCH COMPLETED — " + ResearchTitles[index], 3.1f);
            RefreshUI();
        }
        else ShowMessage("MORE BLOOD IS REQUIRED — OR RESEARCH ALREADY KNOWN", 2.4f);
    }

    private void RefreshUI()
    {
        if (parkGame == null || parkGame.State == null) return;
        bool unlocked = IsUnlocked();
        if (doorLabel != null)
            doorLabel.text = unlocked ? "THE FORGOTTEN LIBRARY  >" : "SEALED LIBRARY  [LOCKED]";
        if (headerResource != null)
            headerResource.text = "BLOOD  " + parkGame.State.blood.ToString("0.0") +
                "    |    +" + parkGame.BloodPerSecond.ToString("0.00") + "/SEC";
        if (archiveProgress != null)
            archiveProgress.text = "FORBIDDEN RESEARCH  " + CountResearched() + " / 3";
        for (int i = 0; i < ResearchBits.Length; i++)
        {
            bool owned = (parkGame.State.libraryResearchMask & ResearchBits[i]) != 0;
            if (stateLabels[i] != null)
                stateLabels[i].text = owned ? "RESEARCHED" : "COST  " + Prices[i] + " BLOOD";
            if (purchaseLabels[i] != null)
                purchaseLabels[i].text = owned ? "DISCOVERED" :
                    "RESEARCH  -  " + Prices[i] + " BLOOD";
            if (purchaseButtons[i] != null)
                purchaseButtons[i].interactable = !owned &&
                    parkGame.State.currentHuman == null &&
                    parkGame.State.blood >= Prices[i];
        }
    }

    private void ShowMessage(string text, float duration)
    {
        // A locked door needs feedback INSIDE the Castle, not inside the hidden room.
        if (!isOpen)
        {
            if (hallToastText != null) hallToastText.text = text;
            hallToastUntil = Time.unscaledTime + duration;
            return;
        }
        if (messageText != null) messageText.text = text;
        messageUntil = Time.unscaledTime + duration;
    }

    private void BuildRoom()
    {
        // SMALL entrance badge inside Castle scenery, NO always-on wide menu.
        RectTransform door = RectAt(castleRoot, "Forgotten Library Entrance", 1500, 828, 330, 58);
        Image doorBackground = door.gameObject.AddComponent<Image>();
        doorBackground.color = InkFaint;
        Button doorButton = door.gameObject.AddComponent<Button>();
        doorButton.onClick.AddListener(TryEnterLibrary);
        doorLabel = Label(door, "Entrance Label", "THE FORGOTTEN LIBRARY  >", 17,
            Gold, TextAlignmentOptions.Center, 7, 5, 316, 48);

        // Lock/achievement notice is tiny, only displayed after user action.
        RectTransform hallNotice = RectAt(castleRoot, "Library Entrance Notice", 1210, 735, 620, 66);
        Image hallNoticeBg = hallNotice.gameObject.AddComponent<Image>();
        hallNoticeBg.color = Ink;
        hallNoticeBg.raycastTarget = false;
        hallToastGroup = hallNotice.gameObject.AddComponent<CanvasGroup>();
        hallToastGroup.blocksRaycasts = false;
        hallToastGroup.interactable = false;
        hallToastGroup.alpha = 0f;
        hallToastText = Label(hallNotice, "Hall Entrance Notice Text", "", 17,
            Gold, TextAlignmentOptions.Center, 10, 4, 600, 58);

        libraryRoot = Rect("ForgottenLibrary_Root", castleRoot);
        Stretch(libraryRoot);
        libraryRoot.SetAsLastSibling();
        Image backdrop = FullImage(libraryRoot, "Forgotten Library Environment", Color.white);
        backdrop.sprite = libraryBackground;
        backdrop.color = libraryBackground == null ? new Color(0.17f, 0.075f, 0.095f, 1f) : Color.white;
        backdrop.raycastTarget = true;
        Button backdropClick = backdrop.gameObject.AddComponent<Button>();
        backdropClick.transition = Selectable.Transition.None;
        backdropClick.onClick.AddListener(HideResearch);
        FullImage(libraryRoot, "Dusk Filter", new Color(0.015f, 0.004f, 0.015f, 0.12f)).raycastTarget = false;

        RectTransform header = RectAt(libraryRoot, "Library Minimal Header", 28, 17, 1864, 64);
        FullImage(header, "Header Translucency", InkFaint).raycastTarget = false;
        Label(header, "Chapter", "MOURNEVEIL  /  THE FORGOTTEN LIBRARY", 25,
            Gold, TextAlignmentOptions.Left, 22, 6, 920, 50);
        headerResource = Label(header, "Resources", "", 21, Gold,
            TextAlignmentOptions.Right, 1150, 6, 677, 48);

        ButtonAt(libraryRoot, "Back to Ashen Hall", "<  BACK TO THE HALL", 42, 105, 235, 55,
            CloseLibrary, Ink);
        archiveProgress = Label(libraryRoot, "Research Progress", "", 18, Gold,
            TextAlignmentOptions.Right, 1380, 105, 455, 56);

        // Hit zone around the real grimoire/desk in the background. The art remains visible.
        RectTransform desk = RectAt(libraryRoot, "Interactive Grimoire — Hover or Click", 655, 435, 620, 315);
        Image deskHitbox = desk.gameObject.AddComponent<Image>();
        deskHitbox.color = new Color(1f, 1f, 1f, 0.005f);
        deskHitbox.raycastTarget = true;
        Button deskButton = desk.gameObject.AddComponent<Button>();
        deskButton.transition = Selectable.Transition.None;
        deskButton.onClick.AddListener(() => ShowResearch(true));
        MourneveilCastleHoverZone deskHover = desk.gameObject.AddComponent<MourneveilCastleHoverZone>();
        deskHover.enter = () => { pointerOverTable = true; closeAt = -1f; if (!drawerPinned) ShowResearch(false); };
        deskHover.exit = () => { pointerOverTable = false; ScheduleClose(); };

        RectTransform hint = RectAt(libraryRoot, "Small Grimoire Hint", 733, 766, 450, 49);
        FullImage(hint, "Hint Translucency", InkFaint).raycastTarget = false;
        hintText = Label(hint, "Hint Caption", "HOVER THE GRIMOIRE  /  CLICK TO PIN", 17,
            Gold, TextAlignmentOptions.Center, 5, 4, 440, 42);
        hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
        hintGroup.alpha = 0.82f;
        hintGroup.blocksRaycasts = false;
        hintGroup.interactable = false;

        drawer = RectAt(libraryRoot, "Library Research Drawer", 1965, 173, 471, 748);
        Image drawerBackground = drawer.gameObject.AddComponent<Image>();
        drawerBackground.color = Ink;
        drawerBackground.raycastTarget = true;
        drawerGroup = drawer.gameObject.AddComponent<CanvasGroup>();
        drawerGroup.alpha = 0f;
        drawerGroup.blocksRaycasts = false;
        drawerGroup.interactable = false;
        MourneveilCastleHoverZone panelHover = drawer.gameObject.AddComponent<MourneveilCastleHoverZone>();
        panelHover.enter = () => { pointerOverDrawer = true; closeAt = -1f; };
        panelHover.exit = () => { pointerOverDrawer = false; ScheduleClose(); };
        RectTransform accent = RectAt(drawer, "Top Accent", 0, 0, 471, 3);
        FullImage(accent, "Accent Color", Burgundy).raycastTarget = false;
        Label(drawer, "Title", "FORBIDDEN RESEARCH", 27, Gold,
            TextAlignmentOptions.Left, 22, 17, 395, 43);
        Label(drawer, "Subtitle", "Knowledge that never dies. One purchase per discovery.", 16,
            Muted, TextAlignmentOptions.Left, 22, 68, 427, 47);
        ButtonAt(drawer, "Close Research", "X", 421, 19, 38, 40, HideResearch, Burgundy);

        for (int i = 0; i < ResearchBits.Length; i++)
            BuildResearchEntry(i, 130 + i * 198);

        RectTransform toast = RectAt(libraryRoot, "Discovery Notice", 655, 884, 610, 56);
        FullImage(toast, "Notice Shade", Ink).raycastTarget = false;
        messageGroup = toast.gameObject.AddComponent<CanvasGroup>();
        messageGroup.alpha = 0f;
        messageGroup.blocksRaycasts = false;
        messageGroup.interactable = false;
        messageText = Label(toast, "Notice Text", "", 18, Gold,
            TextAlignmentOptions.Center, 7, 7, 596, 44);

        libraryRoot.gameObject.SetActive(false);
    }

    private void BuildResearchEntry(int index, int y)
    {
        RectTransform section = RectAt(drawer, ResearchTitles[index] + " Research Card", 18, y, 435, 190);
        FullImage(section, "Card Shade", new Color(0.19f, 0.085f, 0.12f, 0.65f)).raycastTarget = false;
        Label(section, "Study Name", ResearchTitles[index], 21, Gold,
            TextAlignmentOptions.Left, 13, 9, 404, 36);
        Label(section, "Research Effect", Effects[index], 16, Muted,
            TextAlignmentOptions.Left, 13, 46, 404, 53);
        stateLabels[index] = Label(section, "Research Cost / State", "", 16, Gold,
            TextAlignmentOptions.Left, 13, 100, 400, 30);
        int capturedIndex = index;
        purchaseButtons[index] = ButtonAt(section, "Research Buy " + index,
            "RESEARCH", 12, 139, 410, 43, () => Purchase(capturedIndex), Burgundy);
        purchaseLabels[index] = purchaseButtons[index].GetComponentInChildren<TMP_Text>();
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform RectAt(Transform parent, string name, float x, float y,
        float width, float height)
    {
        RectTransform rt = Rect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(.5f, .5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Image FullImage(RectTransform parent, string name, Color color)
    {
        RectTransform rt = Rect(name, parent);
        Stretch(rt);
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Label(Transform parent, string name, string content, int fontSize,
        Color color, TextAlignmentOptions alignment, float x, float y, float width, float height)
    {
        RectTransform rt = RectAt(parent, name, x, y, width, height);
        TextMeshProUGUI txt = rt.gameObject.AddComponent<TextMeshProUGUI>();
        txt.text = content;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = alignment;
        txt.raycastTarget = false;
        txt.textWrappingMode = TextWrappingModes.Normal;
        txt.overflowMode = TextOverflowModes.Ellipsis;
        return txt;
    }

    private static Button ButtonAt(Transform parent, string name, string caption, float x, float y,
        float width, float height, UnityEngine.Events.UnityAction action, Color color)
    {
        RectTransform rt = RectAt(parent, name, x, y, width, height);
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        Button button = rt.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        Label(rt, "Button Caption", caption, 17, Gold,
            TextAlignmentOptions.Center, 4, 0, width - 8, height);
        return button;
    }
}
