using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Mourneveil
{
    [Serializable]
    public class BloodVialUI
    {
        [Tooltip("UI Button representing this vial (Image on the Button can show the full bottle).")]
        public Button button;

        [Tooltip("Optional child object showing an empty/dimmed vial after drinking. Start inactive. Disable Raycast Target on its Image.")]
        public GameObject emptyOverlay;

        [Tooltip("Optional child RectTransform containing only the bottle art. If empty, animates the Button itself.")]
        public RectTransform visualTransform;

        [Tooltip("Optional CanvasGroup on the visualTransform (or on the button). Used for a gentle fade during drinking.")]
        public CanvasGroup canvasGroup;
    }

    public enum PrologueSpeaker { Lucien, Rowan }

    [Serializable]
    public class PrologueDialogueLine
    {
        public PrologueSpeaker speaker = PrologueSpeaker.Rowan;
        [TextArea(2, 5)] public string text;

        public PrologueDialogueLine() { }

        public PrologueDialogueLine(PrologueSpeaker who, string content)
        {
            speaker = who;
            text = content;
        }
    }

    /// <summary>
    /// Prologue v0.4: animated blood vials and character portraits; retains v0.2
    /// intro fade, flashbacks, hunger, heartbeat, knocks and typewriter.
    /// Attach to _Prologue in Crypt_Intro (not to each vial).
    /// </summary>
    public sealed class MourneveilPrologue : MonoBehaviour
    {
        private enum ProloguePhase
        {
            Drinking,
            Hunger,
            Knocking,
            Dialogue,
            Finished
        }

        [Header("Intro UI - existing v0.2 references")]
        [SerializeField] private GameObject introPanel;
        [SerializeField] private TMP_Text storyText;
        [SerializeField] private TMP_Text memoryText;
        [SerializeField] private TMP_Text vialsText;
        [Tooltip("Old Drink button. Keep its reference for compatibility; it is hidden when Blood Vials are configured.")]
        [SerializeField] private Button drinkButton;
        [Tooltip("Legacy decorative icons. Leave empty when using interactive Blood Vials.")]
        [SerializeField] private GameObject[] vialIcons;

        [Header("v0.3 - Interactive Blood Vials")]
        [Tooltip("Create 5 UI Buttons in VialsPanel and assign them here, in the desired order. With vials configured, their count replaces Starting Vials.")]
        [SerializeField] private BloodVialUI[] bloodVials = new BloodVialUI[0];
        [Tooltip("If true, only the next vial can be clicked. If false, the player can drink the 5 vials in any order.")]
        [SerializeField] private bool requireSequentialVials = true;

        [Header("v0.4 - Vial Animation")]
        [SerializeField] private bool animateVials = true;
        [SerializeField, Range(0.10f, 1f)] private float vialAnimationSeconds = 0.32f;
        [SerializeField, Range(0f, 70f)] private float vialLiftPixels = 26f;
        [SerializeField, Range(0f, 50f)] private float vialTiltDegrees = 18f;
        [SerializeField, Range(0.55f, 1f)] private float vialShrinkScale = 0.84f;

        [Header("Atmosphere - existing v0.2 references")]
        [SerializeField] private Image hungerOverlay;
        [SerializeField] private AudioSource soundSource;
        [SerializeField] private AudioClip drinkingSound;
        [SerializeField] private AudioClip knockingSound;

        [Header("Friend dialogue - existing v0.2 references")]
        [SerializeField] private GameObject friendPanel;
        [SerializeField] private TMP_Text friendDialogueText;
        [SerializeField] private Button nextDialogueButton;

        [Header("v0.4 - Dialogue Portraits (optional)")]
        [SerializeField] private Image lucienPortrait;
        [SerializeField] private Image rowanPortrait;
        [SerializeField] private TMP_Text speakerNameText;
        [SerializeField, Range(0.1f, 1f)] private float inactivePortraitBrightness = 0.46f;
        [SerializeField, Range(0f, 1f)] private float portraitFadeSeconds = 0.22f;

        [Tooltip("OFF preserves your existing v0.2/v0.3 Friend Dialogue text. ON uses the speaker-aware lines below.")]
        [SerializeField] private bool useCharacterDialogue = false;
        [SerializeField]
        private PrologueDialogueLine[] characterDialogue =
        {
            new PrologueDialogueLine(PrologueSpeaker.Rowan, "Lucien...? Thank the night. I was afraid you would never wake."),
            new PrologueDialogueLine(PrologueSpeaker.Lucien, "Who are you...?"),
            new PrologueDialogueLine(PrologueSpeaker.Rowan, "Rowan. I left those bottles for you. I knew you would be starving."),
            new PrologueDialogueLine(PrologueSpeaker.Lucien, "Mourneveil... I remember fire. My family..."),
            new PrologueDialogueLine(PrologueSpeaker.Rowan, "The hunters came. Your family... they're gone."),
            new PrologueDialogueLine(PrologueSpeaker.Lucien, "And you? Why did you come back for me?"),
            new PrologueDialogueLine(PrologueSpeaker.Rowan, "Because you're my friend. The last heir must survive."),
            new PrologueDialogueLine(PrologueSpeaker.Lucien, "I need more blood."),
            new PrologueDialogueLine(PrologueSpeaker.Rowan, "Then come. There's a park nearby. We must be careful.")
        };
        [Tooltip("Speaker for each original Friend Dialogue element (only used while Use Character Dialogue is OFF).")]
        [SerializeField]
        private PrologueSpeaker[] legacyDialogueSpeakers =
        {
            PrologueSpeaker.Rowan, PrologueSpeaker.Rowan, PrologueSpeaker.Rowan,
            PrologueSpeaker.Rowan, PrologueSpeaker.Rowan, PrologueSpeaker.Rowan
        };

        [Header("End of current prototype - existing v0.2 references")]
        [SerializeField] private GameObject endPanel;
        [SerializeField] private TMP_Text endText;

        [Header("Timing and content - existing v0.2 references")]
        [Tooltip("Used only if Blood Vials are not configured (old Drink button fallback).")]
        [SerializeField, Min(1)] private int startingVials = 5;
        [SerializeField, Min(0.5f)] private float hungerSeconds = 4f;
        [SerializeField, Min(0.2f)] private float pauseBetweenKnocks = 0.8f;

        [SerializeField, TextArea(2, 4)]
        private string[] memoryFragments =
        {
            "Cold stone... Where am I?",
            "A silver crest. A name I cannot quite recall...",
            "Fire in the halls. Steel striking steel.",
            "Someone was calling out to me. A voice I knew.",
            "Mourneveil... Why does that name hurt?"
        };

        [SerializeField, TextArea(2, 5)]
        private string[] friendDialogue =
        {
            "There you are. I was afraid you wouldn't wake.",
            "I left those bottles beside you. I knew you would be starving.",
            "You don't remember me, do you? We were friends, a long time ago.",
            "The hunters came for your family. The House of Mourneveil fell.",
            "You are its last heir. But the bloodline does not have to end here.",
            "The bottles were only enough to wake you. Come. We have to find more blood."
        };

        [Header("v0.2 - Optional visuals")]
        [SerializeField] private Image blackFadeOverlay;
        [SerializeField] private Image memoryFlashOverlay;
        [SerializeField] private RectTransform shakeTarget;
        [SerializeField, Min(0f)] private float introFadeSeconds = 1.8f;
        [SerializeField, Min(0f)] private float memoryFlashSeconds = 0.32f;
        [SerializeField, Range(0f, 20f)] private float knockShakePixels = 9f;
        [Tooltip("Disables flashing, shaking and animated dialogue typing.")]
        [SerializeField] private bool reduceMotion = false;

        [Header("v0.2 - Optional dialogue typewriter")]
        [SerializeField] private bool typewriterEnabled = true;
        [SerializeField, Range(10f, 120f)] private float dialogueCharactersPerSecond = 42f;

        [Header("v0.2 - Optional heartbeat")]
        [SerializeField] private AudioSource heartbeatSource;
        [SerializeField] private AudioClip heartbeatSound;

        private ProloguePhase phase;
        private int vialsRemaining;
        private int totalVials;
        private int dialogueIndex;
        private bool usingInteractiveVials;
        private bool[] consumedVials;
        private bool openingFadeInProgress;
        private bool typingDialogue;
        private bool vialAnimationInProgress;
        private Coroutine vialAnimation;
        private Coroutine portraitTransition;
        private RectTransform animatedVialTransform;
        private CanvasGroup animatedVialCanvasGroup;
        private Vector3 vialOriginalLocalPosition;
        private Vector3 vialOriginalLocalScale;
        private Quaternion vialOriginalLocalRotation;
        private float vialOriginalAlpha;
        private Color lucienPortraitBaseColor = Color.white;
        private Color rowanPortraitBaseColor = Color.white;

        private UnityAction[] vialClickActions;
        private Coroutine sequence;
        private Coroutine dialogueTyping;
        private Coroutine memoryFlash;
        private Coroutine openingFade;
        private Vector2 shakeOriginalPosition;

        private void Start()
        {
            if (storyText == null || vialsText == null || friendPanel == null ||
                friendDialogueText == null || nextDialogueButton == null || endPanel == null)
            {
                Debug.LogError("[Mourneveil Prologue] Missing required text/panel references on _Prologue.", this);
                enabled = false;
                return;
            }

            usingInteractiveVials = bloodVials != null && bloodVials.Length > 0;
            totalVials = usingInteractiveVials ? bloodVials.Length : Mathf.Max(1, startingVials);

            if (usingInteractiveVials)
            {
                for (int i = 0; i < bloodVials.Length; i++)
                {
                    if (bloodVials[i] == null || bloodVials[i].button == null)
                    {
                        Debug.LogError($"[Mourneveil Prologue] Assign Button for Blood Vials Element {i}.", this);
                        enabled = false;
                        return;
                    }
                }

                consumedVials = new bool[totalVials];
                vialClickActions = new UnityAction[totalVials];

                for (int i = 0; i < totalVials; i++)
                {
                    int vialIndex = i; // Each listener captures its OWN index.
                    vialClickActions[i] = () => OnBloodVialClicked(vialIndex);
                    bloodVials[i].button.onClick.AddListener(vialClickActions[i]);
                }

                // Preserve serialized legacy Drink Button, but don't show a second way to drink.
                if (drinkButton != null)
                    drinkButton.gameObject.SetActive(false);

                if (startingVials != totalVials)
                    Debug.LogWarning("[Mourneveil Prologue] Starting Vials is ignored when interactive Blood Vials are configured. Using " + totalVials + ".", this);
            }
            else
            {
                if (drinkButton == null)
                {
                    Debug.LogError("[Mourneveil Prologue] Add Blood Vials or assign the old Drink Button.", this);
                    enabled = false;
                    return;
                }

                drinkButton.onClick.AddListener(DrinkFromVial);
            }

            nextDialogueButton.onClick.AddListener(NextDialogue);

            if (shakeTarget != null)
                shakeOriginalPosition = shakeTarget.anchoredPosition;
            if (lucienPortrait != null) lucienPortraitBaseColor = lucienPortrait.color;
            if (rowanPortrait != null) rowanPortraitBaseColor = rowanPortrait.color;

            RestartPrologue();
        }

        private void OnDestroy()
        {
            if (drinkButton != null)
                drinkButton.onClick.RemoveListener(DrinkFromVial);

            if (nextDialogueButton != null)
                nextDialogueButton.onClick.RemoveListener(NextDialogue);

            if (vialClickActions != null && bloodVials != null)
            {
                for (int i = 0; i < vialClickActions.Length; i++)
                {
                    if (bloodVials[i] != null && bloodVials[i].button != null)
                        bloodVials[i].button.onClick.RemoveListener(vialClickActions[i]);
                }
            }

            RestoreVialVisual();
            StopHeartbeat();
        }

        /// <summary>Legacy fallback for the original single Drink button.</summary>
        public void DrinkFromVial()
        {
            if (usingInteractiveVials || phase != ProloguePhase.Drinking ||
                openingFadeInProgress || vialsRemaining <= 0)
                return;

            ConsumeOneVial(-1);
        }

        private void OnBloodVialClicked(int index)
        {
            if (!usingInteractiveVials || phase != ProloguePhase.Drinking ||
                openingFadeInProgress || vialsRemaining <= 0 ||
                index < 0 || index >= totalVials || consumedVials[index])
                return;

            if (requireSequentialVials && index != totalVials - vialsRemaining)
                return;
            if (vialAnimationInProgress)
                return;

            if (animateVials && !reduceMotion)
                vialAnimation = StartCoroutine(DrinkVialAnimation(index));
            else
                ConsumeOneVial(index);
        }

        private IEnumerator DrinkVialAnimation(int index)
        {
            vialAnimationInProgress = true;
            SetButtonsInteractable(false);

            BloodVialUI vial = bloodVials[index];
            animatedVialTransform = vial.visualTransform != null
                ? vial.visualTransform : vial.button.GetComponent<RectTransform>();
            animatedVialCanvasGroup = vial.canvasGroup;
            if (animatedVialCanvasGroup == null && animatedVialTransform != null)
                animatedVialCanvasGroup = animatedVialTransform.GetComponent<CanvasGroup>();

            if (animatedVialTransform != null)
            {
                vialOriginalLocalPosition = animatedVialTransform.localPosition;
                vialOriginalLocalScale = animatedVialTransform.localScale;
                vialOriginalLocalRotation = animatedVialTransform.localRotation;
            }
            vialOriginalAlpha = animatedVialCanvasGroup != null ? animatedVialCanvasGroup.alpha : 1f;

            float duration = Mathf.Max(0.05f, vialAnimationSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = t * t * (3f - 2f * t);
                if (animatedVialTransform != null)
                {
                    animatedVialTransform.localPosition = vialOriginalLocalPosition + Vector3.up * (vialLiftPixels * smooth);
                    animatedVialTransform.localScale = Vector3.Lerp(vialOriginalLocalScale, vialOriginalLocalScale * vialShrinkScale, smooth);
                    animatedVialTransform.localRotation = vialOriginalLocalRotation * Quaternion.Euler(0f, 0f, -vialTiltDegrees * smooth);
                }
                if (animatedVialCanvasGroup != null)
                    animatedVialCanvasGroup.alpha = Mathf.Lerp(vialOriginalAlpha, 0.15f, smooth);
                yield return null;
            }

            // Restore original visuals so EmptyOverlay can mark the vial as consumed.
            RestoreVialVisual();
            vialAnimationInProgress = false;
            vialAnimation = null;
            ConsumeOneVial(index); // Existing memories, audio, hunger and knocking.
        }

        private void RestoreVialVisual()
        {
            if (animatedVialTransform != null)
            {
                animatedVialTransform.localPosition = vialOriginalLocalPosition;
                animatedVialTransform.localScale = vialOriginalLocalScale;
                animatedVialTransform.localRotation = vialOriginalLocalRotation;
            }
            if (animatedVialCanvasGroup != null)
                animatedVialCanvasGroup.alpha = vialOriginalAlpha;
            animatedVialTransform = null;
            animatedVialCanvasGroup = null;
        }

        private void ConsumeOneVial(int clickedIndex)
        {
            if (phase != ProloguePhase.Drinking || vialsRemaining <= 0)
                return;

            if (usingInteractiveVials)
                consumedVials[clickedIndex] = true;

            vialsRemaining--;
            int consumedCount = totalVials - vialsRemaining;

            if (memoryText != null && memoryFragments != null && memoryFragments.Length > 0)
            {
                int memoryIndex = Mathf.Min(consumedCount - 1, memoryFragments.Length - 1);
                memoryText.text = memoryFragments[memoryIndex];
            }

            PlaySound(drinkingSound);
            RefreshVials();

            if (memoryFlash != null)
            {
                StopCoroutine(memoryFlash);
                memoryFlash = null;
            }

            SetImageAlpha(memoryFlashOverlay, 0f);
            if (!reduceMotion && memoryFlashOverlay != null)
                memoryFlash = StartCoroutine(FlashMemory());

            if (vialsRemaining <= 0)
            {
                SetButtonsInteractable(false);
                sequence = StartCoroutine(HungerAndKnocking());
            }
        }

        private void SetButtonsInteractable(bool allowDrinking)
        {
            bool canDrink = allowDrinking && phase == ProloguePhase.Drinking &&
                            !openingFadeInProgress && !vialAnimationInProgress && vialsRemaining > 0;

            if (!usingInteractiveVials)
            {
                if (drinkButton != null)
                    drinkButton.interactable = canDrink;
                return;
            }

            int nextIndex = totalVials - vialsRemaining;
            for (int i = 0; i < totalVials; i++)
            {
                if (bloodVials[i] != null && bloodVials[i].button != null)
                    bloodVials[i].button.interactable = canDrink && !consumedVials[i] &&
                        (!requireSequentialVials || i == nextIndex);
            }
        }

        private void RefreshVials()
        {
            vialsText.text = "Blood vials: " + vialsRemaining + " / " + totalVials;

            if (usingInteractiveVials)
            {
                for (int i = 0; i < totalVials; i++)
                {
                    if (bloodVials[i] != null && bloodVials[i].emptyOverlay != null)
                        bloodVials[i].emptyOverlay.SetActive(consumedVials[i]);
                }
            }
            else if (vialIcons != null)
            {
                // Legacy decorative icon logic retained for existing v0.2 scenes.
                for (int i = 0; i < vialIcons.Length; i++)
                {
                    if (vialIcons[i] != null)
                        vialIcons[i].SetActive(i < vialsRemaining);
                }
            }

            SetButtonsInteractable(true);
        }

        private IEnumerator OpeningFade()
        {
            openingFadeInProgress = true;
            SetButtonsInteractable(false);

            if (blackFadeOverlay == null || reduceMotion || introFadeSeconds <= 0f)
            {
                SetImageAlpha(blackFadeOverlay, 0f);
                openingFadeInProgress = false;
                SetButtonsInteractable(true);
                openingFade = null;
                yield break;
            }

            SetImageAlpha(blackFadeOverlay, 1f);
            float elapsed = 0f;
            while (elapsed < introFadeSeconds)
            {
                elapsed += Time.deltaTime;
                SetImageAlpha(blackFadeOverlay, 1f - Mathf.Clamp01(elapsed / introFadeSeconds));
                yield return null;
            }

            SetImageAlpha(blackFadeOverlay, 0f);
            openingFadeInProgress = false;
            SetButtonsInteractable(true);
            openingFade = null;
        }

        private IEnumerator FlashMemory()
        {
            float duration = Mathf.Max(0.01f, memoryFlashSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetImageAlpha(memoryFlashOverlay, (1f - t) * 0.25f);
                yield return null;
            }

            SetImageAlpha(memoryFlashOverlay, 0f);
            memoryFlash = null;
        }

        private IEnumerator HungerAndKnocking()
        {
            phase = ProloguePhase.Hunger;
            storyText.text = "The last bottle is empty. The hunger is not.";
            StartHeartbeat();

            float elapsed = 0f;
            float duration = Mathf.Max(0.5f, hungerSeconds);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float pulse = (Mathf.Sin(elapsed * Mathf.Lerp(5f, 11f, progress)) + 1f) * 0.5f;
                float alpha = reduceMotion ? 0.07f : Mathf.Lerp(0.05f, 0.12f + 0.26f * progress, pulse);
                SetImageAlpha(hungerOverlay, alpha);

                if (heartbeatSource != null && heartbeatSource.isPlaying)
                {
                    heartbeatSource.volume = Mathf.Lerp(0.18f, 0.58f, progress);
                    heartbeatSource.pitch = Mathf.Lerp(0.90f, 1.20f, progress);
                }
                yield return null;
            }

            StopHeartbeat();
            phase = ProloguePhase.Knocking;
            storyText.text = "...Someone is at the door.";
            yield return new WaitForSeconds(0.5f);

            for (int i = 0; i < 3; i++)
            {
                storyText.text = i == 0 ? "BANG!" : i == 1 ? "BANG! BANG!" : "THE DOOR SHUDDERS.";
                PlaySound(knockingSound);
                SetImageAlpha(hungerOverlay, reduceMotion ? 0.07f : 0.35f);

                if (!reduceMotion && shakeTarget != null)
                    yield return StartCoroutine(ShakeScreen(0.21f));
                else
                    yield return new WaitForSeconds(0.21f);

                SetImageAlpha(hungerOverlay, 0.025f);
                yield return new WaitForSeconds(Mathf.Max(0.2f, pauseBetweenKnocks));
            }

            SetImageAlpha(hungerOverlay, 0f);
            BeginFriendDialogue();
            sequence = null;
        }

        private IEnumerator ShakeScreen(float duration)
        {
            if (shakeTarget == null)
                yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float strength = knockShakePixels * (1f - Mathf.Clamp01(elapsed / duration));
                shakeTarget.anchoredPosition = shakeOriginalPosition +
                    new Vector2(UnityEngine.Random.Range(-strength, strength), UnityEngine.Random.Range(-strength, strength));
                yield return null;
            }
            shakeTarget.anchoredPosition = shakeOriginalPosition;
        }

        private void BeginFriendDialogue()
        {
            phase = ProloguePhase.Dialogue;
            dialogueIndex = 0;
            if (introPanel != null) introPanel.SetActive(false);
            friendPanel.SetActive(true);
            nextDialogueButton.interactable = true;
            ShowDialogueLine();
        }

        private int DialogueCount()
        {
            if (useCharacterDialogue && characterDialogue != null && characterDialogue.Length > 0)
                return characterDialogue.Length;
            return friendDialogue != null ? friendDialogue.Length : 0;
        }

        private string DialogueTextAt(int index)
        {
            if (useCharacterDialogue && characterDialogue != null && characterDialogue.Length > 0)
                return characterDialogue[index] != null ? characterDialogue[index].text : "";
            return friendDialogue[index];
        }

        private PrologueSpeaker SpeakerAt(int index)
        {
            if (useCharacterDialogue && characterDialogue != null && characterDialogue.Length > 0)
                return characterDialogue[index] != null ? characterDialogue[index].speaker : PrologueSpeaker.Rowan;
            if (legacyDialogueSpeakers != null && index < legacyDialogueSpeakers.Length)
                return legacyDialogueSpeakers[index];
            return PrologueSpeaker.Rowan;
        }

        private void UpdateSpeakerVisuals(PrologueSpeaker speaker)
        {
            if (speakerNameText != null)
                speakerNameText.text = speaker == PrologueSpeaker.Lucien ? "Lucien" : "Rowan";

            Color lucienTarget = PortraitTint(lucienPortraitBaseColor, speaker == PrologueSpeaker.Lucien);
            Color rowanTarget = PortraitTint(rowanPortraitBaseColor, speaker == PrologueSpeaker.Rowan);
            if (portraitTransition != null)
                StopCoroutine(portraitTransition);
            portraitTransition = null;

            if (reduceMotion || portraitFadeSeconds <= 0f)
            {
                if (lucienPortrait != null) lucienPortrait.color = lucienTarget;
                if (rowanPortrait != null) rowanPortrait.color = rowanTarget;
            }
            else
                portraitTransition = StartCoroutine(FadePortraits(lucienTarget, rowanTarget));
        }

        private Color PortraitTint(Color baseColor, bool active)
        {
            if (active) return baseColor;
            return new Color(baseColor.r * inactivePortraitBrightness,
                baseColor.g * inactivePortraitBrightness,
                baseColor.b * inactivePortraitBrightness, baseColor.a);
        }

        private IEnumerator FadePortraits(Color lucienTarget, Color rowanTarget)
        {
            Color lucienStart = lucienPortrait != null ? lucienPortrait.color : Color.white;
            Color rowanStart = rowanPortrait != null ? rowanPortrait.color : Color.white;
            float elapsed = 0f;
            while (elapsed < portraitFadeSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / portraitFadeSeconds);
                if (lucienPortrait != null) lucienPortrait.color = Color.Lerp(lucienStart, lucienTarget, t);
                if (rowanPortrait != null) rowanPortrait.color = Color.Lerp(rowanStart, rowanTarget, t);
                yield return null;
            }
            if (lucienPortrait != null) lucienPortrait.color = lucienTarget;
            if (rowanPortrait != null) rowanPortrait.color = rowanTarget;
            portraitTransition = null;
        }

        public void NextDialogue()
        {
            if (phase != ProloguePhase.Dialogue)
                return;

            if (typingDialogue)
            {
                if (dialogueTyping != null)
                    StopCoroutine(dialogueTyping);
                dialogueTyping = null;
                typingDialogue = false;
                friendDialogueText.maxVisibleCharacters = int.MaxValue;
                return;
            }

            dialogueIndex++;
            if (dialogueIndex >= DialogueCount())
            {
                FinishPrologue();
                return;
            }

            ShowDialogueLine();
        }

        private void ShowDialogueLine()
        {
            if (dialogueTyping != null)
            {
                StopCoroutine(dialogueTyping);
                dialogueTyping = null;
            }
            typingDialogue = false;

            if (DialogueCount() == 0)
            {
                friendDialogueText.text = "We must leave. The night is waiting.";
                UpdateSpeakerVisuals(PrologueSpeaker.Rowan);
            }
            else
            {
                int line = Mathf.Clamp(dialogueIndex, 0, DialogueCount() - 1);
                friendDialogueText.text = DialogueTextAt(line);
                UpdateSpeakerVisuals(SpeakerAt(line));
            }

            friendDialogueText.maxVisibleCharacters = int.MaxValue;
            if (typewriterEnabled && !reduceMotion)
                dialogueTyping = StartCoroutine(RevealDialogue());
        }

        private IEnumerator RevealDialogue()
        {
            typingDialogue = true;
            friendDialogueText.maxVisibleCharacters = 0;
            friendDialogueText.ForceMeshUpdate();
            int characterCount = friendDialogueText.textInfo.characterCount;
            float revealed = 0f;

            while (revealed < characterCount)
            {
                revealed += Mathf.Max(10f, dialogueCharactersPerSecond) * Time.deltaTime;
                friendDialogueText.maxVisibleCharacters = Mathf.Min(characterCount, Mathf.FloorToInt(revealed));
                yield return null;
            }

            friendDialogueText.maxVisibleCharacters = int.MaxValue;
            typingDialogue = false;
            dialogueTyping = null;
        }

        private void FinishPrologue()
        {
            phase = ProloguePhase.Finished;
            friendPanel.SetActive(false);
            endPanel.SetActive(true);
            if (endText != null)
            {
                endText.text = "CHAPTER II - THE FIRST HUNT\n\n" +
                    "The last heir of Mourneveil must feed.\n" +
                    "To be continued...";
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (soundSource != null && clip != null)
                soundSource.PlayOneShot(clip, 0.7f);
        }

        private void StartHeartbeat()
        {
            if (heartbeatSource == null || heartbeatSound == null)
                return;

            heartbeatSource.Stop();
            heartbeatSource.clip = heartbeatSound;
            heartbeatSource.loop = true;
            heartbeatSource.playOnAwake = false;
            heartbeatSource.volume = 0.18f;
            heartbeatSource.pitch = 0.90f;
            heartbeatSource.Play();
        }

        private void StopHeartbeat()
        {
            if (heartbeatSource == null)
                return;
            heartbeatSource.Stop();
            heartbeatSource.pitch = 1f;
        }

        private void SetImageAlpha(Image image, float alpha)
        {
            if (image == null) return;
            Color tint = image.color;
            tint.a = Mathf.Clamp01(alpha);
            image.color = tint;
        }

        [ContextMenu("DEV: Restart Prologue (Play Mode)")]
        public void RestartPrologue()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Mourneveil Prologue] Restart is available in Play Mode.", this);
                return;
            }

            StopAllCoroutines();
            RestoreVialVisual();
            vialAnimationInProgress = false;
            vialAnimation = null;
            portraitTransition = null;
            if (lucienPortrait != null) lucienPortrait.color = lucienPortraitBaseColor;
            if (rowanPortrait != null) rowanPortrait.color = rowanPortraitBaseColor;
            if (speakerNameText != null) speakerNameText.text = "";
            sequence = null;
            memoryFlash = null;
            openingFade = null;
            dialogueTyping = null;
            typingDialogue = false;
            openingFadeInProgress = true;
            StopHeartbeat();

            if (shakeTarget != null)
                shakeTarget.anchoredPosition = shakeOriginalPosition;

            phase = ProloguePhase.Drinking;
            vialsRemaining = totalVials;
            dialogueIndex = 0;
            if (consumedVials != null)
                Array.Clear(consumedVials, 0, consumedVials.Length);

            if (introPanel != null) introPanel.SetActive(true);
            friendPanel.SetActive(false);
            endPanel.SetActive(false);
            friendDialogueText.maxVisibleCharacters = int.MaxValue;
            storyText.text = "The stone is cold. You open your eyes beside a coffin.\nThere are bottles filled with blood.";
            if (memoryText != null) memoryText.text = "Who am I...?";

            SetImageAlpha(hungerOverlay, 0f);
            SetImageAlpha(memoryFlashOverlay, 0f);
            SetImageAlpha(blackFadeOverlay, 0f);
            RefreshVials();
            openingFade = StartCoroutine(OpeningFade());
        }
    }
}
