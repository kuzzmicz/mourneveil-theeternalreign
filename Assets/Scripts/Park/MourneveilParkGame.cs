using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Park v0.1. Standalone prototype: does not modify the Crypt / Prologue save data.
[Serializable]
public class ParkHuman
{
    public string title;
    public int willpower;
    public int fear;
    public int strength;
    public int intelligence;
    public int stealth;
    public int bloodYield;
}

[Serializable]
public class ParkFollower
{
    public string title;
    public float bloodPerSecond;
    public int intelligence;
    public int stealth;
}

[Serializable]
public class ParkProgress
{
    public int version = 1;
    public float blood = 0f;
    public float humanity = 100f;
    public float soulIntegrity = 100f;
    public float suspicion = 0f;
    public int supplyNetworkLevel = 0;
    public int encountersResolved = 0;
    public ParkHuman currentHuman;
    public List<ParkFollower> followers = new List<ParkFollower>();
    public string lastMessage = "The park is quiet. Find someone in the shadows.";
    public long lastSavedUtcTicks = 0;
}

[DisallowMultipleComponent]
public class MourneveilParkGame : MonoBehaviour
{
    private const string SaveFile = "mourneveil_park_v1.json";
    private const float TurnSoulCost = 18f;
    private static readonly string[] People =
    {
        "A lone walker", "A night-shift nurse", "A tired student",
        "A security guard", "A lost traveller", "A late-night runner",
        "A young researcher", "A solitary musician"
    };

    [Header("Optional background (can be added later)")]
    [SerializeField] private Sprite parkBackground;

    [Header("Park balance - prototype values")]
    [SerializeField, Min(0f)] private float networkBaseProduction = 0.35f;
    [SerializeField, Min(1f)] private float networkUpgradeBaseCost = 30f;
    [SerializeField, Min(0f)] private float naturalSuspicionDecreasePerSecond = 0.12f;
    [SerializeField] private bool enableOfflineBlood = true;
    [SerializeField, Range(0f, 4f)] private float offlineHoursCap = 2f;

    private ParkProgress state = new ParkProgress();
    private MourneveilParkUI ui;
    private float autosaveTimer;
    private float refreshTimer;
    private float layLowCooldownRemaining;

    public ParkProgress State { get { return state; } }
    public Sprite Background { get { return parkBackground; } }
    public float TurnCost { get { return TurnSoulCost; } }
    public bool HuntersAlerted { get { return state.suspicion >= 99.9f; } }
    public float LayLowCooldown { get { return layLowCooldownRemaining; } }

    public float BloodPerSecond
    {
        get
        {
            float value = networkBaseProduction * state.supplyNetworkLevel;
            if (state.followers != null)
            {
                foreach (ParkFollower follower in state.followers)
                    if (follower != null) value += follower.bloodPerSecond;
            }
            return value;
        }
    }

    public int NetworkUpgradeCost
    {
        get
        {
            float cost = networkUpgradeBaseCost * Mathf.Pow(1.75f, state.supplyNetworkLevel);
            return Mathf.CeilToInt(Mathf.Min(cost, 100000000f));
        }
    }

    public float TurningChance
    {
        get
        {
            if (state.currentHuman == null) return 0f;
            // Strong willpower makes turning harder. Other abilities are independent.
            return Mathf.Clamp(82f - state.currentHuman.willpower * 0.65f, 12f, 80f);
        }
    }

    private string SavePath { get { return Path.Combine(Application.persistentDataPath, SaveFile); } }

    private void Awake()
    {
        LoadProgress();
    }

    private void Start()
    {
        ui = GetComponent<MourneveilParkUI>();
        if (ui == null) ui = gameObject.AddComponent<MourneveilParkUI>();
        ui.Build(this);
        Refresh();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        float production = BloodPerSecond;
        if (production > 0f) state.blood += production * dt;
        if (state.currentHuman == null && state.suspicion > 0f)
            state.suspicion = Mathf.Max(0f, state.suspicion - naturalSuspicionDecreasePerSecond * dt);
        layLowCooldownRemaining = Mathf.Max(0f, layLowCooldownRemaining - dt);
        autosaveTimer += dt;
        if (autosaveTimer >= 15f)
        {
            autosaveTimer = 0f;
            SaveProgress();
        }
        refreshTimer += dt;
        if (refreshTimer >= 0.1f)
        {
            refreshTimer = 0f;
            Refresh();
        }
    }

    public void FindHuman()
    {
        if (state.currentHuman != null || HuntersAlerted) return;
        state.currentHuman = new ParkHuman
        {
            title = People[UnityEngine.Random.Range(0, People.Length)],
            willpower = UnityEngine.Random.Range(15, 96),
            fear = UnityEngine.Random.Range(10, 96),
            strength = UnityEngine.Random.Range(15, 96),
            intelligence = UnityEngine.Random.Range(15, 96),
            stealth = UnityEngine.Random.Range(10, 96),
            bloodYield = UnityEngine.Random.Range(9, 17)
        };
        state.lastMessage = "You sense a heartbeat nearby. Choose what Lucien will do.";
        CommitChange();
    }

    public void TakeSmallAmount()
    {
        if (!CanAct()) return;
        ParkHuman target = state.currentHuman;
        int gained = target.bloodYield;
        state.blood += gained;
        state.humanity = Mathf.Max(0f, state.humanity - 1f);
        RaiseSuspicion(5f + target.fear / 25f);
        CompleteEncounter("You took a little blood (+" + gained + "). They will recover, but may remember your face.");
    }

    public void FeedAndEraseMemory()
    {
        if (!CanAct()) return;
        ParkHuman target = state.currentHuman;
        int gained = Mathf.Max(3, target.bloodYield - 5);
        state.blood += gained;
        RaiseSuspicion(2f);
        CompleteEncounter("The human survives, confused. Their memories fade. Blood +" + gained + ".");
    }

    public void DrainHuman()
    {
        if (!CanAct()) return;
        ParkHuman target = state.currentHuman;
        int gained = target.bloodYield * 3;
        state.blood += gained;
        state.humanity = Mathf.Max(0f, state.humanity - 15f);
        RaiseSuspicion(22f);
        CompleteEncounter("The human did not survive. Blood +" + gained + "; Humanity -15. Someone may investigate.");
    }

    public void AttemptTurning()
    {
        if (!CanAct() || state.soulIntegrity < TurnSoulCost) return;
        ParkHuman target = state.currentHuman;
        float chance = TurningChance;
        state.soulIntegrity = Mathf.Max(0f, state.soulIntegrity - TurnSoulCost);
        state.humanity = Mathf.Max(0f, state.humanity - 4f);
        bool success = UnityEngine.Random.Range(0f, 100f) < chance;
        if (success)
        {
            // Prototype simplification: a successful turning joins the faction.
            // Recruitment/consent/loyalty are separate mechanics in the full GDD.
            float production = (float)Math.Round(0.20f + (target.intelligence + target.stealth) / 180f, 2);
            if (state.followers == null) state.followers = new List<ParkFollower>();
            state.followers.Add(new ParkFollower
            {
                title = target.title,
                bloodPerSecond = production,
                intelligence = target.intelligence,
                stealth = target.stealth
            });
            RaiseSuspicion(5f);
            CompleteEncounter("Turning succeeded! A new ally produces +" + production.ToString("0.00") + " Blood/sec.");
        }
        else
        {
            RaiseSuspicion(18f);
            CompleteEncounter("Turning failed. Your soul weakened (-18), and rumours spread through the park.");
        }
    }

    public void LetGo()
    {
        if (!CanAct()) return;
        CompleteEncounter("You let the stranger go. For one night, your hunger does not decide for you.");
    }

    public void BuySupplyNetwork()
    {
        int cost = NetworkUpgradeCost;
        if (state.blood < cost) return;
        state.blood -= cost;
        state.supplyNetworkLevel++;
        state.lastMessage = "Your contacts secure another quiet supply route. Passive Blood production increased.";
        CommitChange();
    }

    public void LayLow()
    {
        if (layLowCooldownRemaining > 0f || state.suspicion <= 0f || state.currentHuman != null) return;
        state.suspicion = Mathf.Max(0f, state.suspicion - 25f);
        layLowCooldownRemaining = 12f; // Session-only cooldown; not a save mechanic.
        state.lastMessage = "You disappear into the fog. Hunters lose track of the trail (-25 Suspicion).";
        CommitChange();
    }

    public void SaveNow()
    {
        state.lastMessage = "Progress saved. The night will remember.";
        SaveProgress();
        Refresh();
    }

    [ContextMenu("DEV: Reset Park prototype save")]
    private void ResetParkForTesting()
    {
        state = new ParkProgress();
        layLowCooldownRemaining = 0f;
        try { if (File.Exists(SavePath)) File.Delete(SavePath); }
        catch (Exception e) { Debug.LogWarning("[Mourneveil Park] Could not delete save: " + e.Message); }
        Refresh();
        Debug.Log("[Mourneveil Park] DEV: Progress reset (next autosave will write fresh progress).");
    }

    private bool CanAct()
    {
        return state.currentHuman != null && !HuntersAlerted;
    }

    private void RaiseSuspicion(float value)
    {
        // Humanity under 35 has one immediate gameplay consequence: less control.
        if (state.humanity < 35f) value += 5f;
        state.suspicion = Mathf.Clamp(state.suspicion + value, 0f, 100f);
    }

    private void CompleteEncounter(string message)
    {
        state.currentHuman = null;
        state.encountersResolved++;
        state.lastMessage = message;
        if (HuntersAlerted)
            state.lastMessage += " HUNTER ALERT: Hide before hunting again!";
        CommitChange();
    }

    private void CommitChange()
    {
        SaveProgress();
        Refresh();
    }

    private void Refresh()
    {
        if (ui != null) ui.Refresh(this);
    }

    private void LoadProgress()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                ParkProgress loaded = JsonUtility.FromJson<ParkProgress>(File.ReadAllText(SavePath));
                if (loaded != null && loaded.version == 1)
                    state = loaded;
            }
            if (state.followers == null) state.followers = new List<ParkFollower>();
            state.humanity = Mathf.Clamp(state.humanity, 0f, 100f);
            state.soulIntegrity = Mathf.Clamp(state.soulIntegrity, 0f, 100f);
            state.suspicion = Mathf.Clamp(state.suspicion, 0f, 100f);
            state.blood = Mathf.Max(0f, state.blood);
            if (enableOfflineBlood && state.lastSavedUtcTicks > 0 && BloodPerSecond > 0f)
            {
                long difference = DateTime.UtcNow.Ticks - state.lastSavedUtcTicks;
                if (difference > 0)
                {
                    double offlineSeconds = Math.Min(TimeSpan.FromTicks(difference).TotalSeconds, offlineHoursCap * 3600d);
                    float gained = (float)(offlineSeconds * BloodPerSecond);
                    if (gained >= 0.1f)
                    {
                        state.blood += gained;
                        state.lastMessage = "While you were away, your allies gathered " + gained.ToString("0.0") + " Blood.";
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Mourneveil Park] Save could not be loaded: " + e.Message);
            state = new ParkProgress();
        }
    }

    private void SaveProgress()
    {
        try
        {
            state.lastSavedUtcTicks = DateTime.UtcNow.Ticks;
            string json = JsonUtility.ToJson(state, true);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Mourneveil Park] Save failed: " + e.Message);
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveProgress();
    }

    private void OnApplicationQuit()
    {
        SaveProgress();
    }
}
