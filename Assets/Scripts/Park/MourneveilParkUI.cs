using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Prototype UI is assembled at runtime; no Editor scripts or prefab references needed.
[DisallowMultipleComponent]
public class MourneveilParkUI : MonoBehaviour
{
    private readonly Color ivory = new Color(0.94f, 0.85f, 0.80f);
    private readonly Color muted = new Color(0.71f, 0.58f, 0.58f);
    private readonly Color panel = new Color(0.085f, 0.034f, 0.055f, 0.96f);
    private readonly Color border = new Color(0.41f, 0.22f, 0.26f);

    private RectTransform root;
    private TMP_Text bloodText, humanityText, soulText, suspicionText;
    private TMP_Text encounterTitleText, encounterStatsText, encounterHintText;
    private TMP_Text networkText, allyText, resultText, warningText;
    private TMP_Text networkButtonLabel, layLowButtonLabel;
    private Button findButton, leaveButton, smallButton, eraseButton, drainButton, turnButton;
    private Button networkButton, layLowButton;
    private bool built;

    public void Build(MourneveilParkGame game)
    {
        if (built) return;
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("ParkCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
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

        // If buttons do not respond, create UI > Button once in the editor to add an
        // EventSystem compatible with your project's chosen Input System.
        if (EventSystem.current == null)
            Debug.LogWarning("[Mourneveil Park] No EventSystem found. Add GameObject > UI > Event System to the Park scene if buttons do not respond.");

        root = NewRect("Park_Runtime_UI", canvas.transform);
        Stretch(root);

        Image bg = AddImage("Park Background", root, Color.black);
        Stretch((RectTransform)bg.transform);
        bg.sprite = game.Background;
        if (game.Background != null)
        {
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;  // Fill whole screen; replace with a fitted asset later.
            bg.color = new Color(0.60f, 0.60f, 0.60f, 1f);
        }
        AddImage("Background shade", root, new Color(0.045f, 0.018f, 0.032f, 0.66f));
        Stretch((RectTransform)root.Find("Background shade"));

        TMP_Text title = AddText("Title", root, "MOURNEVEIL  /  CHAPTER II: THE FIRST HUNT", 39, ivory, TextAlignmentOptions.Left);
        Place((RectTransform)title.transform, 62, 28, 1770, 65);
        TMP_Text subtitle = AddText("Subtitle", root, "The living city does not know you have returned.", 24, muted, TextAlignmentOptions.Left);
        Place((RectTransform)subtitle.transform, 65, 92, 1750, 40);

        bloodText = StatBox("Blood", 60);
        humanityText = StatBox("Humanity", 515);
        soulText = StatBox("Soul Integrity", 970);
        suspicionText = StatBox("Suspicion", 1425);

        RectTransform left = Frame("Encounter Panel", root, 60, 270, 1080, 690);
        TMP_Text head = AddText("Encounter Header", left, "A SHADOW IN THE PARK", 33, ivory, TextAlignmentOptions.Left);
        Place((RectTransform)head.transform, 30, 24, 1000, 50);
        encounterTitleText = AddText("Person", left, "No one nearby", 31, ivory, TextAlignmentOptions.Left);
        Place((RectTransform)encounterTitleText.transform, 30, 98, 1000, 49);
        encounterStatsText = AddText("Human Stats", left, "Find a stranger to begin your first hunt.", 26, muted, TextAlignmentOptions.TopLeft);
        Place((RectTransform)encounterStatsText.transform, 30, 162, 1010, 124);

        findButton = MakeButton(left, "Find person", 28, 308, 495, 69, game.FindHuman);
        leaveButton = MakeButton(left, "Let them go", 554, 308, 495, 69, game.LetGo);
        smallButton = MakeButton(left, "Take a little blood", 28, 399, 495, 75, game.TakeSmallAmount);
        eraseButton = MakeButton(left, "Feed & erase memory", 554, 399, 495, 75, game.FeedAndEraseMemory);
        drainButton = MakeButton(left, "Drain completely", 28, 490, 495, 75, game.DrainHuman);
        turnButton = MakeButton(left, "Attempt turning (18 Soul)", 554, 490, 495, 75, game.AttemptTurning);
        encounterHintText = AddText("Risk Hint", left,
            "Careful feeding is safer. Killing gains more Blood, but costs Humanity.",
            21, muted, TextAlignmentOptions.TopLeft);
        Place((RectTransform)encounterHintText.transform, 33, 591, 1008, 75);

        RectTransform right = Frame("Dynasty Panel", root, 1170, 270, 690, 690);
        TMP_Text dynastyTitle = AddText("Dynasty Header", right, "YOUR DYNASTY", 33, ivory, TextAlignmentOptions.Left);
        Place((RectTransform)dynastyTitle.transform, 27, 24, 630, 50);
        TMP_Text networkDescription = AddText("Network Explanation", right,
            "Build a discreet blood supply network. Every upgrade produces Blood automatically.",
            24, muted, TextAlignmentOptions.TopLeft);
        Place((RectTransform)networkDescription.transform, 28, 102, 628, 85);
        networkText = AddText("Supply Status", right, "", 25, ivory, TextAlignmentOptions.TopLeft);
        Place((RectTransform)networkText.transform, 28, 205, 625, 85);
        networkButton = MakeButton(right, "Buy supply upgrade", 25, 302, 635, 72, game.BuySupplyNetwork);
        networkButtonLabel = networkButton.GetComponentInChildren<TextMeshProUGUI>();
        allyText = AddText("Allies", right, "", 25, ivory, TextAlignmentOptions.TopLeft);
        Place((RectTransform)allyText.transform, 27, 400, 629, 112);
        warningText = AddText("Hunter Warning", right, "", 22, muted, TextAlignmentOptions.TopLeft);
        Place((RectTransform)warningText.transform, 28, 523, 630, 62);
        layLowButton = MakeButton(right, "Lay low", 26, 601, 310, 65, game.LayLow);
        layLowButtonLabel = layLowButton.GetComponentInChildren<TextMeshProUGUI>();
        MakeButton(right, "Save progress", 352, 601, 308, 65, game.SaveNow);

        RectTransform result = Frame("Story Log", root, 60, 977, 1800, 77);
        resultText = AddText("Latest Outcome", result, "", 24, ivory, TextAlignmentOptions.MidlineLeft);
        Place((RectTransform)resultText.transform, 22, 5, 1750, 68);
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

        ParkHuman human = state.currentHuman;
        bool engaged = human != null;
        if (engaged)
        {
            encounterTitleText.text = human.title;
            encounterStatsText.text =
                "Willpower: " + human.willpower + "     Fear: " + human.fear + "     Strength: " + human.strength + "\n" +
                "Intelligence: " + human.intelligence + "     Stealth: " + human.stealth + "     Blood: ~" + human.bloodYield + "\n" +
                "Estimated turning chance: " + game.TurningChance.ToString("0") + "%";
        }
        else
        {
            encounterTitleText.text = game.HuntersAlerted ? "The hunters are watching!" : "No one nearby";
            encounterStatsText.text = game.HuntersAlerted
                ? "Your presence is exposed. Lay low or wait for Suspicion to decrease."
                : "Find a stranger to choose how Lucien gathers Blood.";
        }

        findButton.interactable = !engaged && !game.HuntersAlerted;
        leaveButton.interactable = engaged && !game.HuntersAlerted;
        smallButton.interactable = engaged && !game.HuntersAlerted;
        eraseButton.interactable = engaged && !game.HuntersAlerted;
        drainButton.interactable = engaged && !game.HuntersAlerted;
        turnButton.interactable = engaged && !game.HuntersAlerted && state.soulIntegrity >= game.TurnCost;
        networkButton.interactable = state.blood >= game.NetworkUpgradeCost;
        layLowButton.interactable = state.suspicion > 0f && !engaged && game.LayLowCooldown <= 0f;
        layLowButtonLabel.text = game.LayLowCooldown > 0f ? "Lay low (" + Mathf.CeilToInt(game.LayLowCooldown) + "s)" : "Lay low (-25 Suspicion)";

        networkText.text = "Supply routes: Lv. " + state.supplyNetworkLevel +
            "\nPassive Blood: +" + game.BloodPerSecond.ToString("0.00") + "/sec";
        networkButtonLabel.text = "Upgrade supply network  —  " + game.NetworkUpgradeCost + " Blood";
        int count = state.followers == null ? 0 : state.followers.Count;
        allyText.text = "Vampiric allies: " + count +
            "\nSuccessfully turned allies also produce Blood automatically.";
        warningText.text = game.HuntersAlerted
            ? "HUNTER ALERT! Hunting is blocked until Suspicion falls."
            : (state.humanity < 35f
                ? "Your instincts grow violent: each hunt raises extra Suspicion."
                : "Low Humanity makes hunting harder. Soul can be restored in a later rebirth system.");
        encounterHintText.text = engaged
            ? "Small feeding: +" + human.bloodYield + " Blood, moderate risk.  Memory erase: +" + Mathf.Max(3, human.bloodYield-5) + " Blood, low risk.\n" +
              "Drain: +" + human.bloodYield * 3 + " Blood, -15 Humanity.  Turn: -18 Soul, chance " + game.TurningChance.ToString("0") + "%."
            : "There is no perfect way to survive. Every choice shapes the dynasty you rebuild.";
        resultText.text = "NIGHT LOG  •  " + state.lastMessage;
    }

    private TMP_Text StatBox(string name, float x)
    {
        RectTransform panelRect = Frame(name + " frame", root, x, 150, 430, 96);
        TMP_Text label = AddText(name + " label", panelRect, name, 25, ivory, TextAlignmentOptions.TopLeft);
        Place((RectTransform)label.transform, 17, 10, 408, 80);
        return label;
    }

    private RectTransform Frame(string name, Transform parent, float x, float y, float w, float h)
    {
        RectTransform frame = NewRect(name, parent);
        Place(frame, x, y, w, h);
        AddImage("Frame Background", frame, panel);
        Stretch((RectTransform)frame.Find("Frame Background"));
        // Simple thin silver-burgundy inset border (no external assets required).
        Image lineTop = AddImage("Top trim", frame, border);
        Place((RectTransform)lineTop.transform, 0, 0, w, 3);
        return frame;
    }

    private Button MakeButton(Transform parent, string caption, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = NewRect(caption, parent);
        Place(rect, x, y, w, h);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.28f, 0.065f, 0.11f, 0.98f);
        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock palette = button.colors;
        palette.normalColor = Color.white;
        palette.highlightedColor = new Color(1.17f, 1.07f, 1.07f);
        palette.pressedColor = new Color(0.68f, 0.58f, 0.58f);
        palette.disabledColor = new Color(0.36f, 0.36f, 0.36f, 0.58f);
        palette.colorMultiplier = 1f;
        palette.fadeDuration = 0.12f;
        button.colors = palette;
        button.transition = Selectable.Transition.ColorTint;
        button.onClick.AddListener(action);
        TMP_Text text = AddText("Label", rect, caption, 23, ivory, TextAlignmentOptions.Center);
        Stretch((RectTransform)text.transform);
        text.raycastTarget = false;
        return button;
    }

    private TMP_Text AddText(string name, Transform parent, string value, int fontSize, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = NewRect(name, parent);
        TextMeshProUGUI t = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        t.fontSize = fontSize;
        t.text = value;
        t.color = color;
        t.alignment = alignment;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Truncate;
        t.raycastTarget = false;
        return t;
    }

    private Image AddImage(string name, Transform parent, Color color)
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

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
