using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mourneveil
{
    /// <summary>
    /// Self-contained prototype of the intro to Mourneveil.
    /// The introductory blood vials are story objects, NOT the economy's Blood currency.
    /// Attach to a GameObject in the separate Crypt_Intro scene.
    /// No Editor scripts and no dependencies on MourneveilGame / MourneveilUI.
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

        [Header("Intro UI")]
        [SerializeField] private GameObject introPanel;
        [SerializeField] private TMP_Text storyText;
        [SerializeField] private TMP_Text memoryText;
        [SerializeField] private TMP_Text vialsText;
        [SerializeField] private Button drinkButton;
        [Tooltip("Optional: five separate vial images, ordered left to right.")]
        [SerializeField] private GameObject[] vialIcons;

        [Header("Atmosphere (optional)")]
        [Tooltip("Fullscreen red Image; Raycast Target should be unchecked.")]
        [SerializeField] private Image hungerOverlay;
        [SerializeField] private AudioSource soundSource;
        [SerializeField] private AudioClip drinkingSound;
        [SerializeField] private AudioClip knockingSound;

        [Header("Friend dialogue")]
        [SerializeField] private GameObject friendPanel;
        [SerializeField] private TMP_Text friendDialogueText;
        [SerializeField] private Button nextDialogueButton;

        [Header("End of current prototype")]
        [SerializeField] private GameObject endPanel;
        [SerializeField] private TMP_Text endText;

        [Header("Timing and content")]
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

        private ProloguePhase phase;
        private int vialsRemaining;
        private int dialogueIndex;
        private Coroutine sequence;

        private void Start()
        {
            if (drinkButton == null || storyText == null || vialsText == null ||
                friendPanel == null || friendDialogueText == null ||
                nextDialogueButton == null || endPanel == null)
            {
                Debug.LogError("[Mourneveil Prologue] Missing required UI references in the Inspector.", this);
                enabled = false;
                return;
            }

            // Buttons are wired here. Do NOT also add OnClick listeners in Inspector.
            drinkButton.onClick.AddListener(DrinkFromVial);
            nextDialogueButton.onClick.AddListener(NextDialogue);
            RestartPrologue();
        }

        private void OnDestroy()
        {
            if (drinkButton != null)
                drinkButton.onClick.RemoveListener(DrinkFromVial);
            if (nextDialogueButton != null)
                nextDialogueButton.onClick.RemoveListener(NextDialogue);
        }

        public void DrinkFromVial()
        {
            if (phase != ProloguePhase.Drinking || vialsRemaining <= 0)
                return;

            vialsRemaining--;
            int consumedCount = startingVials - vialsRemaining;

            if (memoryText != null && memoryFragments != null && memoryFragments.Length > 0)
            {
                int memoryIndex = Mathf.Min(consumedCount - 1, memoryFragments.Length - 1);
                memoryText.text = memoryFragments[memoryIndex];
            }

            PlaySound(drinkingSound);
            RefreshVials();

            if (vialsRemaining == 0)
            {
                drinkButton.interactable = false;
                sequence = StartCoroutine(HungerAndKnocking());
            }
        }

        private IEnumerator HungerAndKnocking()
        {
            phase = ProloguePhase.Hunger;
            storyText.text = "The last bottle is empty. The hunger is not.";

            float elapsed = 0f;
            while (elapsed < hungerSeconds)
            {
                elapsed += Time.deltaTime;
                float pulse = (Mathf.Sin(elapsed * 6f) + 1f) * 0.5f;
                SetOverlayAlpha(Mathf.Lerp(0.05f, 0.27f, pulse));
                yield return null;
            }

            phase = ProloguePhase.Knocking;
            storyText.text = "...Someone is at the door.";
            for (int i = 0; i < 3; i++)
            {
                storyText.text = i == 0 ? "BANG!" : "BANG! BANG!";
                SetOverlayAlpha(0.24f);
                PlaySound(knockingSound);
                yield return new WaitForSeconds(pauseBetweenKnocks * 0.35f);
                SetOverlayAlpha(0.03f);
                yield return new WaitForSeconds(pauseBetweenKnocks * 0.65f);
            }

            SetOverlayAlpha(0f);
            BeginFriendDialogue();
            sequence = null;
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
            if (friendDialogue == null || friendDialogue.Length == 0)
            {
                friendDialogueText.text = "We must leave. The night is waiting.";
                return;
            }

            friendDialogueText.text = friendDialogue[Mathf.Clamp(dialogueIndex, 0, friendDialogue.Length - 1)];
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
            vialsText.text = "Blood vials: " + vialsRemaining + " / " + startingVials;
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
                soundSource.PlayOneShot(clip);
        }

        private void SetOverlayAlpha(float alpha)
        {
            if (hungerOverlay == null) return;
            Color tint = hungerOverlay.color;
            tint.a = Mathf.Clamp01(alpha);
            hungerOverlay.color = tint;
        }

        [ContextMenu("DEV: Restart Prologue (Play Mode)")]
        public void RestartPrologue()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Mourneveil Prologue] Restart is available in Play Mode.", this);
                return;
            }

            if (sequence != null)
            {
                StopCoroutine(sequence);
                sequence = null;
            }

            phase = ProloguePhase.Drinking;
            vialsRemaining = Mathf.Max(1, startingVials);
            dialogueIndex = 0;
            if (introPanel != null) introPanel.SetActive(true);
            friendPanel.SetActive(false);
            endPanel.SetActive(false);
            drinkButton.interactable = true;
            storyText.text = "The stone is cold. You open your eyes beside a coffin.\nThere are bottles filled with blood.";
            if (memoryText != null) memoryText.text = "Who am I...?";
            SetOverlayAlpha(0f);
            RefreshVials();
        }
    }
}
