using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Mourneveil Park v0.9b — stealth-friendly hunter in the v0.8 UI park.
// Attach to _ParkSystems. Do NOT attach a SpriteRenderer or create Hunter_NPC manually.
// Requires the existing MourneveilParkGame, MourneveilParkUI, MourneveilParkPlayer scripts.
[DisallowMultipleComponent]
public sealed class MourneveilHunter : MonoBehaviour
{
    public enum HunterState { Patrol, Investigate, Chase }

    [Header("References")]
    [SerializeField] private MourneveilParkGame parkGame;

    [Header("Hunter sprites — optional single-frame fallbacks")]
    [SerializeField] private Sprite idleRight;
    [SerializeField] private Sprite idleLeft;
    [SerializeField] private Sprite runRight;
    [SerializeField] private Sprite runLeft;

    [Header("Animation frames — four sprites per direction")]
    [SerializeField] private Sprite[] walkRightFrames = new Sprite[4];
    [SerializeField] private Sprite[] walkLeftFrames = new Sprite[4];
    [SerializeField] private Sprite[] runRightFrames = new Sprite[4];
    [SerializeField] private Sprite[] runLeftFrames = new Sprite[4];
    [SerializeField, Range(0.06f, 0.4f)] private float walkFrameTime = 0.18f;
    [SerializeField, Range(0.06f, 0.4f)] private float runFrameTime = 0.12f;

    [Header("Spawn / patrol — local park UI coordinates")]
    [SerializeField, Range(0f, 100f)] private float suspicionToAppear = 50f;
    [SerializeField, Range(0f, 100f)] private float suspicionToLeave = 25f;
    [SerializeField] private float leftEdge = -500f;
    [SerializeField] private float rightEdge = 500f;
    [SerializeField] private float pathY = 100f;
    [SerializeField, Min(10f)] private float patrolSpeed = 110f;
    [SerializeField, Min(10f)] private float chaseSpeed = 205f;

    [Header("Stealth — directional vision, not a 360-degree circle")]
    [SerializeField, Min(30f)] private float visionRange = 190f;
    [SerializeField, Min(20f)] private float verticalVisionRange = 115f;
    [SerializeField, Range(0.1f, 1f)] private float sneakVisionMultiplier = 0.42f;
    [SerializeField, Range(1f, 2f)] private float sprintVisionMultiplier = 1.15f;
    [SerializeField, Min(0f)] private float sprintHearingRange = 85f;
    [SerializeField, Range(0.1f, 2f)] private float walkDetectionPerSecond = 0.62f;
    [SerializeField, Range(0.05f, 1f)] private float sneakDetectionPerSecond = 0.17f;
    [SerializeField, Range(0.3f, 3f)] private float sprintDetectionPerSecond = 1.3f;
    [SerializeField, Range(0.1f, 2f)] private float awarenessDecayPerSecond = 0.75f;
    [SerializeField, Range(0.1f, 0.9f)] private float investigateThreshold = 0.30f;
    [SerializeField, Min(0.5f)] private float investigateDuration = 2.2f;
    [SerializeField, Min(0.5f)] private float loseSightDuration = 2.0f;

    [Header("Only an alerted hunter can capture Lucien")]
    [SerializeField, Min(15f)] private float catchRange = 48f;
    [SerializeField, Min(0f)] private float bloodPenalty = 30f;
    [SerializeField, Min(0f)] private float suspicionPenalty = 10f;
    [SerializeField, Min(1f)] private float afterCatchCooldown = 7f;
    [SerializeField, Min(0f)] private float spawnGraceSeconds = 2.5f;

    [Header("Visuals")]
    [SerializeField] private bool showAwarenessIcon = true;

    private RectTransform hunterRect;
    private Image hunterImage;
    private TextMeshProUGUI awarenessLabel;
    private MourneveilParkPlayer lucien;
    private MourneveilParkUI parkUI;
    private HunterState currentState = HunterState.Patrol;
    private int direction = 1;
    private float awareness;
    private float investigateTimer;
    private float lostSightTimer;
    private float catchCooldown;
    private float spawnGrace;
    private float animationTimer;
    private Vector2 lastKnownPosition;
    private bool isActive;
    private bool setupComplete;

    public HunterState CurrentState { get { return currentState; } }
    public bool IsVisible { get { return isActive; } }
    public float Awareness { get { return awareness; } }

    private IEnumerator Start()
    {
        if (parkGame == null) parkGame = GetComponent<MourneveilParkGame>();
        if (parkGame == null)
        {
            Debug.LogError("[Mourneveil Hunter] Assign Park Game on _ParkSystems.");
            yield break;
        }
        parkUI = parkGame.GetComponent<MourneveilParkUI>();

        // The entire park UI is generated at runtime. Wait for Lucien to exist.
        RectTransform parkArea = null;
        for (int attempt = 0; attempt < 180; attempt++)
        {
            GameObject parkObject = GameObject.Find("Moonlit Park");
            if (parkObject != null)
            {
                parkArea = parkObject.GetComponent<RectTransform>();
                Transform playerObject = parkObject.transform.Find("Lucien_Player");
                if (parkArea != null && playerObject != null)
                    lucien = playerObject.GetComponent<MourneveilParkPlayer>();
            }
            if (parkArea != null && lucien != null) break;
            yield return null;
        }
        if (parkArea == null || lucien == null)
        {
            Debug.LogError("[Mourneveil Hunter] Moonlit Park / Lucien_Player not found.");
            yield break;
        }

        GameObject hunterObject = new GameObject("Hunter_NPC", typeof(RectTransform), typeof(Image));
        hunterRect = hunterObject.GetComponent<RectTransform>();
        hunterRect.SetParent(parkArea, false);
        // Keep Lucien visually in front of the hunter if their sprites overlap.
        hunterRect.SetSiblingIndex(lucien.transform.GetSiblingIndex());
        hunterRect.anchorMin = new Vector2(0.5f, 0f);
        hunterRect.anchorMax = new Vector2(0.5f, 0f);
        hunterRect.pivot = new Vector2(0.5f, 0f);
        hunterRect.sizeDelta = new Vector2(98f, 175f);
        hunterRect.anchoredPosition = new Vector2(leftEdge, pathY);
        hunterImage = hunterObject.GetComponent<Image>();
        hunterImage.sprite = idleRight != null ? idleRight : idleLeft;
        hunterImage.preserveAspect = true;
        hunterImage.raycastTarget = false;

        // An unobtrusive awareness symbol over the hunter's hat.
        GameObject statusObject = new GameObject("Hunter Awareness Icon", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform statusRect = statusObject.GetComponent<RectTransform>();
        statusRect.SetParent(hunterRect, false);
        statusRect.anchorMin = new Vector2(0.5f, 1f);
        statusRect.anchorMax = new Vector2(0.5f, 1f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.sizeDelta = new Vector2(100f, 30f);
        statusRect.anchoredPosition = new Vector2(0f, 15f);
        awarenessLabel = statusObject.GetComponent<TextMeshProUGUI>();
        awarenessLabel.fontSize = 25;
        awarenessLabel.fontStyle = FontStyles.Bold;
        awarenessLabel.alignment = TextAlignmentOptions.Center;
        awarenessLabel.raycastTarget = false;
        awarenessLabel.text = "";

        hunterObject.SetActive(false);
        setupComplete = true;
        Debug.Log("[Mourneveil Hunter] v0.9b ready: stealth vision / awareness enabled.");
    }

    private void Update()
    {
        if (!setupComplete || parkGame == null || lucien == null) return;

        float dt = Time.deltaTime;
        catchCooldown = Mathf.Max(0f, catchCooldown - dt);
        spawnGrace = Mathf.Max(0f, spawnGrace - dt);

        if (!isActive)
        {
            if (parkGame.State.suspicion >= suspicionToAppear) Spawn();
            else return;
        }
        else if (suspicionToLeave < suspicionToAppear && parkGame.State.suspicion <= suspicionToLeave)
        {
            Despawn();
            return;
        }

        // Do not attack during dialog or an active human encounter.
        if (lucien.EncounterActive)
        {
            UpdateVisual(dt);
            return;
        }

        Vector2 playerPosition = lucien.ParkPosition;
        bool canSee = spawnGrace <= 0f && CanSee(playerPosition);
        bool hearsSprinting = spawnGrace <= 0f && lucien.IsSprinting &&
                              Vector2.Distance(hunterRect.anchoredPosition, playerPosition) <= sprintHearingRange;

        if (canSee)
        {
            lastKnownPosition = playerPosition;
            float rate = lucien.IsSneaking ? sneakDetectionPerSecond :
                         (lucien.IsSprinting ? sprintDetectionPerSecond : walkDetectionPerSecond);
            awareness = Mathf.Clamp01(awareness + dt * rate);
        }
        else
        {
            awareness = Mathf.Clamp01(awareness - dt * awarenessDecayPerSecond);
        }

        switch (currentState)
        {
            case HunterState.Patrol:
                MoveX(direction * patrolSpeed * dt);
                if (hunterRect.anchoredPosition.x >= rightEdge) direction = -1;
                else if (hunterRect.anchoredPosition.x <= leftEdge) direction = 1;

                if (awareness >= investigateThreshold || hearsSprinting)
                {
                    currentState = HunterState.Investigate;
                    investigateTimer = investigateDuration;
                    if (hearsSprinting && !canSee)
                        lastKnownPosition = playerPosition;
                    Face(lastKnownPosition.x);
                    Log(hearsSprinting && !canSee
                        ? "The hunter heard footsteps nearby..."
                        : "The hunter noticed suspicious movement...");
                }
                break;

            case HunterState.Investigate:
                // Stop and check the last known location, not the live player's position.
                Face(lastKnownPosition.x);
                investigateTimer -= dt;
                if (canSee && awareness >= 1f)
                {
                    currentState = HunterState.Chase;
                    lostSightTimer = loseSightDuration;
                    Log("The hunter spotted Lucien! Run or break line of sight!");
                }
                else if (investigateTimer <= 0f && !canSee)
                {
                    currentState = HunterState.Patrol;
                    awareness = Mathf.Min(awareness, 0.15f);
                    Log("The hunter resumes patrol.");
                }
                else if (investigateTimer <= 0f)
                {
                    investigateTimer = 0.45f;
                }
                break;

            case HunterState.Chase:
                // Never chase a hidden player using perfect, all-seeing coordinates.
                if (canSee)
                {
                    lostSightTimer = loseSightDuration;
                    lastKnownPosition = playerPosition;
                }
                else
                {
                    lostSightTimer -= dt;
                }
                Face(lastKnownPosition.x);
                MoveX(direction * chaseSpeed * dt);

                // IMPORTANT: No capture based on distance alone. Must be in Chase,
                // visibly facing the player, and within reach after spawn grace.
                if (spawnGrace <= 0f && catchCooldown <= 0f && canSee &&
                    Vector2.Distance(hunterRect.anchoredPosition, playerPosition) <= catchRange)
                {
                    CatchLucien();
                    break;
                }
                if (lostSightTimer <= 0f)
                {
                    currentState = HunterState.Patrol;
                    awareness = 0f;
                    Log("Lucien escaped the hunter's sight.");
                }
                break;
        }

        UpdateVisual(dt);
    }

    private bool CanSee(Vector2 playerPosition)
    {
        Vector2 offset = playerPosition - hunterRect.anchoredPosition;

        // Rear blind spot: sneaking behind a patrol is now a real tactic.
        if (offset.x * direction < -6f) return false;

        // Being above/below the patrol path can avoid his line of sight.
        float verticalDistance = Mathf.Abs(offset.y);
        if (verticalDistance > verticalVisionRange) return false;

        float multiplier = lucien.IsSneaking ? sneakVisionMultiplier :
                           (lucien.IsSprinting ? sprintVisionMultiplier : 1f);
        float verticalFactor = Mathf.Lerp(1f, 0.65f, verticalDistance / verticalVisionRange);
        float effectiveRange = visionRange * multiplier * verticalFactor;
        return offset.sqrMagnitude <= effectiveRange * effectiveRange;
    }

    private void Spawn()
    {
        isActive = true;
        currentState = HunterState.Patrol;
        awareness = 0f;
        animationTimer = 0f;
        // Spawn at the edge farther from Lucien to avoid surprise instant captures.
        bool playerOnLeft = lucien.ParkPosition.x < 0f;
        hunterRect.anchoredPosition = new Vector2(playerOnLeft ? rightEdge : leftEdge, pathY);
        direction = playerOnLeft ? -1 : 1; // Face into the park.
        lastKnownPosition = hunterRect.anchoredPosition;
        spawnGrace = spawnGraceSeconds;
        catchCooldown = Mathf.Max(catchCooldown, spawnGraceSeconds);
        hunterRect.gameObject.SetActive(true);
        UpdateVisual(0f);
        Log("A hunter patrol enters the park. Stay out of his sight.");
    }

    private void Despawn()
    {
        isActive = false;
        awareness = 0f;
        hunterRect.gameObject.SetActive(false);
        Log("The hunter patrol leaves the area.");
    }

    private void MoveX(float dx)
    {
        Vector2 position = hunterRect.anchoredPosition;
        position.x = Mathf.Clamp(position.x + dx, leftEdge, rightEdge);
        hunterRect.anchoredPosition = position;
    }

    private void Face(float targetX)
    {
        if (targetX > hunterRect.anchoredPosition.x + 1f) direction = 1;
        else if (targetX < hunterRect.anchoredPosition.x - 1f) direction = -1;
    }

    private void CatchLucien()
    {
        parkGame.State.blood = Mathf.Max(0f, parkGame.State.blood - bloodPenalty);
        parkGame.State.suspicion = Mathf.Min(100f, parkGame.State.suspicion + suspicionPenalty);
        Log("The hunter caught Lucien! Blood -" + bloodPenalty.ToString("0") +
            ", Suspicion +" + suspicionPenalty.ToString("0") + ".");
        if (parkUI != null) parkUI.Refresh(parkGame);

        // A meaningful escape window after capture: reset the pursuit and
        // return the hunter to the edge of the park farthest from the player.
        currentState = HunterState.Patrol;
        awareness = 0f;
        investigateTimer = 0f;
        lostSightTimer = 0f;
        catchCooldown = afterCatchCooldown;
        spawnGrace = spawnGraceSeconds;
        bool playerOnLeft = lucien.ParkPosition.x < 0f;
        hunterRect.anchoredPosition = new Vector2(playerOnLeft ? rightEdge : leftEdge, pathY);
        direction = playerOnLeft ? -1 : 1;
    }

    private void UpdateVisual(float dt)
    {
        if (hunterImage == null || hunterRect == null) return;
        bool isRunning = currentState == HunterState.Chase;
        animationTimer += dt;
        Sprite[] frames = isRunning
            ? (direction > 0 ? runRightFrames : runLeftFrames)
            : (direction > 0 ? walkRightFrames : walkLeftFrames);
        float frameTime = isRunning ? runFrameTime : walkFrameTime;
        int frame = Mathf.FloorToInt(animationTimer / frameTime) % 4;
        Sprite sprite = GetFrame(frames, frame);
        bool flip = false;
        if (sprite == null)
        {
            Sprite[] opposite = isRunning
                ? (direction > 0 ? runLeftFrames : runRightFrames)
                : (direction > 0 ? walkLeftFrames : walkRightFrames);
            sprite = GetFrame(opposite, frame);
            flip = sprite != null;
        }
        if (sprite == null)
        {
            sprite = isRunning
                ? (direction > 0 ? runRight : runLeft)
                : (direction > 0 ? idleRight : idleLeft);
        }
        if (sprite == null)
        {
            sprite = isRunning
                ? (direction > 0 ? runLeft : runRight)
                : (direction > 0 ? idleLeft : idleRight);
            flip = sprite != null;
        }
        if (currentState == HunterState.Investigate)
        {
            Sprite idle = direction > 0 ? idleRight : idleLeft;
            if (idle != null) { sprite = idle; flip = false; }
        }
        if (sprite != null) hunterImage.sprite = sprite;
        hunterRect.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);

        if (awarenessLabel != null)
        {
            awarenessLabel.text = !showAwarenessIcon ? "" :
                currentState == HunterState.Chase ? "!" :
                currentState == HunterState.Investigate ? "?" : "";
            awarenessLabel.color = currentState == HunterState.Chase
                ? new Color(0.95f, 0.22f, 0.27f)
                : new Color(0.95f, 0.80f, 0.52f);
        }
    }

    private static Sprite GetFrame(Sprite[] frames, int frame)
    {
        if (frames == null || frames.Length == 0) return null;
        // A partially populated animation also works during setup.
        Sprite chosen = frames[frame % frames.Length];
        if (chosen != null) return chosen;
        for (int i = 0; i < frames.Length; i++)
            if (frames[i] != null) return frames[i];
        return null;
    }

    private void Log(string message)
    {
        if (parkGame != null) parkGame.State.lastMessage = message;
        Debug.Log("[Mourneveil Hunter] " + message);
    }
}
