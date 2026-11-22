using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Mourneveil v0.4: every passerby has stable traits for the entire spawn.
// Notice -> suspicion -> either flee, retreat or confront (depending on traits).
// This is UI-space prototype AI, not physics/line-of-sight AI.
[DisallowMultipleComponent]
public sealed class MourneveilParkWalker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Image artwork;
    private Button button;
    private TMP_Text hoverLabel;
    private TMP_Text awarenessLabel;
    private Image awarenessBackground;
    private Image awarenessFill;
    private RectTransform awarenessFillRect;
    private RectTransform rect;
    private MourneveilParkPlayer player;
    private Action<MourneveilParkWalker> onDetected;
    private ParkHuman human;
    private Vector2 home;
    private Vector2 position;
    private float speed;
    private float phase;
    private float cooldown;
    private float awareness;
    private float detectionDifficulty;
    private float sneakMultiplier;
    private int index;
    private bool hovered;
    private bool selected;
    private bool marked;
    private bool locked;
    private bool initialized;
    private bool usesSprite;
    private bool spotted;
    private bool confronting;
    private bool approachCompromised;

    public Vector2 ParkPosition { get { return position; } }
    public ParkHuman Human { get { return human; } }
    public float Awareness { get { return awareness; } }
    public bool HasSpottedLucien { get { return spotted; } }
    public bool CanAmbush { get { return IsAvailable && !spotted && !approachCompromised && awareness < 45f; } }
    public bool IsAvailable { get { return initialized && cooldown <= 0f && !locked; } }

    public void Initialize(Image image, Button targetButton, TMP_Text label,
        Image meterBackground, Image meterFill, TMP_Text meterText,
        ParkHuman generatedHuman, Vector2 homePosition, float walkSpeed,
        int id, MourneveilParkPlayer controller,
        Action<MourneveilParkWalker> detectedCallback,
        float difficulty, float sneakingMultiplier)
    {
        artwork = image;
        usesSprite = image != null && image.sprite != null;
        button = targetButton;
        hoverLabel = label;
        awarenessBackground = meterBackground;
        awarenessFill = meterFill;
        awarenessFillRect = meterFill != null ? meterFill.rectTransform : null;
        awarenessLabel = meterText;
        rect = GetComponent<RectTransform>();
        home = homePosition;
        position = home;
        speed = walkSpeed;
        phase = id * 2.25f;
        index = id;
        player = controller;
        onDetected = detectedCallback;
        detectionDifficulty = Mathf.Max(0.1f, difficulty);
        sneakMultiplier = Mathf.Clamp(sneakingMultiplier, 0.05f, 1f);
        human = generatedHuman;
        rect.anchoredPosition = position;
        initialized = true;
        hoverLabel.text = "MARK";
        hoverLabel.color = new Color(0.95f, 0.86f, 0.81f, 0f);
        UpdateAwarenessVisuals();
    }

    // Sprinting straight into a target always leaves a witness, even if their
    // awareness meter hasn't filled yet. An event is reported only once.
    public void ForceSpot()
    {
        if (!IsAvailable || spotted || human == null) return;
        awareness = 100f;
        spotted = true;
        confronting = human.strength >= 72 && human.fear <= 48;
        approachCompromised = true;
        if (onDetected != null) onDetected(this);
        UpdateAwarenessVisuals();
    }

    public void AssignHuman(ParkHuman replacement)
    {
        human = replacement;
        awareness = 0f;
        spotted = false;
        confronting = false;
        approachCompromised = false;
        marked = false;
        hovered = false;
        UpdateAwarenessVisuals();
    }

    public void SetState(bool isSelected, bool isLocked)
    {
        selected = isSelected;
        locked = isLocked;
        if (button != null) button.interactable = !locked && cooldown <= 0f;
        if (locked) hovered = false;
    }

    public void SetMarked(bool isMarked)
    {
        marked = isMarked;
    }

    public void BeginRespawn()
    {
        cooldown = UnityEngine.Random.Range(4.5f, 8f);
        position = new Vector2(Mathf.Clamp(home.x + UnityEngine.Random.Range(-95f, 95f), -495f, 495f),
            Mathf.Clamp(home.y + UnityEngine.Random.Range(-25f, 25f), 80f, 135f));
        home = position;
        awareness = 0f;
        spotted = false;
        confronting = false;
        approachCompromised = false;
        marked = false;
        hovered = false;
    }

    private void Update()
    {
        if (!initialized || player == null || human == null) return;
        float dt = Time.deltaTime;
        if (cooldown > 0f) cooldown = Mathf.Max(0f, cooldown - dt);
        bool visible = cooldown <= 0f;

        if (!locked && visible)
        {
            Vector2 away = position - player.ParkPosition;
            float distance = away.magnitude;
            UpdateDetection(distance, dt);

            if (spotted && confronting)
            {
                // A brave/strong NPC stands their ground, but can still be caught.
                position += away.normalized * 9f * dt;
            }
            else if (spotted || (awareness > 68f && !player.IsSneaking))
            {
                // Fearful NPCs flee; high stealth makes them faster at escape.
                float fleeSpeed = 98f + human.fear * 0.36f + human.stealth * 0.33f;
                if (away.sqrMagnitude > 0.01f)
                    position += away.normalized * fleeSpeed * dt;
            }
            else
            {
                float time = Time.time * speed + phase;
                Vector2 destination = home + new Vector2(Mathf.Sin(time * 0.53f) * 78f,
                    Mathf.Sin(time * 0.33f + phase) * 20f);
                position = Vector2.MoveTowards(position, destination, (53f + index * 7f) * dt);
            }
            position.x = Mathf.Clamp(position.x, -520f, 520f);
            position.y = Mathf.Clamp(position.y, 75f, 135f);
        }

        rect.anchoredPosition = position;
        if (button != null) button.interactable = visible && !locked;
        if (artwork != null)
        {
            Color targetColor = !visible ? new Color(1f, 1f, 1f, 0f)
                : selected ? new Color(1f, 0.68f, 0.73f, 1f)
                : locked ? new Color(0.55f, 0.48f, 0.52f, 1f)
                : spotted ? new Color(1f, 0.54f, 0.60f, 1f)
                : (hovered || marked) ? new Color(1f, 0.85f, 0.9f, 1f)
                : Color.white;
            if (!usesSprite)
                targetColor = new Color(0.2f, 0.09f, 0.16f, targetColor.a);
            artwork.color = Color.Lerp(artwork.color, targetColor, dt * 11f);
        }
        if (hoverLabel != null)
        {
            Color c = hoverLabel.color;
            bool show = visible && !locked && (hovered || marked || spotted || awareness >= 45f);
            c.a = Mathf.MoveTowards(c.a, show ? 1f : 0f, dt * 8f);
            hoverLabel.color = c;
            hoverLabel.text = spotted ? (confronting ? "CONFRONTING" : "FLEEING")
                : awareness >= 45f ? "SUSPICIOUS" : marked ? "MARKED" : "MARK";
        }
        Vector3 desiredScale = Vector3.one * (selected ? 1.08f : hovered || marked ? 1.05f : 1f);
        rect.localScale = Vector3.Lerp(rect.localScale, desiredScale, dt * 10f);
        UpdateAwarenessVisuals();
    }

    private void UpdateDetection(float distance, float dt)
    {
        if (spotted || human == null) return;

        // Higher intelligence -> larger radius and faster recognition.
        float intelligence = Mathf.Clamp01(human.intelligence / 100f);
        float radius = Mathf.Lerp(148f, 215f, intelligence);
        if (player.IsSprinting) radius *= 1.20f;

        // Standing still at spawn shouldn't alarm everyone on the screen.
        // A stationary Lucien is noticed only if someone gets very close.
        if (distance > radius + 80f && awareness <= 2f)
            approachCompromised = false; // Let players disengage and try again.
        if (distance < radius && player.IsMoving && !player.IsSneaking && distance < 115f)
            approachCompromised = true; // No last-frame Ctrl exploit after a noisy approach.

        if (distance < radius && (player.IsMoving || distance < 82f))
        {
            if (!player.IsSneaking && distance < 110f)
                awareness = Mathf.Max(awareness,
                    Mathf.Lerp(32f, 69f, intelligence));
            float closeness = 1f - Mathf.Clamp01(distance / radius);
            float movement = player.IsSprinting ? 1.75f : 1f;
            if (player.IsSneaking) movement *= sneakMultiplier;
            float rate = (15f + 56f * closeness + human.intelligence * 0.33f) * movement * detectionDifficulty;
            awareness = Mathf.Min(100f, awareness + rate * dt);
        }
        else
            awareness = Mathf.Max(0f, awareness - 26f * dt);

        if (awareness >= 100f)
        {
            spotted = true; // Once per spawn: don't repeatedly charge global Suspicion.
            confronting = human.strength >= 72 && human.fear <= 48;
            if (onDetected != null) onDetected(this);
        }
    }

    private void UpdateAwarenessVisuals()
    {
        bool show = initialized && cooldown <= 0f && !locked && awareness > 1f;
        if (awarenessBackground != null) awarenessBackground.enabled = show;
        if (awarenessFill != null) awarenessFill.enabled = show;
        if (awarenessLabel != null)
        {
            awarenessLabel.enabled = show;
            awarenessLabel.text = spotted ? "ALERT!" : "AWARENESS  " + Mathf.CeilToInt(awareness) + "%";
        }
        if (awarenessFillRect != null)
        {
            // Anchors allow a smooth fill without changing the height.
            awarenessFillRect.anchorMax = new Vector2(Mathf.Clamp01(awareness / 100f), 1f);
            awarenessFillRect.offsetMin = Vector2.zero;
            awarenessFillRect.offsetMax = Vector2.zero;
        }
        if (awarenessFill != null)
            awarenessFill.color = spotted || awareness >= 75f
                ? new Color(0.97f, 0.24f, 0.34f)
                : awareness >= 40f ? new Color(0.96f, 0.63f, 0.34f)
                : new Color(0.78f, 0.70f, 0.62f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (IsAvailable) hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
    }
}
