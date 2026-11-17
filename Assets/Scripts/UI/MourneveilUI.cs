using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mourneveil
{
    public class MourneveilUI : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private MourneveilGame game;

        [Header("Counters - TextMeshPro")]
        [SerializeField] private TMP_Text bloodText;
        [SerializeField] private TMP_Text productionText;
        [SerializeField] private TMP_Text awakeningText;
        [SerializeField] private TMP_Text stageText;

        [Header("Buttons")]
        [SerializeField] private Button gatherButton;
        [SerializeField] private Button clickUpgradeButton;
        [SerializeField] private Button servantUpgradeButton;
        [SerializeField] private Button saveButton;

        [Header("Upgrade button labels - TextMeshPro")]
        [SerializeField] private TMP_Text clickUpgradeLabel;
        [SerializeField] private TMP_Text servantUpgradeLabel;

        [Header("Horror event panel")]
        [SerializeField] private GameObject eventPanel;
        [SerializeField] private TMP_Text eventText;
        [SerializeField] private Button dismissEventButton;

        private float refreshTimer;
        private bool listenersRegistered;

        private void Start()
        {
            if (game == null)
                game = FindFirstObjectByType<MourneveilGame>();

            if (game == null || gatherButton == null || clickUpgradeButton == null ||
                servantUpgradeButton == null || saveButton == null || dismissEventButton == null)
            {
                Debug.LogError("[Mourneveil] Assign the game and every button in MourneveilUI Inspector.");
                enabled = false;
                return;
            }

            gatherButton.onClick.AddListener(game.GatherBlood);
            clickUpgradeButton.onClick.AddListener(OnClickUpgrade);
            servantUpgradeButton.onClick.AddListener(OnBuyServant);
            saveButton.onClick.AddListener(OnSave);
            dismissEventButton.onClick.AddListener(DismissEvent);
            game.HorrorEvent += ShowHorrorEvent;
            listenersRegistered = true;

            if (eventPanel != null)
                eventPanel.SetActive(false);

            Refresh();
        }

        private void Update()
        {
            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer >= 0.15f)
            {
                Refresh();
                refreshTimer = 0f;
            }
        }

        private void OnDestroy()
        {
            if (!listenersRegistered || game == null)
                return;

            gatherButton.onClick.RemoveListener(game.GatherBlood);
            clickUpgradeButton.onClick.RemoveListener(OnClickUpgrade);
            servantUpgradeButton.onClick.RemoveListener(OnBuyServant);
            saveButton.onClick.RemoveListener(OnSave);
            dismissEventButton.onClick.RemoveListener(DismissEvent);
            game.HorrorEvent -= ShowHorrorEvent;
        }

        private void Refresh()
        {
            if (game == null)
                return;

            if (bloodText != null) bloodText.text = $"Blood: {game.Blood:0.0}";
            if (productionText != null) productionText.text = $"Blood/sec: {game.BloodPerSecond:0.0}";
            if (awakeningText != null) awakeningText.text = $"Awakening: {game.Awakening:0}%";
            if (stageText != null) stageText.text = $"Castle: {game.AwakeningStage}";

            if (clickUpgradeLabel != null)
                clickUpgradeLabel.text = $"Stronger Bite Lv. {game.ClickUpgradeLevel}\nCost: {game.ClickUpgradeCost:0} Blood";

            if (servantUpgradeLabel != null)
                servantUpgradeLabel.text = $"Restless Servant Lv. {game.ServantLevel}\nCost: {game.ServantCost:0} Blood";

            clickUpgradeButton.interactable = game.Blood >= game.ClickUpgradeCost;
            servantUpgradeButton.interactable = game.Blood >= game.ServantCost;
        }

        private void OnClickUpgrade()
        {
            game.TryBuyClickUpgrade();
            Refresh();
        }

        private void OnBuyServant()
        {
            game.TryBuyServant();
            Refresh();
        }

        private void OnSave()
        {
            game.SaveGame();
            Debug.Log($"[Mourneveil] Saved to: {SaveSystem.SavePath}");
        }

        private void ShowHorrorEvent(string message)
        {
            if (eventText != null) eventText.text = message;
            if (eventPanel != null) eventPanel.SetActive(true);
        }

        private void DismissEvent()
        {
            if (eventPanel != null) eventPanel.SetActive(false);
        }
    }
}
