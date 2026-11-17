using System;
using UnityEngine;

namespace Mourneveil
{
   
    public class MourneveilGame : MonoBehaviour
    {
        [SerializeField, Min(5f)] private float autosaveInterval = 30f;

        private GameState state;
        private float autosaveTimer;

        public event Action<string> HorrorEvent;

        public double Blood => state.blood;
        public int ClickUpgradeLevel => state.clickUpgradeLevel;
        public int ServantLevel => state.servantLevel;
        public float Awakening => state.awakening;

        public double BloodPerClick => 1.0 + state.clickUpgradeLevel;
        public double BloodPerSecond => state.servantLevel * 0.5;

        public double ClickUpgradeCost => Math.Ceiling(10.0 * Math.Pow(1.6, state.clickUpgradeLevel));
        public double ServantCost => Math.Ceiling(15.0 * Math.Pow(1.7, state.servantLevel));

        public string AwakeningStage
        {
            get
            {
                if (state.awakening < 15f) return "Dormant";
                if (state.awakening < 35f) return "Stirring";
                if (state.awakening < 70f) return "Whispering";
                return "Awake";
            }
        }

        private void Awake()
        {
            state = SaveSystem.Load();
            state.blood = Math.Max(0.0, state.blood);
            state.clickUpgradeLevel = Math.Max(0, state.clickUpgradeLevel);
            state.servantLevel = Math.Max(0, state.servantLevel);
            state.awakening = Mathf.Clamp(state.awakening, 0f, 100f);
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;

            if (BloodPerSecond > 0)
            {
                state.blood += BloodPerSecond * deltaTime;
              
                AddAwakening((float)(Math.Min(BloodPerSecond, 1000.0) * 0.012 * deltaTime));
            }

            autosaveTimer += deltaTime;
            if (autosaveTimer >= autosaveInterval)
            {
                SaveGame();
                autosaveTimer = 0f;
            }
        }

        public void GatherBlood()
        {
            state.blood += BloodPerClick;
            AddAwakening(0.30f);
        }

        public bool TryBuyClickUpgrade()
        {
            double cost = ClickUpgradeCost;
            if (state.blood < cost)
                return false;

            state.blood -= cost;
            state.clickUpgradeLevel++;
            AddAwakening(3f);
            return true;
        }

        public bool TryBuyServant()
        {
            double cost = ServantCost;
            if (state.blood < cost)
                return false;

            state.blood -= cost;
            state.servantLevel++;
            AddAwakening(6f);
            return true;
        }

        private void AddAwakening(float amount)
        {
            state.awakening = Mathf.Clamp(state.awakening + amount, 0f, 100f);

            if (!state.firstWhisperSeen && state.awakening >= 35f)
            {
                state.firstWhisperSeen = true;
                HorrorEvent?.Invoke("The walls whisper your name.\n\nYou are not alone in the crypt.");
                SaveGame();
            }
        }

        public void SaveGame()
        {
            SaveSystem.Save(state);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && state != null)
                SaveGame();
        }

        private void OnApplicationQuit()
        {
            if (state != null)
                SaveGame();
        }

#if UNITY_EDITOR
        [ContextMenu("DEV: Reset prototype progress (deletes save)")]
        private void ResetProgressForTesting()
        {
            SaveSystem.DeleteSaveForTesting();
            state = new GameState();
            autosaveTimer = 0f;
            Debug.Log("[Mourneveil] Progress reset for testing.");
        }
#endif
    }
}
