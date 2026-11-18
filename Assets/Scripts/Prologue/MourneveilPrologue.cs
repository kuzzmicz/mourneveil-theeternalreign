using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mourneveil
{
    /// <summary>
    /// Mourneveil Prologue v0.2: an upgrade of v0.1, preserving all existing serialized fields.
    /// The five story vials are intentionally separate from the Blood economy.
    /// Attach to _Prologue in Crypt_Intro. No custom Editor scripts are required.
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

        [Header("Intro UI — existing references from v0.1")]
        [SerializeField] private GameObject introPanel;
        [SerializeField] private TMP_Text storyText;
        [SerializeField] private TMP_Text memoryText;
        [SerializeField] private TMP_Text vialsText;
        [SerializeField] private Button drinkButton;
        [Tooltip("Optional: up to five separate vial GameObjects, ordered left to right.")]
        [SerializeField] private GameObject[] vialIcons;

        [Header("Atmosphere — existing references from v0.1")]
        [Tooltip("A fullscreen red Image with Raycast Target disabled.")]
        [SerializeField] private Image hungerOverlay;
        [SerializeField] private AudioSource soundSource;
        [SerializeField] private AudioClip drinkingSound;
        [SerializeField] private AudioClip knockingSound;

        [Header("Friend dialogue — existing references from v0.1")]
        [SerializeField] private GameObject friendPanel;
        [SerializeField] private TMP_Text friendDialogueText;
        [SerializeField] private Button nextDialogueButton;

        [Header("End of current prototype — existing references from v0.1")]
        [SerializeField] private GameObject endPanel;
        [SerializeField] private TMP_Text endText;

        [Header("Timing and content — existing references from v0.1")]
        [SerializeField, Min(1)] private int startingVials = 5;
        [SerializeField, Min(0.5f)] private float hungerSeconds = 4f;
        [SerializeField, Min(0.2f)] private float pauseBetweenKnocks = 0.8f;

        [SerializeField, TextArea(2, 4)] private string[] memoryFragments =
        {
            "Cold stone... Where am I?",
            "A silver crest. A name I cannot quite recall...",
            "Fire in the halls. Steel striking steel.",
            "Someone was calling out to me. A voice I knew.",
            "Mourneveil... Why does that name hurt?"
        };

        [SerializeField, TextArea(2, 5)] private string[] friendDialogue =
        {
            "There you are. I was afraid you wouldn't wake.",
            "I left those bottles beside you. I knew you would be starving.",
            "You don't remember me, do you? We were friends, a long time ago.",
            "The hunters came for your family. The House of Mourneveil fell.",
            "You are its last heir. But the bloodline does not have to end here.",
            "The bottles were only enough to wake you. Come. We have to find more blood."
        };

        [Header("v0.2 — Optional visuals")]
        [Tooltip("Fullscreen BLACK UI Image. Put it above all other UI, disable Raycast Target, start with alpha 0 in Inspector.")]
        [SerializeField] private Image blackFadeOverlay;
        [Tooltip("Fullscreen PALE RED UI Image. Put it above the background but below text/buttons; Raycast Target OFF.")]
        [SerializeField] private Image memoryFlashOverlay;
        [Tooltip("Optional RectTransform to shake during door knocks (e.g. crypt background). Leave empty to disable shake.")]
        [SerializeField] private RectTransform shakeTarget;
        [SerializeField, Min(0f)] private float introFadeSeconds = 1.8f;
        [SerializeField, Min(0f)] private float memoryFlashSeconds = 0.32f;
        [SerializeField, Range(0f, 20f)] private float knockShakePixels = 9f;
        [Tooltip("Turn ON to avoid flashing, shaking and animated typing.")]
        [SerializeField] private bool reduceMotion = false;

        [Header("v0.2 — Optional dialogue typewriter")]
        [SerializeField] private bool typewriterEnabled = true;
        [SerializeField, Range(10f, 120f)] private float dialogueCharactersPerSecond = 42f;

        [Header("v0.2 — Optional heartbeat")]
        [Tooltip("Separate AudioSource used ONLY for heartbeat. Set Play On Awake OFF and Spatial Blend 0 (2D).")]
        [SerializeField] private AudioSource heartbeatSource;
        [SerializeField] private AudioClip heartbeatSound;

        private ProloguePhase phase;
        private int vialsRemaining;
        private int dialogueIndex;
        private bool typingDialogue;
        private Coroutine sequence;
        private Coroutine dialogueTyping;
        private Coroutine memoryFlash;
        private Coroutine openingFade;
        private Vector2 shakeOriginalPosition;

        private void Start()
        {
            if (drinkButton == null || storyText == null || vialsText == null ||
                friendPanel == null || friendDialogueText == null ||
                nextDialogueButton == null || endPanel == null)
            {
                Debug.LogError("[Mourneveil Prologue] Required UI references are missing in the Inspector.", this);
                enabled = false;
                return;
            }

            // The script attaches button listeners. Keep the buttons' On Click() lists empty in the Inspector.
            drinkButton.onClick.AddListener(DrinkFromVial);
            nextDialogueButton.onClick.AddListener(NextDialogue);

            if (shakeTarget != null)
                shakeOriginalPosition = shakeTarget.anchoredPosition;

            RestartPrologue();
        }

        private void OnDestroy()
        {
            if (drinkButton != null)
                drinkButton.onClick.RemoveListener(DrinkFromVial);
            if (nextDialogueButton != null)
                nextDialogueButton.onClick.RemoveListener(NextDialogue);
            StopHeartbeat();
        }

        public void DrinkFromVial()
        {
            if (phase != ProloguePhase.Drinking || vialsRemaining <= 0 || !drinkButton.interactable)
                return;

            vialsRemaining--;
            int consumedCount = Mathf.Max(1, startingVials) - vialsRemaining;

            if (memoryText != null && memoryFragments != null && memoryFragments.Length > 0)
            {
                int index = Mathf.Min(consumedCount - 1, memoryFragments.Length - 1);
                memoryText.text = memoryFragments[index];
            }

            PlaySound(drinkingSound);
            RefreshVials();

            if (memoryFlash != null)
                StopCoroutine(memoryFlash);
            SetImageAlpha(memoryFlashOverlay, 0f);
            if (!reduceMotion && memoryFlashOverlay != null)
                memoryFlash = StartCoroutine(FlashMemory());

            if (vialsRemaining == 0)
            {
                drinkButton.interactable = false;
                sequence = StartCoroutine(HungerAndKnocking());
            }
        }

        private IEnumerator OpeningFade()
        {
            drinkButton.interactable = false;
            if (blackFadeOverlay == null || reduceMotion || introFadeSeconds <= 0f)
            {
                SetImageAlpha(blackFadeOverlay, 0f);
                drinkButton.interactable = true;
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
            drinkButton.interactable = true;
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
                float strength = (1f - t) * 0.25f;
                SetImageAlpha(memoryFlashOverlay, strength);
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
                    new Vector2(Random.Range(-strength, strength), Random.Range(-strength, strength));
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
            if (friendDialogue == null || dialogueIndex >= friendDialogue.Length)
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

            if (friendDialogue == null || friendDialogue.Length == 0)
                friendDialogueText.text = "We must leave. The night is waiting.";
            else
                friendDialogueText.text = friendDialogue[Mathf.Clamp(dialogueIndex, 0, friendDialogue.Length - 1)];

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
                endText.text = "CHAPTER II — THE FIRST HUNT\n\n" +
                    "The last heir of Mourneveil must feed.\n" +
                    "To be continued...";
            }
        }

        private void RefreshVials()
        {
            vialsText.text = "Blood vials: " + vialsRemaining + " / " + Mathf.Max(1, startingVials);
            if (vialIcons == null) return;

            for (int i = 0; i < vialIcons.Length; i++)
            {
                if (vialIcons[i] != null)
                    vialIcons[i].SetActive(i < vialsRemaining);
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
            sequence = null;
            memoryFlash = null;
            openingFade = null;
            dialogueTyping = null;
            typingDialogue = false;
            StopHeartbeat();

            if (shakeTarget != null)
                shakeTarget.anchoredPosition = shakeOriginalPosition;

            phase = ProloguePhase.Drinking;
            vialsRemaining = Mathf.Max(1, startingVials);
            dialogueIndex = 0;
            if (introPanel != null) introPanel.SetActive(true);
            friendPanel.SetActive(false);
            endPanel.SetActive(false);
            friendDialogueText.maxVisibleCharacters = int.MaxValue;
            storyText.text = "The stone is cold. You open your eyes beside a coffin.\nThere are bottles filled with blood.";
            if (memoryText != null) memoryText.text = "Who am I...?";

            SetImageAlpha(hungerOverlay, 0f);
            SetImageAlpha(memoryFlashOverlay, 0f);
            RefreshVials();
            openingFade = StartCoroutine(OpeningFade());
        }
    }
}
