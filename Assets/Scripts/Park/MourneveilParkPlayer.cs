using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Park v0.3 - UI world player controller. No Rigidbody or colliders required.
// Supports Unity's new Input System and legacy Input Manager.
[DisallowMultipleComponent]
public sealed class MourneveilParkPlayer : MonoBehaviour
{
    [Header("Movement in the Park UI (reference pixels / second)")]
    [SerializeField, Min(10f)] private float walkingSpeed = 215f;
    [SerializeField, Min(10f)] private float sprintSpeed = 325f;
    [SerializeField, Min(10f)] private float sneakSpeed = 105f;
    [SerializeField] private Vector2 horizontalLimits = new Vector2(-515f, 515f);
    [SerializeField] private Vector2 verticalLimits = new Vector2(58f, 305f);

    private RectTransform rect;
    private Image image;
    private Vector2 startPosition;
    private bool encounterActive;
    private bool sneaking;
    private bool sprinting;
    private bool moving;
    private Vector3 initialImageScale;

    public Vector2 ParkPosition { get { return rect == null ? Vector2.zero : rect.anchoredPosition; } }
    public bool IsSneaking { get { return sneaking && !encounterActive; } }
    public bool IsSprinting { get { return sprinting && !encounterActive; } }
    public bool IsMoving { get { return moving && !encounterActive; } }
    public bool EncounterActive { get { return encounterActive; } }

    public void Initialize(Image artwork, Vector2 startingPosition)
    {
        rect = GetComponent<RectTransform>();
        image = artwork;
        startPosition = startingPosition;
        rect.anchoredPosition = startPosition;
        initialImageScale = image == null ? Vector3.one : image.rectTransform.localScale;
    }

    public void SetEncounterActive(bool active)
    {
        encounterActive = active;
    }

    private void Update()
    {
        if (rect == null) return;
        if (encounterActive)
        {
            moving = false;
            return;
        }

        Vector2 axis = ReadMoveKeys();
        moving = axis.sqrMagnitude > 0.01f;
        sneaking = ReadSneakKey();
        sprinting = !sneaking && ReadSprintKey();
        float speed = sneaking ? sneakSpeed : (sprinting ? sprintSpeed : walkingSpeed);

        if (axis.sqrMagnitude > 1f) axis.Normalize();
        Vector2 next = rect.anchoredPosition + axis * speed * Time.deltaTime;
        next.x = Mathf.Clamp(next.x, horizontalLimits.x, horizontalLimits.y);
        next.y = Mathf.Clamp(next.y, verticalLimits.x, verticalLimits.y);
        rect.anchoredPosition = next;

        if (image != null)
        {
            Vector3 targetScale = initialImageScale;
            if (axis.x < -0.1f) targetScale.x = -Mathf.Abs(initialImageScale.x);
            else if (axis.x > 0.1f) targetScale.x = Mathf.Abs(initialImageScale.x);
            // Flip only the sprite Image, never the player root or its label.
            image.rectTransform.localScale = Vector3.Lerp(
                image.rectTransform.localScale, targetScale, Time.deltaTime * 12f);
            Color targetColor = sneaking ? new Color(0.78f, 0.75f, 0.85f) : Color.white;
            image.color = Color.Lerp(image.color, targetColor, Time.deltaTime * 8f);
        }
    }

    public static bool InteractJustPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null) return Keyboard.current.eKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.E);
#else
        return false;
#endif
    }

    private static Vector2 ReadMoveKeys()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        if (k != null)
        {
            float x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f) -
                      (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1f : 0f);
            float y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1f : 0f) -
                      (k.sKey.isPressed || k.downArrowKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(
            (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) -
            (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
            (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) -
            (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));
#else
        return Vector2.zero;
#endif
    }

    private static bool ReadSneakKey()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
#else
        return false;
#endif
    }

    private static bool ReadSprintKey()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
        return false;
#endif
    }
}
