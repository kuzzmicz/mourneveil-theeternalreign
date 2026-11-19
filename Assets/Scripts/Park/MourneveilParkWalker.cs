using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Park v0.3 - roaming target with simple flee / respawn state.
// Click marks the target; only the E key while close starts an encounter.
[DisallowMultipleComponent]
public sealed class MourneveilParkWalker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Image artwork;
    private Button button;
    private TMP_Text hoverLabel;
    private RectTransform rect;
    private MourneveilParkPlayer player;
    private Vector2 home;
    private Vector2 position;
    private float speed;
    private float phase;
    private float cooldown;
    private int index;
    private bool hovered;
    private bool selected;
    private bool marked;
    private bool locked;
    private bool initialized;
    private bool usesSprite;

    public Vector2 ParkPosition { get { return position; } }
    public bool IsAvailable { get { return initialized && cooldown <= 0f && !locked; } }

    public void Initialize(Image image, Button targetButton, TMP_Text label,
        Vector2 homePosition, float walkSpeed, int id, MourneveilParkPlayer controller)
    {
        artwork = image;
        usesSprite = image != null && image.sprite != null;
        button = targetButton;
        hoverLabel = label;
        rect = GetComponent<RectTransform>();
        home = homePosition;
        position = home;
        speed = walkSpeed;
        phase = id * 2.25f;
        index = id;
        player = controller;
        rect.anchoredPosition = position;
        initialized = true;
        hoverLabel.text = "MARK";
        hoverLabel.color = new Color(0.95f, 0.86f, 0.81f, 0f);
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
        cooldown = Random.Range(4.5f, 8f);
        position = new Vector2(Mathf.Clamp(home.x + Random.Range(-95f, 95f), -495f, 495f),
            Mathf.Clamp(home.y + Random.Range(-25f, 25f), 90f, 300f));
        home = position;
        marked = false;
        hovered = false;
    }

    private void Update()
    {
        if (!initialized) return;
        float dt = Time.deltaTime;
        if (cooldown > 0f) cooldown = Mathf.Max(0f, cooldown - dt);

        if (!locked && cooldown <= 0f)
        {
            float time = Time.time * speed + phase;
            Vector2 destination = home + new Vector2(Mathf.Sin(time * 0.53f) * 78f,
                Mathf.Sin(time * 0.33f + phase) * 20f);

            Vector2 away = position - player.ParkPosition;
            bool fleeing = !player.IsSneaking && away.sqrMagnitude < 150f * 150f;
            if (fleeing && away.sqrMagnitude > 0.01f)
                position += away.normalized * (75f + index * 9f) * dt;
            else
                position = Vector2.MoveTowards(position, destination, (53f + index * 7f) * dt);

            position.x = Mathf.Clamp(position.x, -520f, 520f);
            position.y = Mathf.Clamp(position.y, 88f, 304f);
        }
        rect.anchoredPosition = position;
        bool visible = cooldown <= 0f;
        if (button != null) button.interactable = visible && !locked;
        if (artwork != null)
        {
            Color targetColor = !visible ? new Color(1f, 1f, 1f, 0f)
                : selected ? new Color(1f, 0.68f, 0.73f, 1f)
                : locked ? new Color(0.55f, 0.48f, 0.52f, 1f)
                : (hovered || marked) ? new Color(1f, 0.85f, 0.9f, 1f)
                : Color.white;
            if (!usesSprite)
            {
                Color placeholder = new Color(0.2f, 0.09f, 0.16f, targetColor.a);
                targetColor = placeholder;
            }
            artwork.color = Color.Lerp(artwork.color, targetColor, dt * 11f);
        }
        if (hoverLabel != null)
        {
            Color c = hoverLabel.color;
            c.a = Mathf.MoveTowards(c.a,
                visible && !locked && (hovered || marked) ? 1f : 0f, dt * 8f);
            hoverLabel.color = c;
            hoverLabel.text = marked ? "MARKED" : "MARK";
        }
        Vector3 desiredScale = Vector3.one * (selected ? 1.08f : hovered || marked ? 1.05f : 1f);
        rect.localScale = Vector3.Lerp(rect.localScale, desiredScale, dt * 10f);
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
