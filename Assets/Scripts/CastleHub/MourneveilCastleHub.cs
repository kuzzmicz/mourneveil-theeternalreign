using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Mourneveil v1.0a — Castle Hub (safe, same-scene implementation).
// Add this component to _ParkSystems, alongside the EXISTING MourneveilParkGame.
// This UI uses ParkGame.State and ParkGame methods: there is no second economy,
// no second save file and no scene-loading/duplication of offline progress.
// Park is left exactly as it is. Opening Castle temporarily suspends park actors.
[DisallowMultipleComponent]
public sealed class MourneveilCastleHub : MonoBehaviour
{
    private enum Tab { None, Overview, Upgrades, Servants, Expeditions, Chronicle }

    [Header("References")]
    [SerializeField] private MourneveilParkGame parkGame;
    [SerializeField] private Sprite castleBackground;

    [Header("Opening and navigation")]
    [SerializeField] private bool startInCastle = true;
    [SerializeField] private KeyCode toggleCastleKey = KeyCode.H;
    [SerializeField, Range(0.2f, 1.0f)] private float hoverCloseDelay = 0.40f;
    [SerializeField, Range(300f, 2400f)] private float drawerSlideSpeed = 1400f;
    [SerializeField] private bool keepProducingWhenUnfocused = true;

    // Style — translucent overlays, leaving the castle art as the focal point.
    private static readonly Color Ink = new Color(0.079f, 0.035f, 0.056f, 0.91f);
    private static readonly Color InkDeep = new Color(0.05f, 0.024f, 0.041f, 0.92f);
    private static readonly Color Burgundy = new Color(0.38f, 0.105f, 0.19f, 0.97f);
    private static readonly Color Gold = new Color(0.92f, 0.82f, 0.76f);
    private static readonly Color Muted = new Color(0.77f, 0.63f, 0.64f);
    private static readonly Color Line = new Color(0.60f, 0.30f, 0.37f, 0.90f);

    private Canvas castleCanvas;
    private RectTransform root;
    private RectTransform drawer;
    private CanvasGroup drawerGroup;
    private RectTransform panelContents;
    private TMP_Text drawerHeading, drawerDescription;
    private TMP_Text bloodText, productionText, humanityText, soulText, suspicionText;
    private TMP_Text objectiveTitle, objectiveProgress, objectiveNext, infoNote;
    private CanvasGroup objectiveGroup;
    private TMP_Text objectiveCompletionNotice;
    private int lastObjectiveStage = -1;
    private float objectiveNoticeUntil;
    private TMP_Text upgradeInfo, upgradeButtonText;
    private TMP_Text servantsInfo, servantSelectionInfo;
    private TMP_Text expeditionInfo, chronicleInfo;
    private TMP_Text overviewInfo;
    private Button upgradeButton;
    private readonly Dictionary<Tab, GameObject> pages = new Dictionary<Tab, GameObject>();
    private readonly Dictionary<Tab, Button> tabs = new Dictionary<Tab, Button>();
    private readonly List<Behaviour> parkedBehaviours = new List<Behaviour>();
    private int selectedFollowerIndex;
    private Tab activeTab = Tab.None;
    private bool pinned, pointerOverTab, pointerOverDrawer, inCastle, initialized;
    private float closeAt = -1f;
    private float nextRefresh;
    private TMP_Text parkShortcutWarning;
    private float warningUntil;
    private Button parkShortcut;

    private void Awake()
    {
        if (parkGame == null) parkGame = GetComponent<MourneveilParkGame>();
        if (parkGame == null)
        {
            Debug.LogError("[Mourneveil Castle] Add the component to the same _ParkSystems as MourneveilParkGame.");
            enabled = false;
            return;
        }
        if (keepProducingWhenUnfocused) Application.runInBackground = true;
    }

    private IEnumerator Start()
    {
        // ParkGame.Start creates Park_Runtime_UI first; we must not steal its Canvas.
        yield return null;
        CreateCastleCanvas();
        initialized = true;
        if (startInCastle && parkGame.State.currentHuman == null) EnterCastle(false);
        else LeaveCastle(false);
    }

    private void Update()
    {
        if (!initialized || parkGame == null) return;
        if (CastleKeyJustPressed())
        {
            if (inCastle) LeaveCastle(true);
            else EnterCastle(true);
        }
        else if (inCastle && EscapeJustPressed())
        {
            // When the Library is open, Esc returns to the hall, NOT the park.
            MourneveilForgottenLibrary library = GetComponent<MourneveilForgottenLibrary>();
            if (library != null && library.IsOpen) library.CloseLibrary();
            else if (activeTab != Tab.None) CloseDrawer();
            else LeaveCastle(true);
        }

        if (!inCastle)
        {
            if (parkShortcutWarning != null && Time.unscaledTime > warningUntil)
                parkShortcutWarning.text = "";
            return;
        }

        if (!pinned && activeTab != Tab.None && closeAt >= 0f &&
            Time.unscaledTime >= closeAt && !pointerOverTab && !pointerOverDrawer)
            CloseDrawer();

        if (drawer != null && drawerGroup != null)
        {
            float destinationX = activeTab == Tab.None ? -590f : 167f;
            Vector2 destination = new Vector2(destinationX, -186f);
            drawer.anchoredPosition = Vector2.MoveTowards(drawer.anchoredPosition,
                destination, drawerSlideSpeed * Time.unscaledDeltaTime);
            drawerGroup.alpha = Mathf.MoveTowards(drawerGroup.alpha,
                activeTab == Tab.None ? 0f : 1f, 7f * Time.unscaledDeltaTime);
            bool visible = activeTab != Tab.None;
            drawerGroup.blocksRaycasts = visible;
            drawerGroup.interactable = visible;
        }
        // Keep the objective out of the way whenever a drawer is visible.
        if (objectiveGroup != null)
        {
            float targetAlpha = activeTab == Tab.None ? 1f : 0f;
            objectiveGroup.alpha = Mathf.MoveTowards(objectiveGroup.alpha, targetAlpha,
                8f * Time.unscaledDeltaTime);
        }
        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 0.15f;
            RefreshData();
        }
    }

    // Supports the Input System and the Legacy Input Manager, like ParkPlayer.
    private bool CastleKeyJustPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            Key mapped;
            if (Enum.TryParse(toggleCastleKey.ToString(), out mapped) && mapped != Key.None)
                return Keyboard.current[mapped].wasPressedThisFrame;
            return Keyboard.current.hKey.wasPressedThisFrame; // default shortcut
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(toggleCastleKey);
#else
        return false;
#endif
    }

    private static bool EscapeJustPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null) return Keyboard.current.escapeKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    private void EnterCastle(bool save)
    {
        if (!initialized) return;
        if (!CanEnterCastle())
        {
            if (parkShortcutWarning != null)
            {
                parkShortcutWarning.text = "Finish the current encounter before returning to the castle.";
                warningUntil = Time.unscaledTime + 3.5f;
            }
            return;
        }
        if (inCastle) return;
        if (save) parkGame.SaveNow();
        inCastle = true;
        SuspendParkActors();
        root.gameObject.SetActive(true);
        parkShortcut.gameObject.SetActive(false);
        CloseDrawer();
        RefreshData();
    }

    private void LeaveCastle(bool save)
    {
        if (!initialized) return;
        if (save && inCastle) parkGame.SaveNow();
        inCastle = false;
        CloseDrawer();
        root.gameObject.SetActive(false);
        parkShortcut.gameObject.SetActive(true);
        RestoreParkActors();
    }

    private bool CanEnterCastle()
    {
        if (parkGame.State.currentHuman != null) return false;
        GameObject parkUI = GameObject.Find("Park_Runtime_UI_v05");
        if (parkUI == null) return true;
        string[] modalNames = { "Confrontation Overlay", "Hunt Result Overlay", "Dynasty Management Overlay" };
        foreach (string modal in modalNames)
        {
            Transform t = parkUI.transform.Find(modal);
            if (t != null && t.gameObject.activeSelf) return false;
        }
        return true;
    }

    private void SuspendParkActors()
    {
        parkedBehaviours.Clear();
        MonoBehaviour[] scripts = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MonoBehaviour behaviour in scripts)
        {
            if (behaviour == null || !behaviour.enabled || behaviour == this) continue;
            string n = behaviour.GetType().Name;
            if (n != "MourneveilParkPlayer" && n != "MourneveilParkWalker" &&
                n != "MourneveilHunter" && n != "MourneveilParkUI" &&
                n != "MourneveilParkAnimationBootstrap") continue;
            parkedBehaviours.Add(behaviour);
            behaviour.enabled = false;
        }
        // MourneveilParkGame intentionally stays ENABLED: it owns idle income,
        // save data, decay of Suspicion and all progression calculations.
    }

    private void RestoreParkActors()
    {
        foreach (Behaviour b in parkedBehaviours)
            if (b != null) b.enabled = true;
        parkedBehaviours.Clear();
    }

    private void OnDestroy()
    {
        RestoreParkActors();
    }

    private void CreateCastleCanvas()
    {
        GameObject host = new GameObject("CastleHub_RuntimeCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        castleCanvas = host.GetComponent<Canvas>();
        castleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        castleCanvas.overrideSorting = true;
        castleCanvas.sortingOrder = 90;
        CanvasScaler scaler = host.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // The PARK button is outside the Castle overlay so it remains accessible.
        parkShortcut = ButtonAt(host.transform, "Open Castle Shortcut", "CASTLE  [H]",
            1620, 135, 250, 48, () => EnterCastle(true), Ink);
        parkShortcutWarning = Label(host.transform, "Shortcut Warning", "", 18,
            Gold, TextAlignmentOptions.Right, 1150, 190, 720, 45);
        parkShortcutWarning.raycastTarget = false;

        root = Rect("CastleHub_Root", host.transform);
        Stretch(root);
        Image background = Rect("Castle Scene Artwork", root).gameObject.AddComponent<Image>();
        Stretch(background.rectTransform);
        background.sprite = castleBackground;
        background.color = castleBackground == null ? new Color(0.14f, 0.055f, 0.09f) : Color.white;
        background.preserveAspect = false;
        background.raycastTarget = true;
        Button dismiss = background.gameObject.AddComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(CloseDrawer);
        Image tint = FullImage(root, "Very Light Mood Tint", new Color(0.02f, 0.005f, 0.012f, 0.21f));
        tint.raycastTarget = false;

        // Nearly the whole Castle artwork remains visible; HUD is 52 px high.
        Image header = FullImage(RectAt(root, "Minimal Header", 32, 18, 1856, 62),
            "Header Shade", new Color(0.06f, 0.022f, 0.04f, 0.71f));
        header.raycastTarget = false;
        Label(root, "Game Name", "MOURNEVEIL   /   THE ETERNAL REIGN", 24,
            Gold, TextAlignmentOptions.Left, 51, 26, 760, 36);
        Label(root, "Castle Name", "THE ASHEN HALL", 18,
            Muted, TextAlignmentOptions.Right, 1400, 28, 430, 34);
        CreateHud();
        CreateObjective();

        // Small, persistent navigation tabs. Hover previews; click pins.
        AddDockTab(Tab.Overview, "OVERVIEW", 264);
        AddDockTab(Tab.Upgrades, "UPGRADES", 324);
        AddDockTab(Tab.Servants, "SERVANTS", 384);
        AddDockTab(Tab.Expeditions, "EXPEDITIONS", 444);
        AddDockTab(Tab.Chronicle, "CHRONICLE", 504);

        // Drawer uses a separate area, not a reserved layout column.
        drawer = RectAt(root, "Castle Content Drawer", -590, 186, 555, 758);
        Image drawerBg = drawer.gameObject.AddComponent<Image>();
        drawerBg.color = InkDeep;
        drawerBg.raycastTarget = true;
        drawerGroup = drawer.gameObject.AddComponent<CanvasGroup>();
        drawerGroup.alpha = 0f;
        drawerGroup.interactable = false;
        drawerGroup.blocksRaycasts = false;
        MourneveilCastleHoverZone drawerHover = drawer.gameObject.AddComponent<MourneveilCastleHoverZone>();
        drawerHover.enter = () => { pointerOverDrawer = true; closeAt = -1f; };
        drawerHover.exit = () => { pointerOverDrawer = false; ScheduleClose(); };
        Image leftLine = FullImage(RectAt(drawer, "Top Accent", 0, 0, 555, 3), "Accent", Line);
        leftLine.raycastTarget = false;
        drawerHeading = Label(drawer, "Drawer Title", "UPGRADES", 29, Gold,
            TextAlignmentOptions.Left, 26, 22, 490, 44);
        drawerDescription = Label(drawer, "Drawer Subtitle", "Build the fallen dynasty.", 17,
            Muted, TextAlignmentOptions.Left, 26, 72, 490, 62);
        ButtonAt(drawer, "Close Drawer", "X", 480, 23, 49, 39, CloseDrawer, Burgundy);
        panelContents = RectAt(drawer, "Drawer Content", 24, 144, 506, 582);
        BuildPages();

        // Accessible, small calls to action. Neither is a wide permanent panel.
        ButtonAt(root, "Gather Blood", "GATHER  +1 BLOOD", 750, 980, 236, 58, GatherBlood, Burgundy);
        ButtonAt(root, "Enter Park", "ENTER THE PARK", 1490, 968, 338, 72,
            () => LeaveCastle(true), Burgundy);
        infoNote = Label(root, "Castle Tiny Hint", "Hover a tab to peek  |  Click to pin  |  H: Park  |  Esc: close",
            17, Gold, TextAlignmentOptions.Center, 995, 994, 450, 45);

        if (EventSystem.current == null)
        {
            Debug.LogWarning("[Mourneveil Castle] No EventSystem found. Castle buttons require one (GameObject > UI > Event System).");
        }
        root.gameObject.SetActive(false);
    }

    private void CreateHud()
    {
        RectTransform hud = RectAt(root, "Compact Stats", 520, 98, 1340, 60);
        FullImage(hud, "Soft HUD Backdrop", new Color(0.065f, 0.026f, 0.045f, 0.78f)).raycastTarget = false;
        bloodText = HudStat(hud, "BLOOD", 0);
        productionText = HudStat(hud, "BLOOD/SEC", 1);
        humanityText = HudStat(hud, "HUMANITY", 2);
        soulText = HudStat(hud, "SOUL", 3);
        suspicionText = HudStat(hud, "SUSPICION", 4);
    }

    private TMP_Text HudStat(Transform parent, string title, int index)
    {
        int x = index * 268;
        Label(parent, title + " Label", title, 13, Muted,
            TextAlignmentOptions.Center, x + 3, 5, 261, 20);
        return Label(parent, title + " Value", "--", 22, Gold,
            TextAlignmentOptions.Center, x + 3, 24, 261, 31);
    }

    private void CreateObjective()
    {
        RectTransform objective = RectAt(root, "Small Next Objective", 171, 829, 560, 128);
        objectiveGroup = objective.gameObject.AddComponent<CanvasGroup>();
        objectiveGroup.interactable = false;
        objectiveGroup.blocksRaycasts = false;
        FullImage(objective, "Objective Backdrop", new Color(0.06f, 0.025f, 0.045f, 0.78f))
            .raycastTarget = false;
        objectiveTitle = Label(objective, "Objective Title", "NEXT OBJECTIVE", 18,
            Gold, TextAlignmentOptions.Left, 20, 10, 530, 28);
        objectiveProgress = Label(objective, "Objective Detail", "Gather 30 Blood", 22,
            Gold, TextAlignmentOptions.Left, 20, 41, 525, 36);
        objectiveNext = Label(objective, "Objective Hint", "Your dynasty needs resources.", 15,
            Muted, TextAlignmentOptions.Left, 20, 87, 525, 28);
        // Short, unobtrusive success feedback when a milestone is completed.
        objectiveCompletionNotice = Label(root, "Objective Completion Notice", "", 22,
            Gold, TextAlignmentOptions.Center, 705, 177, 530, 45);
    }

    private void AddDockTab(Tab tab, string title, int y)
    {
        Button b = ButtonAt(root, "Dock " + title, title, 18, y, 143, 51, () => ClickTab(tab), Ink);
        tabs.Add(tab, b);
        MourneveilCastleHoverZone hover = b.gameObject.AddComponent<MourneveilCastleHoverZone>();
        hover.enter = () => { pointerOverTab = true; HoverTab(tab); };
        hover.exit = () => { pointerOverTab = false; ScheduleClose(); };
    }

    private void HoverTab(Tab tab)
    {
        closeAt = -1f;
        if (pinned) return; // A pinned panel is deliberately stable during hover.
        OpenTab(tab);
    }

    private void ClickTab(Tab tab)
    {
        if (pinned && activeTab == tab) { CloseDrawer(); return; }
        pinned = true;
        OpenTab(tab);
    }

    private void OpenTab(Tab tab)
    {
        activeTab = tab;
        closeAt = -1f;
        foreach (KeyValuePair<Tab, GameObject> pair in pages)
            pair.Value.SetActive(pair.Key == tab);
        foreach (KeyValuePair<Tab, Button> pair in tabs)
        {
            Image img = pair.Value.GetComponent<Image>();
            if (img != null) img.color = pair.Key == tab ? Burgundy : Ink;
        }
        drawerHeading.text = tab.ToString().ToUpperInvariant();
        string description = "";
        switch (tab)
        {
            case Tab.Overview: description = "Your bloodline, your next step."; break;
            case Tab.Upgrades: description = "Invest in quiet, enduring power."; break;
            case Tab.Servants: description = "Each vampire shapes your dynasty."; break;
            case Tab.Expeditions: description = "Active hunting is always your choice."; break;
            case Tab.Chronicle: description = "What the night remembers."; break;
        }
        drawerDescription.text = description;
        if (drawer != null && drawerGroup != null)
        {
            drawerGroup.interactable = true;
            drawerGroup.blocksRaycasts = true;
        }
        RefreshData();
    }

    private void ScheduleClose()
    {
        if (!pinned) closeAt = Time.unscaledTime + hoverCloseDelay;
    }

    private void CloseDrawer()
    {
        activeTab = Tab.None;
        pinned = false;
        closeAt = -1f;
        foreach (Button b in tabs.Values)
        {
            if (b != null) b.GetComponent<Image>().color = Ink;
        }
    }

    private GameObject Page(Tab tab)
    {
        RectTransform rect = RectAt(panelContents, tab + " Page", 0, 0, 506, 582);
        pages.Add(tab, rect.gameObject);
        rect.gameObject.SetActive(false);
        return rect.gameObject;
    }

    private void BuildPages()
    {
        GameObject overview = Page(Tab.Overview);
        overviewInfo = Label(overview.transform, "Overview Instructions",
            "Your bloodline has fallen, but it can rise again.\n\n" +
            "1. Gather Blood.\n2. Buy your first Supply Route.\n3. Hunt in the park when you feel ready.\n" +
            "4. Turn a human and assign your first servant.\n\n" +
            "The castle produces Blood while you explore the park.",
            21, Gold, TextAlignmentOptions.TopLeft, 5, 12, 482, 398);
        ButtonAt(overview.transform, "Overview to Upgrades", "VIEW UPGRADES", 3, 436,
            476, 58, () => { pinned = true; OpenTab(Tab.Upgrades); }, Burgundy);

        GameObject upgrades = Page(Tab.Upgrades);
        upgradeInfo = Label(upgrades.transform, "Upgrade Stats", "", 22,
            Gold, TextAlignmentOptions.TopLeft, 5, 20, 488, 285);
        upgradeButton = ButtonAt(upgrades.transform, "Buy Supply", "BUY SUPPLY ROUTE", 3,
            329, 476, 72, () => { parkGame.BuySupplyNetwork(); RefreshData(); }, Burgundy);
        upgradeButtonText = upgradeButton.GetComponentInChildren<TextMeshProUGUI>();
        Label(upgrades.transform, "Upgrade Hint",
            "More Supply Routes = permanent Blood/sec.\n" +
            "Scholars amplify production; Hunters add their own.", 18,
            Muted, TextAlignmentOptions.TopLeft, 8, 427, 465, 113);

        GameObject servants = Page(Tab.Servants);
        servantsInfo = Label(servants.transform, "Dynasty Count", "", 20,
            Gold, TextAlignmentOptions.TopLeft, 5, 10, 474, 144);
        servantSelectionInfo = Label(servants.transform, "Selected Vampire", "", 19,
            Muted, TextAlignmentOptions.TopLeft, 5, 145, 474, 167);
        ButtonAt(servants.transform, "Previous Servant", "PREV", 4, 319, 217, 51,
            () => { selectedFollowerIndex--; RefreshData(); }, Ink);
        ButtonAt(servants.transform, "Next Servant", "NEXT", 247, 319, 217, 51,
            () => { selectedFollowerIndex++; RefreshData(); }, Ink);
        ParkFollowerRole[] roles = { ParkFollowerRole.Hunter, ParkFollowerRole.Spy,
            ParkFollowerRole.Scholar, ParkFollowerRole.Guardian };
        for (int i = 0; i < roles.Length; i++)
        {
            ParkFollowerRole role = roles[i];
            float x = i % 2 == 0 ? 4 : 247;
            float y = i / 2 == 0 ? 388 : 462;
            ButtonAt(servants.transform, "Assign " + role, role.ToString().ToUpperInvariant(),
                x, y, 217, 58, () => AssignSelectedRole(role), Burgundy);
        }
        Label(servants.transform, "Servants Hint", "Training costs 15 Blood. First recruit: turn a human in the park.",
            15, Muted, TextAlignmentOptions.Left, 5, 533, 480, 48);

        GameObject expeditions = Page(Tab.Expeditions);
        expeditionInfo = Label(expeditions.transform, "Expedition Guide",
            "The park is an optional active expedition.\n\n" +
            "WASD — move  |  SHIFT — run\nCTRL — sneak  |  E — hunt\n\n" +
            "Your Supply Routes continue producing Blood during hunts.\n" +
            "Hunters may appear when Suspicion rises.",
            21, Gold, TextAlignmentOptions.TopLeft, 5, 18, 483, 352);
        ButtonAt(expeditions.transform, "Go Hunting", "ENTER THE PARK", 4, 420, 470, 71,
            () => LeaveCastle(true), Burgundy);

        GameObject chronicle = Page(Tab.Chronicle);
        chronicleInfo = Label(chronicle.transform, "The Chronicle", "", 20,
            Gold, TextAlignmentOptions.TopLeft, 4, 22, 476, 340);
        ButtonAt(chronicle.transform, "Save Castle Progress", "SAVE PROGRESS", 4, 419, 474, 68,
            () => { parkGame.SaveNow(); RefreshData(); }, Burgundy);
        Label(chronicle.transform, "Save Reminder", "Your existing Park save file is reused. No new save slot is created.",
            17, Muted, TextAlignmentOptions.Left, 5, 507, 474, 72);
    }

    private void AssignSelectedRole(ParkFollowerRole role)
    {
        List<ParkFollower> followers = parkGame.State.followers;
        if (followers == null || followers.Count == 0) return;
        selectedFollowerIndex = Mathf.Clamp(selectedFollowerIndex, 0, followers.Count - 1);
        ParkFollower candidate = followers[selectedFollowerIndex];
        if (candidate == null) return;
        parkGame.AssignFollowerRole(candidate.id, role);
        RefreshData();
    }

    private void GatherBlood()
    {
        // Intentionally simple v1.0a clicker action: uses the EXISTING ParkProgress.
        // ParkGame already autosaves every 15 seconds and owns the persistence.
        parkGame.State.blood += 1f;
        RefreshData();
    }

    private void RefreshData()
    {
        if (parkGame == null || parkGame.State == null || root == null) return;
        ParkProgress s = parkGame.State;
        bloodText.text = s.blood.ToString("0.0");
        productionText.text = "+" + parkGame.BloodPerSecond.ToString("0.00") + "/s";
        humanityText.text = s.humanity.ToString("0") + "%";
        soulText.text = s.soulIntegrity.ToString("0") + "%";
        suspicionText.text = s.suspicion.ToString("0") + "%";

        int followers = s.followers == null ? 0 : s.followers.Count;

        // Each goal comes directly from the saved game state. No separate
        // quest flags, no duplicated progress, and no endless fallback task.
        int objectiveStage;
        if (s.supplyNetworkLevel < 1)
        {
            objectiveStage = 0;
            objectiveTitle.text = "NEXT OBJECTIVE  /  FIRST SUPPLY ROUTE";
            if (s.blood < 30f)
            {
                objectiveProgress.text = "Gather 30 Blood  (" + s.blood.ToString("0") + "/30)";
                objectiveNext.text = "Gather Blood or hunt to afford your first route.";
            }
            else
            {
                objectiveProgress.text = "Blood gathered! Buy your first route.";
                objectiveNext.text = "Open UPGRADES and purchase a Supply Route.";
            }
        }
        else if (followers < 1)
        {
            objectiveStage = 1;
            objectiveTitle.text = "NEXT OBJECTIVE  /  FIRST VAMPIRE";
            objectiveProgress.text = "Recruit your first vampire  (0/1)";
            objectiveNext.text = "In the park, try ATTEMPT TURNING.";
        }
        else if (s.supplyNetworkLevel < 2)
        {
            objectiveStage = 2;
            objectiveTitle.text = "NEXT OBJECTIVE  /  STRENGTHEN SUPPLY";
            objectiveProgress.text = "Upgrade Supply Routes  (1/2)";
            objectiveNext.text = "Open UPGRADES and invest in passive Blood/sec.";
        }
        else if (followers < 2)
        {
            objectiveStage = 3;
            objectiveTitle.text = "NEXT OBJECTIVE  /  GROW THE DYNASTY";
            objectiveProgress.text = "Recruit a second vampire  (1/2)";
            objectiveNext.text = "Your first ally is recruited. Find a second one.";
        }
        else if (s.supplyNetworkLevel < 3)
        {
            objectiveStage = 4;
            objectiveTitle.text = "NEXT OBJECTIVE  /  THE THIRD ROUTE";
            objectiveProgress.text = "Upgrade Supply Routes  (" + s.supplyNetworkLevel + "/3)";
            objectiveNext.text = "Build a stronger passive income for your house.";
        }
        else
        {
            // Castle v1.1: the House is restored; begin Library research goals.
            int studied = 0;
            if ((s.libraryResearchMask & 1) != 0) studied++;
            if ((s.libraryResearchMask & 2) != 0) studied++;
            if ((s.libraryResearchMask & 4) != 0) studied++;
            objectiveStage = 5 + studied;
            if (studied < 3)
            {
                objectiveTitle.text = "NEXT CHAPTER  /  THE FORGOTTEN LIBRARY";
                objectiveProgress.text = "Complete forbidden research  (" + studied + "/3)";
                objectiveNext.text = "Click LIBRARY in the hall; hover the grimoire to study.";
            }
            else
            {
                objectiveTitle.text = "CHAPTER COMPLETE  /  THE ARCHIVE AWAKENS";
                objectiveProgress.text = "All 3 library discoveries researched!";
                objectiveNext.text = "Your dynasty now bears the wisdom of the old house.";
            }
        }

        // A milestone can also be finished in the park. RefreshData runs when
        // returning to the castle, so the success message still appears.
        if (lastObjectiveStage >= 0 && objectiveStage > lastObjectiveStage)
        {
            objectiveNoticeUntil = Time.unscaledTime + 3.2f;
        }
        lastObjectiveStage = objectiveStage;
        if (objectiveCompletionNotice != null)
        {
            objectiveCompletionNotice.text = Time.unscaledTime < objectiveNoticeUntil
                ? "OBJECTIVE COMPLETE"
                : "";
        }
        upgradeInfo.text = "SUPPLY NETWORK\n\n" +
            "Current level: " + s.supplyNetworkLevel + "\n" +
            "Blood/sec: +" + parkGame.BloodPerSecond.ToString("0.00") + "\n" +
            "Next upgrade: " + parkGame.NetworkUpgradeCost + " Blood\n\n" +
            "A route produces +0.35 base Blood/sec\n" +
            "(before Scholar bonuses).";
        upgradeButton.interactable = s.blood >= parkGame.NetworkUpgradeCost;
        upgradeButtonText.text = "BUY SUPPLY ROUTE  -  " + parkGame.NetworkUpgradeCost + " BLOOD";
        servantsInfo.text = "VAMPIRES: " + followers + "\n" +
            "Hunter: " + parkGame.CountRole(ParkFollowerRole.Hunter) + "   " +
            "Spy: " + parkGame.CountRole(ParkFollowerRole.Spy) + "\n" +
            "Scholar: " + parkGame.CountRole(ParkFollowerRole.Scholar) + "   " +
            "Guardian: " + parkGame.CountRole(ParkFollowerRole.Guardian);
        if (followers == 0)
        {
            servantSelectionInfo.text = "No servants yet.\n\nHunt in the park and use ATTEMPT TURNING.";
        }
        else
        {
            selectedFollowerIndex = (selectedFollowerIndex % followers + followers) % followers;
            ParkFollower ally = s.followers[selectedFollowerIndex];
            servantSelectionInfo.text = ally == null ? "Unknown servant" :
                "SELECTED  " + (selectedFollowerIndex + 1) + "/" + followers + "\n" +
                ally.title + "\nRole: " + ally.role +
                "  |  Base income: +" + ally.bloodPerSecond.ToString("0.00") + "/s\n" +
                "Strength: " + ally.strength + "   Intelligence: " + ally.intelligence;
        }
        chronicleInfo.text = "LAST MEMORY\n\n" + s.lastMessage + "\n\n" +
            "Total encounters: " + s.encountersResolved + "\n" +
            "Supply routes: " + s.supplyNetworkLevel + "\n" +
            "Dynasty size: " + followers;
    }

    // ---------- Small runtime UI helpers. No prefabs needed. ----------
    private static RectTransform Rect(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return (RectTransform)obj.transform;
    }

    private static RectTransform RectAt(Transform parent, string name, float x, float y,
        float width, float height)
    {
        RectTransform r = Rect(name, parent);
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
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    private static Image FullImage(RectTransform parent, string name, Color color)
    {
        RectTransform r = Rect(name, parent);
        Stretch(r);
        Image image = r.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Label(Transform parent, string name, string content, int fontSize,
        Color color, TextAlignmentOptions alignment, float x, float y, float w, float h)
    {
        RectTransform r = RectAt(parent, name, x, y, w, h);
        TextMeshProUGUI txt = r.gameObject.AddComponent<TextMeshProUGUI>();
        txt.text = content;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = alignment;
        txt.raycastTarget = false;
        txt.overflowMode = TextOverflowModes.Ellipsis;
        return txt;
    }

    private static Button ButtonAt(Transform parent, string name, string title, float x, float y,
        float w, float h, UnityEngine.Events.UnityAction action, Color color)
    {
        RectTransform r = RectAt(parent, name, x, y, w, h);
        Image image = r.gameObject.AddComponent<Image>();
        image.color = color;
        Button button = r.gameObject.AddComponent<Button>();
        ColorBlock block = button.colors;
        block.normalColor = Color.white;
        block.highlightedColor = new Color(1.25f, 1.12f, 1.14f, 1f);
        block.pressedColor = new Color(0.7f, 0.58f, 0.60f, 1f);
        block.disabledColor = new Color(0.46f, 0.40f, 0.42f, 0.55f);
        block.fadeDuration = 0.10f;
        button.colors = block;
        button.onClick.AddListener(action);
        TMP_Text t = Label(r, "Text", title, 17, Gold, TextAlignmentOptions.Center, 0, 0, w, h);
        t.raycastTarget = false;
        return button;
    }
}

// Pointer relay for preview-on-hover, without EventTrigger arrays or inspector wiring.
public sealed class MourneveilCastleHoverZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [NonSerialized] public Action enter;
    [NonSerialized] public Action exit;
    public void OnPointerEnter(PointerEventData e) { if (enter != null) enter(); }
    public void OnPointerExit(PointerEventData e) { if (exit != null) exit(); }
}
