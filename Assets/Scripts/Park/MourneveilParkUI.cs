using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Mourneveil Park v0.5.1: Dynasty Management + readable, acknowledged hunt outcomes.
// Generates its UI at runtime; no prefabs or editor scripts are required.
[DisallowMultipleComponent]
public class MourneveilParkUI : MonoBehaviour
{
    [Header("v0.2 - assign optional passerby PNG sprites (Student, Guard, Wanderer)")]
    [SerializeField] private Sprite studentSprite;
    [SerializeField] private Sprite guardSprite;
    [SerializeField] private Sprite wandererSprite;
    [Header("v0.3 - Lucien controllable avatar (optional sprite)")]
    [SerializeField] private Sprite lucienSprite;
    [SerializeField, Range(55f, 160f)] private float interactionRange = 100f;
    [Header("v0.4 - Detection tuning")]
    [SerializeField, Range(0.3f, 2f)] private float detectionDifficulty = 1f;
    [SerializeField, Range(0.05f, 1f)] private float sneakDetectionMultiplier = 0.22f;

    [Header("v0.5.1 - Hunt results")]
    [Tooltip("If enabled, every completed hunt shows a result popup. Click CONTINUE HUNTING to dismiss it.")]
    [SerializeField] private bool showHuntOutcomePopup = true;

    private readonly Color ivory = new Color(0.94f, 0.85f, 0.80f);
    private readonly Color muted = new Color(0.73f, 0.60f, 0.61f);
    private readonly Color panel = new Color(0.085f, 0.034f, 0.055f, 0.96f);
    private readonly Color border = new Color(0.56f, 0.27f, 0.32f);

    private TMP_Text bloodText, humanityText, soulText, suspicionText;
    private TMP_Text encounterTitleText, encounterStatsText, encounterHintText;
    private TMP_Text networkText, allyText, resultText, warningText;
    private TMP_Text networkButtonLabel, layLowButtonLabel;
    private Button leaveButton, smallButton, eraseButton, drainButton, turnButton;
    private Button networkButton, layLowButton, manageDynastyButton;

    // v0.5 - Modal Dynasty roster, generated without custom prefabs.
    private RectTransform rosterOverlay, rosterListContent;
    private TMP_Text rosterCounter, rosterName, rosterStats, rosterRoleInfo, rosterEmptyHint;
    private Image rosterPortrait;
    private readonly Button[] roleButtons = new Button[4];
    private int selectedFollowerId = -1;
    private int rosterCachedCount = -1;
    private bool rosterDirty = true;
    private readonly MourneveilParkWalker[] walkers = new MourneveilParkWalker[3];
    private MourneveilParkPlayer player;
    private MourneveilParkGame gameRef;
    private TMP_Text nearbyHintText;
    private int markedWalker = -1;
    private int lastEngagedWalker = -1;
    private int lastResolvedCount;

    // UI-only result state: never overwrites the saved progress or lastMessage.
    private RectTransform outcomeOverlay;
    private TMP_Text outcomeHeadingText, outcomeBodyText;
    private string pinnedOutcomeMessage;
    private bool IsOutcomeVisible
    {
        get { return outcomeOverlay != null && outcomeOverlay.gameObject.activeSelf; }
    }

    private bool built;

    public void Build(MourneveilParkGame game)
    {
        if (built) return;
        gameRef = game;
        lastResolvedCount = game.State.encountersResolved;
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject go = new GameObject("ParkCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null)
            Debug.LogWarning("[Mourneveil Park] Add UI > Event System to the scene if buttons do not respond.");

        RectTransform root = NewRect("Park_Runtime_UI_v05", canvas.transform);
        Stretch(root);

        // Fullscreen backdrop + shade; the clear version is visible in the park window.
        Image fullBg = AddImage("Fullscreen Park Backdrop", root, Color.white);
        Stretch((RectTransform)fullBg.transform);
        fullBg.sprite = game.Background;
        fullBg.type = Image.Type.Simple;
        fullBg.color = game.Background == null ? new Color(0.11f, 0.07f, 0.11f) : Color.white;
        Image globalShade = AddImage("Fullscreen Dark Tint", root, new Color(0.025f, 0.008f, 0.018f, 0.83f));
        Stretch((RectTransform)globalShade.transform);

        TMP_Text title = AddText("Title", root, "MOURNEVEIL   /   CHAPTER II : THE FIRST HUNT",
            37, ivory, TextAlignmentOptions.Left);
        Place(title.rectTransform, 55, 18, 1780, 55);
        TMP_Text subtitle = AddText("Subtitle", root,
            "The city sleeps. Its heartbeats do not.", 23, muted, TextAlignmentOptions.Left);
        Place(subtitle.rectTransform, 56, 76, 1750, 35);

        bloodText = StatBox(root, "Blood", 55);
        humanityText = StatBox(root, "Humanity", 516);
        soulText = StatBox(root, "Soul Integrity", 977);
        suspicionText = StatBox(root, "Suspicion", 1438);

        // Playable UI-space park. The player must approach an NPC and press E.
        RectTransform world = Frame("Moonlit Park", root, 55, 239, 1165, 486);
        world.gameObject.AddComponent<RectMask2D>();
        Image parkImage = AddImage("Park Artwork", world, Color.white);
        if (game.Background != null)
        {
            float aspect = game.Background.rect.width / game.Background.rect.height;
            float artworkHeight = Mathf.Max(486f, 1165f / aspect);
            Place(parkImage.rectTransform, 0f, (486f - artworkHeight) * 0.5f, 1165f, artworkHeight);
            parkImage.sprite = game.Background;
            parkImage.preserveAspect = false;
            parkImage.color = new Color(0.88f, 0.83f, 0.85f, 1f);
        }
        else
        {
            Stretch(parkImage.rectTransform);
            parkImage.color = new Color(0.16f, 0.10f, 0.13f, 1f);
        }
        Image horizon = AddImage("Horizon Shade", world, new Color(0.05f, 0.014f, 0.03f, 0.12f));
        Stretch(horizon.rectTransform);
        TMP_Text worldCaption = AddText("World Caption", world,
            "MOURNEVEIL PARK   /   AFTER MIDNIGHT", 20, ivory, TextAlignmentOptions.Left);
        Place(worldCaption.rectTransform, 18, 15, 470, 30);
        TMP_Text controls = AddText("Controls", world,
            "WASD MOVE  |  SHIFT RUN  |  CTRL SNEAK  |  E HUNT", 17, ivory,
            TextAlignmentOptions.Right);
        Place(controls.rectTransform, 500, 18, 640, 33);

        player = CreatePlayer(world);
        Sprite[] sprites = { studentSprite, guardSprite, wandererSprite };
        float[] positionsX = { -355f, -20f, 328f };
        float[] speeds = { 0.64f, 0.48f, 0.54f };
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            ParkHuman candidate = game.State.currentHuman != null &&
                game.State.currentHuman.appearanceIndex == index
                ? game.State.currentHuman : game.CreateHumanPreview(index);
            walkers[i] = CreateWalker(world, sprites[i], index, candidate,
                positionsX[i], 90f + i * 8f, speeds[i], () => MarkWalker(index), player);
        }
        player.transform.SetAsLastSibling();
        nearbyHintText = AddText("Park Action Prompt", world,
            "Approach a passerby. Press E when close enough.",
            21, ivory, TextAlignmentOptions.Center);
        Place(nearbyHintText.rectTransform, 155, 440, 855, 33);

        // A single encounter panel is below the world. Nothing blocks walking in the park.
        RectTransform actions = Frame("Hunt Actions", root, 55, 742, 1165, 281);
        encounterTitleText = AddText("Selected Person", actions,
            "MOVE CLOSE TO A PASSERBY AND PRESS E", 27, ivory, TextAlignmentOptions.Left);
        Place(encounterTitleText.rectTransform, 23, 10, 1110, 36);
        encounterStatsText = AddText("Person Stats", actions,
            "WASD or Arrows: move  |  Shift: run  |  Ctrl: sneak  |  E: begin encounter",
            20, muted, TextAlignmentOptions.TopLeft);
        Place(encounterStatsText.rectTransform, 23, 53, 1110, 58);

        const float bw = 360f;
        smallButton = MakeButton(actions, "TAKE A LITTLE BLOOD", 23, 116, bw, 55, game.TakeSmallAmount);
        eraseButton = MakeButton(actions, "FEED & ERASE MEMORY", 402, 116, bw, 55, game.FeedAndEraseMemory);
        drainButton = MakeButton(actions, "DRAIN COMPLETELY", 781, 116, bw, 55, game.DrainHuman);
        turnButton = MakeButton(actions, "ATTEMPT TURNING", 23, 184, bw, 55, game.AttemptTurning);
        leaveButton = MakeButton(actions, "LET THEM GO", 402, 184, bw, 55, game.LetGo);
        encounterHintText = AddText("Decision Hint", actions,
            "First catch a human. The choices unlock only after pressing E nearby.",
            18, muted, TextAlignmentOptions.Left);
        Place(encounterHintText.rectTransform, 23, 248, 1110, 30);

        RectTransform dynasty = Frame("Dynasty", root, 1240, 239, 625, 784);
        TMP_Text dynastyTitle = AddText("Dynasty Title", dynasty, "THE DYNASTY",
            34, ivory, TextAlignmentOptions.Left);
        Place(dynastyTitle.rectTransform, 25, 23, 570, 49);
        TMP_Text dynastyIntro = AddText("Dynasty Intro", dynasty,
            "Build a discreet network while the hunters sleep.", 22, muted,
            TextAlignmentOptions.TopLeft);
        Place(dynastyIntro.rectTransform, 25, 88, 580, 66);
        networkText = AddText("Production", dynasty, "", 27, ivory,
            TextAlignmentOptions.TopLeft);
        Place(networkText.rectTransform, 25, 166, 580, 78);
        networkButton = MakeButton(dynasty, "UPGRADE SUPPLY NETWORK", 23, 267, 578, 68, game.BuySupplyNetwork);
        networkButtonLabel = networkButton.GetComponentInChildren<TextMeshProUGUI>();
        allyText = AddText("Allies", dynasty, "", 23, ivory, TextAlignmentOptions.TopLeft);
        Place(allyText.rectTransform, 25, 350, 570, 70);
        manageDynastyButton = MakeButton(dynasty, "MANAGE DYNASTY", 25, 421, 578, 51, OpenRoster);
        warningText = AddText("Hunter Warning", dynasty, "", 20, muted,
            TextAlignmentOptions.TopLeft);
        Place(warningText.rectTransform, 25, 486, 574, 56);
        layLowButton = MakeButton(dynasty, "LAY LOW", 25, 550, 277, 61, game.LayLow);
        layLowButtonLabel = layLowButton.GetComponentInChildren<TextMeshProUGUI>();
        MakeButton(dynasty, "SAVE PROGRESS", 317, 550, 284, 61, game.SaveNow);
        TMP_Text logTitle = AddText("Log Header", dynasty, "NIGHT LOG", 24,
            ivory, TextAlignmentOptions.Left);
        Place(logTitle.rectTransform, 25, 645, 570, 38);
        resultText = AddText("Latest Outcome", dynasty, "", 21,
            muted, TextAlignmentOptions.TopLeft);
        Place(resultText.rectTransform, 25, 685, 570, 84);

        BuildDynastyRoster(root);
        BuildOutcomePopup(root); // Last sibling: always above the rest of the park UI.
        built = true;
    }

    public void Refresh(MourneveilParkGame game)
    {
        if (!built) return;
        ParkProgress state = game.State;
        bloodText.text = "BLOOD\n" + state.blood.ToString("0.0") + "  (" + game.BloodPerSecond.ToString("0.00") + "/sec)";
        humanityText.text = "HUMANITY\n" + state.humanity.ToString("0") + "%";
        soulText.text = "SOUL INTEGRITY\n" + state.soulIntegrity.ToString("0") + "%";
        suspicionText.text = "SUSPICION\n" + state.suspicion.ToString("0") + "%";

        ParkHuman person = state.currentHuman;
        bool engaged = person != null;
        if (engaged)
        {
            encounterTitleText.text = person.title.ToUpperInvariant();
            encounterStatsText.text =
                "Willpower: " + person.willpower + "     Fear: " + person.fear +
                "     Strength: " + person.strength + "     Blood: ~" + person.bloodYield + "\n" +
                "Intelligence: " + person.intelligence + "     Stealth: " + person.stealth +
                "     Turning chance: " + game.TurningChance.ToString("0") + "%" +
                (state.currentEncounterAmbush ? "     [STEALTH AMBUSH!]" : "");
        }
        else
        {
            encounterTitleText.text = game.HuntersAlerted ? "HUNTERS ARE WATCHING" : "NO TARGET SELECTED";
            encounterStatsText.text = game.HuntersAlerted
                ? "Your presence is exposed. Lay low or wait for Suspicion to decrease."
                : "Walk around with WASD. Approach a person and press E to start the hunt.";
        }
        if (engaged)
        {
            lastEngagedWalker = Mathf.Clamp(person.appearanceIndex, 0, 2);
            pinnedOutcomeMessage = null; // New encounter: old result no longer needs to stay pinned.
        }
        else if (state.encountersResolved != lastResolvedCount)
        {
            // CompleteEncounter() updates lastMessage and calls Refresh() immediately.
            // Capture the outcome *before* an NPC detection can overwrite lastMessage.
            pinnedOutcomeMessage = state.lastMessage;
            if (showHuntOutcomePopup)
                ShowOutcomePopup(pinnedOutcomeMessage);
            if (lastEngagedWalker >= 0 && lastEngagedWalker < walkers.Length && walkers[lastEngagedWalker] != null)
            {
                walkers[lastEngagedWalker].BeginRespawn();
                walkers[lastEngagedWalker].AssignHuman(game.CreateHumanPreview(lastEngagedWalker));
            }
            lastEngagedWalker = -1;
            markedWalker = -1;
        }
        lastResolvedCount = state.encountersResolved;
        bool rosterOpen = rosterOverlay != null && rosterOverlay.gameObject.activeSelf;
        bool outcomeOpen = IsOutcomeVisible;
        player.SetEncounterActive(engaged || rosterOpen || outcomeOpen);
        for (int i = 0; i < walkers.Length; i++)
        {
            if (walkers[i] != null)
            {
                walkers[i].SetState(engaged && person.appearanceIndex == i,
                    engaged || rosterOpen || outcomeOpen || game.HuntersAlerted);
                walkers[i].SetMarked(!engaged && markedWalker == i);
            }
        }
        UpdateNearbyHint(game, engaged);
        leaveButton.interactable = engaged && !game.HuntersAlerted;
        smallButton.interactable = engaged && !game.HuntersAlerted;
        eraseButton.interactable = engaged && !game.HuntersAlerted;
        drainButton.interactable = engaged && !game.HuntersAlerted;
        turnButton.interactable = engaged && !game.HuntersAlerted && state.soulIntegrity >= game.TurnCost;
        networkButton.interactable = state.blood >= game.NetworkUpgradeCost;
        layLowButton.interactable = state.suspicion > 0 && !engaged && game.LayLowCooldown <= 0;
        layLowButtonLabel.text = game.LayLowCooldown > 0
            ? "LAY LOW (" + Mathf.CeilToInt(game.LayLowCooldown) + "s)"
            : "LAY LOW (-25 SUSPICION)";

        networkText.text = "Supply routes: Lv. " + state.supplyNetworkLevel +
            "\nPassive Blood: +" + game.BloodPerSecond.ToString("0.00") + "/sec";
        networkButtonLabel.text = "UPGRADE SUPPLY  -  " + game.NetworkUpgradeCost + " BLOOD";
        int allies = state.followers == null ? 0 : state.followers.Count;
        allyText.text = "Vampires: " + allies + "  |  H " + game.CountRole(ParkFollowerRole.Hunter) +
            " / S " + game.CountRole(ParkFollowerRole.Spy) +
            " / R " + game.CountRole(ParkFollowerRole.Scholar) +
            " / G " + game.CountRole(ParkFollowerRole.Guardian) +
            "\nScholar boost: +" + (game.ScholarBonus * 100f).ToString("0") + "% Blood/sec";
        manageDynastyButton.interactable = allies > 0 && !engaged;
        warningText.text = game.HuntersAlerted
            ? "HUNTER ALERT! Hunting blocked until Suspicion falls."
            : (state.humanity < 35
                ? "Low Humanity: every hunt increases extra Suspicion."
                : "Stay discreet. A high Suspicion attracts hunters.");
        encounterHintText.text = engaged
            ? "Take +" + (person.bloodYield + (state.currentEncounterAmbush ?
                  Mathf.CeilToInt(person.bloodYield * 0.25f) : 0)) +
              " Blood | Erase +" + (Mathf.Max(3, person.bloodYield - 5) +
                  (state.currentEncounterAmbush ? 3 : 0)) +
              " | Drain +" + (person.bloodYield * 3) + " | Turn costs " +
              game.TurnCost.ToString("0") + " Soul." +
              (state.currentEncounterAmbush ? "  UNDETECTED BONUS!" : "")
            : "Approach quietly for a stealth reward. Intelligent NPCs detect you sooner.";
        // Keep the last resolved hunt visible in NIGHT LOG even if NPCs later
        // report spotting Lucien and replace state.lastMessage.
        resultText.text = !engaged && !string.IsNullOrEmpty(pinnedOutcomeMessage)
            ? pinnedOutcomeMessage : state.lastMessage;
        if (rosterOpen) RefreshRoster(game);
    }

    // --------- v0.5.1: readable encounter outcomes ---------
    private void BuildOutcomePopup(RectTransform root)
    {
        outcomeOverlay = NewRect("Hunt Result Overlay", root);
        Stretch(outcomeOverlay);

        Image shade = AddImage("Hunt Result Backdrop", outcomeOverlay,
            new Color(0.012f, 0.004f, 0.016f, 0.86f));
        Stretch(shade.rectTransform);
        shade.raycastTarget = true; // Prevent interaction with controls behind the result.

        RectTransform surface = Frame("Hunt Result Frame", outcomeOverlay,
            425f, 302f, 1070f, 470f);
        TMP_Text kicker = AddText("Result Kicker", surface, "MOURNEVEIL   /   THE FIRST HUNT",
            19, muted, TextAlignmentOptions.Left);
        Place(kicker.rectTransform, 48f, 30f, 940f, 35f);

        outcomeHeadingText = AddText("Hunt Result Heading", surface, "THE HUNT IS OVER",
            42, ivory, TextAlignmentOptions.Left);
        Place(outcomeHeadingText.rectTransform, 48f, 90f, 975f, 65f);
        outcomeHeadingText.fontStyle = FontStyles.Bold;

        outcomeBodyText = AddText("Hunt Result Details", surface, "",
            27, ivory, TextAlignmentOptions.TopLeft);
        Place(outcomeBodyText.rectTransform, 48f, 184f, 970f, 130f);
        outcomeBodyText.textWrappingMode = TextWrappingModes.Normal;
        outcomeBodyText.overflowMode = TextOverflowModes.Overflow;

        MakeButton(surface, "CONTINUE HUNTING", 340f, 350f, 390f, 80f, CloseOutcomePopup);
        outcomeOverlay.gameObject.SetActive(false);
    }

    private void ShowOutcomePopup(string message)
    {
        if (outcomeOverlay == null) return;
        string text = string.IsNullOrWhiteSpace(message)
            ? "The encounter has ended." : message;
        bool success = text.StartsWith("Turning succeeded!", StringComparison.OrdinalIgnoreCase);
        bool failure = text.StartsWith("Turning failed.", StringComparison.OrdinalIgnoreCase);

        outcomeHeadingText.text = success ? "A NEW VAMPIRE AWAKENS"
            : failure ? "THE TURNING HAS FAILED" : "THE HUNT IS OVER";
        outcomeHeadingText.color = success
            ? new Color(0.91f, 0.79f, 0.67f)
            : failure ? new Color(1f, 0.54f, 0.59f) : ivory;
        outcomeBodyText.text = text;
        outcomeOverlay.gameObject.SetActive(true);
    }

    private void CloseOutcomePopup()
    {
        if (outcomeOverlay != null) outcomeOverlay.gameObject.SetActive(false);
        if (gameRef != null) Refresh(gameRef);
    }

    // --------- v0.5: Dynasty management screen ---------
    private void BuildDynastyRoster(RectTransform root)
    {
        rosterOverlay = NewRect("Dynasty Management Overlay", root);
        Stretch(rosterOverlay);
        Image shade = AddImage("Modal Backdrop", rosterOverlay, new Color(0.015f, 0.004f, 0.016f, 0.97f));
        Stretch(shade.rectTransform);
        shade.raycastTarget = true; // Block clicks on the hunting UI behind the modal.

        RectTransform surface = Frame("Dynasty Roster Frame", rosterOverlay, 265, 98, 1390, 866);
        TMP_Text heading = AddText("Dynasty Header", surface,
            "THE VAMPIRE DYNASTY", 35, ivory, TextAlignmentOptions.Left);
        Place(heading.rectTransform, 34, 19, 740, 50);
        rosterCounter = AddText("Roster Count", surface, "", 21, muted, TextAlignmentOptions.Left);
        Place(rosterCounter.rectTransform, 37, 69, 810, 33);
        MakeButton(surface, "CLOSE   X", 1134, 22, 222, 55, CloseRoster);

        RectTransform listPanel = Frame("Vampire Roster", surface, 30, 117, 610, 708);
        TMP_Text leftLabel = AddText("Roster Label", listPanel, "YOUR VAMPIRES", 23, ivory,
            TextAlignmentOptions.Left);
        Place(leftLabel.rectTransform, 15, 12, 570, 35);

        RectTransform scrollHost = NewRect("Roster ScrollRect", listPanel);
        Place(scrollHost, 12, 58, 586, 632);
        ScrollRect scroller = scrollHost.gameObject.AddComponent<ScrollRect>();
        scroller.horizontal = false;
        scroller.vertical = true;
        scroller.movementType = ScrollRect.MovementType.Clamped;
        scroller.scrollSensitivity = 38f;

        RectTransform viewport = NewRect("Viewport", scrollHost);
        Stretch(viewport);
        Image viewportImage = AddImage("Viewport Click Target", viewport, new Color(0.09f, 0.04f, 0.08f, 0.02f));
        Stretch(viewportImage.rectTransform);
        viewportImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        rosterListContent = NewRect("Roster Items", viewport);
        rosterListContent.anchorMin = new Vector2(0f, 1f);
        rosterListContent.anchorMax = new Vector2(1f, 1f);
        rosterListContent.pivot = new Vector2(0.5f, 1f);
        rosterListContent.anchoredPosition = Vector2.zero;
        rosterListContent.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = rosterListContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 10, 10);
        layout.spacing = 9f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        ContentSizeFitter fitter = rosterListContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroller.viewport = viewport;
        scroller.content = rosterListContent;

        RectTransform details = Frame("Selected Vampire", surface, 662, 117, 697, 708);
        rosterName = AddText("Selected Name", details, "", 31, ivory, TextAlignmentOptions.Left);
        Place(rosterName.rectTransform, 20, 12, 648, 49);
        Image portraitBackground = AddImage("Portrait Background", details, new Color(0.22f, 0.09f, 0.14f));
        Place(portraitBackground.rectTransform, 20, 80, 192, 290);
        rosterPortrait = AddImage("Vampire Portrait", details, Color.white);
        Place(rosterPortrait.rectTransform, 25, 85, 182, 280);
        rosterPortrait.preserveAspect = true;
        rosterStats = AddText("Vampire Attributes", details, "", 21, ivory,
            TextAlignmentOptions.TopLeft);
        Place(rosterStats.rectTransform, 236, 83, 432, 285);
        rosterEmptyHint = AddText("Empty Portrait Hint", details, "", 19, muted,
            TextAlignmentOptions.Center);
        Place(rosterEmptyHint.rectTransform, 34, 154, 163, 100);

        TMP_Text rolesHeader = AddText("Role Assignment Header", details,
            "ASSIGN A ROLE", 26, ivory, TextAlignmentOptions.Left);
        Place(rolesHeader.rectTransform, 20, 393, 625, 43);
        string[] names = { "HUNTER   /   BLOOD", "SPY   /   SECRECY",
                           "SCHOLAR   /   RESEARCH", "GUARDIAN   /   PROTECTION" };
        for (int i = 0; i < roleButtons.Length; i++)
        {
            ParkFollowerRole choice = (ParkFollowerRole)i;
            float x = i % 2 == 0 ? 20f : 356f;
            float y = i < 2 ? 448f : 526f;
            roleButtons[i] = MakeButton(details, names[i], x, y, 319f, 64f,
                () => AssignSelectedRole(choice));
        }
        rosterRoleInfo = AddText("Role Description", details, "", 19, muted,
            TextAlignmentOptions.TopLeft);
        Place(rosterRoleInfo.rectTransform, 20, 619, 648, 82);
        rosterOverlay.gameObject.SetActive(false);
    }

    private void OpenRoster()
    {
        if (gameRef == null || gameRef.State.currentHuman != null || IsOutcomeVisible ||
            gameRef.State.followers == null || gameRef.State.followers.Count == 0) return;
        if (selectedFollowerId < 0 || gameRef.GetFollower(selectedFollowerId) == null)
            selectedFollowerId = gameRef.State.followers[0].id;
        rosterDirty = true;
        rosterOverlay.gameObject.SetActive(true);
        Refresh(gameRef);
    }

    private void CloseRoster()
    {
        if (rosterOverlay != null) rosterOverlay.gameObject.SetActive(false);
        if (gameRef != null) Refresh(gameRef);
    }

    private void SelectFollower(int id)
    {
        selectedFollowerId = id;
        rosterDirty = true;
        if (gameRef != null) RefreshRoster(gameRef);
    }

    private void AssignSelectedRole(ParkFollowerRole role)
    {
        if (gameRef == null) return;
        if (gameRef.AssignFollowerRole(selectedFollowerId, role))
        {
            rosterDirty = true;
            RefreshRoster(gameRef);
        }
    }

    private void RefreshRoster(MourneveilParkGame game)
    {
        List<ParkFollower> followers = game.State.followers;
        int count = followers == null ? 0 : followers.Count;
        if (rosterCounter != null) rosterCounter.text = count + " VAMPIRE" + (count == 1 ? "" : "S") +
            "  /  Train a new role for " + game.RoleCost + " Blood.";
        if (count == 0)
        {
            rosterName.text = "NO VAMPIRES YET";
            rosterStats.text = "Turn a human in the park to start your dynasty.";
            rosterRoleInfo.text = "";
            rosterPortrait.enabled = false;
            rosterEmptyHint.text = "?";
            foreach (Button roleButton in roleButtons) roleButton.interactable = false;
            return;
        }
        if (game.GetFollower(selectedFollowerId) == null)
        {
            selectedFollowerId = followers[0].id;
            rosterDirty = true;
        }
        if (rosterDirty || count != rosterCachedCount)
        {
            RebuildRosterRows(followers);
            rosterCachedCount = count;
            rosterDirty = false;
        }
        ParkFollower chosen = game.GetFollower(selectedFollowerId);
        if (chosen == null) return;
        rosterName.text = "#" + chosen.id + "   " + chosen.title.ToUpperInvariant();
        rosterStats.text =
            "CURRENT ROLE: " + chosen.role.ToString().ToUpperInvariant() + "\n\n" +
            "Strength       " + chosen.strength + "\n" +
            "Intelligence   " + chosen.intelligence + "\n" +
            "Stealth          " + chosen.stealth + "\n" +
            "Willpower      " + chosen.willpower + "\n" +
            "Fear              " + chosen.fear + "\n\n" +
            "Hunter base: +" + chosen.bloodPerSecond.ToString("0.00") + " Blood/sec";
        Sprite portrait = SpriteForAppearance(chosen.appearanceIndex);
        rosterPortrait.sprite = portrait;
        rosterPortrait.enabled = portrait != null;
        rosterEmptyHint.text = portrait != null ? "" : "VAMPIRE\n#" + chosen.id;
        for (int i = 0; i < roleButtons.Length; i++)
        {
            ParkFollowerRole role = (ParkFollowerRole)i;
            roleButtons[i].interactable = game.CanAssignRole(selectedFollowerId, role);
            Image img = roleButtons[i].GetComponent<Image>();
            if (img != null)
                img.color = chosen.role == role
                    ? new Color(0.48f, 0.18f, 0.21f, 1f)
                    : new Color(0.32f, 0.065f, 0.13f, 0.97f);
        }
        rosterRoleInfo.text = chosen.role == ParkFollowerRole.Hunter
            ? "HUNTER: Produces +" + chosen.bloodPerSecond.ToString("0.00") +
              " Blood/sec, boosted by Scholars."
            : chosen.role == ParkFollowerRole.Spy
                ? "SPY: Speeds up passive Suspicion decay. Extra decay: " +
                  game.SpySuspicionDecay.ToString("0.00") + "/sec (all Spies)."
                : chosen.role == ParkFollowerRole.Scholar
                    ? "SCHOLAR: Boosts all Blood/sec (+" +
                      (game.ScholarBonus * 100f).ToString("0") +
                      "% total) and improves Turning Chance."
                    : "GUARDIAN: Reduces Suspicion gains by " +
                      (game.GuardianProtection * 100f).ToString("0") + "% (all Guardians).";
        rosterRoleInfo.text += "\nRole changes cost " + game.RoleCost + " Blood. The current role is highlighted.";
    }

    private Sprite SpriteForAppearance(int index)
    {
        if (index == 0) return studentSprite;
        if (index == 1) return guardSprite;
        return wandererSprite;
    }

    private void RebuildRosterRows(List<ParkFollower> followers)
    {
        for (int i = rosterListContent.childCount - 1; i >= 0; i--)
            Destroy(rosterListContent.GetChild(i).gameObject);
        foreach (ParkFollower ally in followers)
        {
            if (ally == null) continue;
            int id = ally.id; // Stable identity, not a list index.
            RectTransform row = NewRect("Vampire #" + id, rosterListContent);
            LayoutElement size = row.gameObject.AddComponent<LayoutElement>();
            size.preferredHeight = 78f;
            Image background = row.gameObject.AddComponent<Image>();
            background.color = id == selectedFollowerId
                ? new Color(0.41f, 0.13f, 0.20f, 1f)
                : new Color(0.16f, 0.075f, 0.12f, 1f);
            Button button = row.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => SelectFollower(id));
            TMP_Text label = AddText("Name and Role", row,
                "#" + id + "  " + ally.title + "\n" + ally.role.ToString().ToUpperInvariant(),
                20, ivory, TextAlignmentOptions.Left);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(16f, 5f);
            label.rectTransform.offsetMax = new Vector2(-12f, -5f);
        }
    }

    private MourneveilParkWalker CreateWalker(RectTransform parent, Sprite sprite, int index,
        ParkHuman human, float centerX, float bottomY, float speed,
        Action onClick, MourneveilParkPlayer controller)
    {
        RectTransform rect = NewRect("Passerby_" + (index + 1), parent);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(centerX, bottomY);
        rect.sizeDelta = new Vector2(98, 175);
        Image graphic = rect.gameObject.AddComponent<Image>();
        graphic.sprite = sprite;
        graphic.preserveAspect = sprite != null;
        graphic.color = sprite == null ? new Color(0.18f, 0.09f, 0.15f, 0.94f) : Color.white;
        Button button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick());
        if (sprite == null)
        {
            TMP_Text fallback = AddText("PLACEHOLDER", rect, "?", 50, ivory, TextAlignmentOptions.Center);
            Stretch(fallback.rectTransform);
        }
        TMP_Text hover = AddText("Hover Label", rect, "STALK", 17, ivory, TextAlignmentOptions.Center);
        RectTransform hoverRect = hover.rectTransform;
        hoverRect.anchorMin = new Vector2(0.5f, 0f);
        hoverRect.anchorMax = new Vector2(0.5f, 0f);
        hoverRect.pivot = new Vector2(0.5f, 0f);
        hoverRect.anchoredPosition = new Vector2(0, 175);
        hoverRect.sizeDelta = new Vector2(122, 22);
        // NPC's per-person awareness meter (appears only when Lucien is noticed).
        Image meterBg = AddImage("Awareness Track", rect, new Color(0.10f, 0.035f, 0.065f, 0.88f));
        meterBg.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        meterBg.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        meterBg.rectTransform.pivot = new Vector2(0.5f, 0f);
        meterBg.rectTransform.anchoredPosition = new Vector2(0f, 206f);
        meterBg.rectTransform.sizeDelta = new Vector2(112f, 10f);
        Image meter = AddImage("Awareness Fill", meterBg.rectTransform, new Color(0.95f, 0.65f, 0.35f));
        meter.rectTransform.anchorMin = new Vector2(0f, 0f);
        meter.rectTransform.anchorMax = new Vector2(0f, 1f);
        meter.rectTransform.offsetMin = Vector2.zero;
        meter.rectTransform.offsetMax = Vector2.zero;
        TMP_Text meterLabel = AddText("Awareness Label", rect, "AWARENESS", 13, ivory, TextAlignmentOptions.Center);
        meterLabel.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        meterLabel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        meterLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        meterLabel.rectTransform.anchoredPosition = new Vector2(0f, 218f);
        meterLabel.rectTransform.sizeDelta = new Vector2(132f, 22f);
        MourneveilParkWalker motion = rect.gameObject.AddComponent<MourneveilParkWalker>();
        motion.Initialize(graphic, button, hover, meterBg, meter, meterLabel,
            human, new Vector2(centerX, bottomY), speed, index, controller,
            OnPasserbyDetected, detectionDifficulty, sneakDetectionMultiplier);
        return motion;
    }

    private void Update()
    {
        if (!built || gameRef == null || player == null) return;
        if ((rosterOverlay != null && rosterOverlay.gameObject.activeSelf) || IsOutcomeVisible) return;
        if (!MourneveilParkPlayer.InteractJustPressed()) return;
        if (gameRef.State.currentHuman != null || gameRef.HuntersAlerted) return;

        int nearest = FindNearestAvailableWalker(out float distance);
        if (nearest >= 0 && distance <= interactionRange)
        {
            MourneveilParkWalker target = walkers[nearest];
            if (player.IsSprinting) target.ForceSpot(); // Running catches faster, but is conspicuous.
            if (gameRef.HuntersAlerted) return;
            bool ambush = player.IsSneaking && target.CanAmbush;
            markedWalker = -1;
            gameRef.FindHuman(target.Human, ambush); // Exactly this NPC and their pre-rolled stats.
        }
    }

    private void OnPasserbyDetected(MourneveilParkWalker witness)
    {
        if (witness != null && gameRef != null)
            gameRef.ReportDetection(witness.Human);
    }

    // Clicking a passerby just marks them: no ranged hunting shortcut.
    private void MarkWalker(int index)
    {
        if (gameRef == null || gameRef.State.currentHuman != null || gameRef.HuntersAlerted || IsOutcomeVisible) return;
        markedWalker = index;
    }

    private int FindNearestAvailableWalker(out float distance)
    {
        int bestIndex = -1;
        distance = float.PositiveInfinity;
        if (player == null) return -1;
        for (int i = 0; i < walkers.Length; i++)
        {
            MourneveilParkWalker npc = walkers[i];
            if (npc == null || !npc.IsAvailable) continue;
            float d = Vector2.Distance(player.ParkPosition, npc.ParkPosition);
            if (d < distance)
            {
                distance = d;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private void UpdateNearbyHint(MourneveilParkGame game, bool engaged)
    {
        if (nearbyHintText == null) return;
        if (engaged)
        {
            nearbyHintText.text = "THE HUNT  /  Choose what Lucien will do below.";
            return;
        }
        if (game.HuntersAlerted)
        {
            nearbyHintText.text = "HUNTERS ARE ALERT. Lay low before hunting again.";
            return;
        }
        int closest = FindNearestAvailableWalker(out float distance);
        if (closest < 0)
        {
            nearbyHintText.text = "The path is empty. Wait for another passerby.";
            return;
        }
        if (distance <= interactionRange)
        {
            MourneveilParkWalker npc = walkers[closest];
            nearbyHintText.text = player.IsSneaking && npc.CanAmbush
                ? "[ E ]  SILENT AMBUSH  /  BONUS BLOOD + TURN CHANCE"
                : npc.HasSpottedLucien
                    ? "[ E ]  CATCH THE WITNESS  /  THEY HAVE SPOTTED YOU"
                    : "[ E ]  INTERACT  /  HOLD CTRL FOR STEALTH BONUS";
            return;
        }
        if (markedWalker >= 0 && walkers[markedWalker] != null && walkers[markedWalker].IsAvailable)
        {
            nearbyHintText.text = "TARGET MARKED  /  Follow them and press E when close.";
            return;
        }
        nearbyHintText.text = player.IsSneaking
            ? "SNEAKING  /  Keep approaching quietly."
            : "MOVE CLOSE TO A PASSERBY  /  CTRL TO SNEAK";
    }

    private MourneveilParkPlayer CreatePlayer(RectTransform parent)
    {
        RectTransform root = NewRect("Lucien_Player", parent);
        root.anchorMin = new Vector2(0.5f, 0f);
        root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        root.sizeDelta = new Vector2(100f, 164f);
        root.anchoredPosition = new Vector2(0f, 65f);

        Image shadow = AddImage("Player Ground Shadow", root, new Color(0.08f, 0.0f, 0.04f, 0.56f));
        shadow.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        shadow.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        shadow.rectTransform.pivot = new Vector2(0.5f, 0f);
        shadow.rectTransform.anchoredPosition = Vector2.zero;
        shadow.rectTransform.sizeDelta = new Vector2(90f, 13f);

        Image spriteImage = AddImage("Lucien Sprite", root, Color.white);
        spriteImage.sprite = lucienSprite;
        spriteImage.preserveAspect = true;
        spriteImage.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        spriteImage.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        spriteImage.rectTransform.pivot = new Vector2(0.5f, 0f);
        spriteImage.rectTransform.anchoredPosition = Vector2.zero;
        spriteImage.rectTransform.sizeDelta = new Vector2(95f, 162f);
        if (lucienSprite == null) spriteImage.color = new Color(0.16f, 0.09f, 0.18f, 1f);

        TMP_Text name = AddText("Player Label", root, "LUCIEN", 18, ivory, TextAlignmentOptions.Center);
        name.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        name.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        name.rectTransform.pivot = new Vector2(0.5f, 0f);
        name.rectTransform.anchoredPosition = new Vector2(0f, 163f);
        name.rectTransform.sizeDelta = new Vector2(120f, 30f);
        MourneveilParkPlayer controller = root.gameObject.AddComponent<MourneveilParkPlayer>();
        controller.Initialize(spriteImage, new Vector2(0f, 65f));
        return controller;
    }

    private TMP_Text StatBox(Transform parent, string name, float x)
    {
        RectTransform frame = Frame(name + " Box", parent, x, 123, 425, 93);
        TMP_Text text = AddText(name + " Text", frame, name, 24, ivory, TextAlignmentOptions.TopLeft);
        Place(text.rectTransform, 18, 9, 400, 78);
        return text;
    }

    private RectTransform Frame(string name, Transform parent, float x, float y, float w, float h)
    {
        RectTransform frame = NewRect(name, parent);
        Place(frame, x, y, w, h);
        Image background = AddImage("Frame Background", frame, panel);
        Stretch(background.rectTransform);
        Image top = AddImage("Crimson Top Line", frame, border);
        Place(top.rectTransform, 0, 0, w, 3);
        return frame;
    }

    private Button MakeButton(Transform parent, string caption, float x, float y,
        float w, float h, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = NewRect(caption, parent);
        Place(rect, x, y, w, h);
        Image img = rect.gameObject.AddComponent<Image>();
        img.color = new Color(0.32f, 0.065f, 0.13f, 0.97f);
        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.20f, 1.10f, 1.10f);
        colors.pressedColor = new Color(0.68f, 0.52f, 0.58f);
        colors.disabledColor = new Color(0.28f, 0.27f, 0.27f, 0.55f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        button.onClick.AddListener(action);
        TMP_Text captionText = AddText("Caption", rect, caption, 21, ivory, TextAlignmentOptions.Center);
        Stretch(captionText.rectTransform);
        return button;
    }

    private TMP_Text AddText(string name, Transform parent, string value, int size,
        Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = NewRect(name, parent);
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = value;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal; // Current TMPro API.
        tmp.overflowMode = TextOverflowModes.Truncate;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Image AddImage(string name, Transform parent, Color color)
    {
        RectTransform rect = NewRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
    }
}
