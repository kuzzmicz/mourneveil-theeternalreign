using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Park v0.5. Separate Park save, backwards-compatible with v0.1–v0.4.
public enum ParkFollowerRole { Hunter = 0, Spy = 1, Scholar = 2, Guardian = 3 }
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
    // v0.2 visual variant; old v0.1 saves default to 0 and remain readable.
    public int appearanceIndex = 0;
}

[Serializable]
public class ParkFollower
{
    // Legacy fields retained exactly: v0.1-v0.4 JSON still deserializes.
    public string title;
    public float bloodPerSecond;
    public int intelligence;
    public int stealth;

    // New v0.5 identity, traits, visual variant and assignment.
    public int id;
    public int appearanceIndex;
    public int willpower;
    public int fear;
    public int strength;
    public ParkFollowerRole role = ParkFollowerRole.Hunter;
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
    // v0.4: persisted with the active encounter; old v1 saves default to false.
    public bool currentEncounterAmbush = false;
    // v0.6: hypnotized confrontation has its own quiet-hunt bonus.
    public bool currentEncounterMesmerized = false;
    public List<ParkFollower> followers = new List<ParkFollower>();
    public int nextFollowerId = 1;
    public string lastMessage = "The park is quiet. Find someone in the shadows.";
    public long lastSavedUtcTicks = 0;
}

[DisallowMultipleComponent]
public class MourneveilParkGame : MonoBehaviour
{
    private const string SaveFile = "mourneveil_park_v1.json";
    private const float TurnSoulCost = 18f;
    private const int RoleTrainingBloodCost = 15;
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
    private bool needsLegacySaveBackup;

    public ParkProgress State { get { return state; } }
    public Sprite Background { get { return parkBackground; } }
    public float TurnCost { get { return TurnSoulCost; } }
    public bool HuntersAlerted { get { return state.suspicion >= 99.9f; } }
    public float LayLowCooldown { get { return layLowCooldownRemaining; } }
    public int RoleCost { get { return RoleTrainingBloodCost; } }

    // Scholars boost every source of Blood/sec. Boost capped at +100%.
    public float ScholarBonus
    {
        get
        {
            float boost = 0f;
            foreach (ParkFollower ally in FollowersSafe())
                if (ally.role == ParkFollowerRole.Scholar)
                    boost += 0.07f + Mathf.Clamp(ally.intelligence, 0, 100) / 1000f;
            return Mathf.Min(1f, boost);
        }
    }

    // Spies accelerate decay when Lucien is not engaged in an encounter.
    public float SpySuspicionDecay
    {
        get
        {
            float extra = 0f;
            foreach (ParkFollower ally in FollowersSafe())
                if (ally.role == ParkFollowerRole.Spy)
                    extra += 0.04f + Mathf.Clamp(ally.stealth, 0, 100) / 1600f;
            return Mathf.Min(1.2f, extra);
        }
    }

    // Guardians reduce immediate suspicion increases, up to 60% total.
    public float GuardianProtection
    {
        get
        {
            float protection = 0f;
            foreach (ParkFollower ally in FollowersSafe())
                if (ally.role == ParkFollowerRole.Guardian)
                    protection += 0.06f + Mathf.Clamp(ally.strength, 0, 100) / 2000f;
            return Mathf.Min(0.6f, protection);
        }
    }

    private IEnumerable<ParkFollower> FollowersSafe()
    {
        if (state == null || state.followers == null) yield break;
        foreach (ParkFollower ally in state.followers)
            if (ally != null) yield return ally;
    }

    public ParkFollower GetFollower(int id)
    {
        foreach (ParkFollower ally in FollowersSafe())
            if (ally.id == id) return ally;
        return null;
    }

    public int CountRole(ParkFollowerRole role)
    {
        int count = 0;
        foreach (ParkFollower ally in FollowersSafe())
            if (ally.role == role) count++;
        return count;
    }

    public bool CanAssignRole(int id, ParkFollowerRole role)
    {
        ParkFollower ally = GetFollower(id);
        return ally != null && ally.role != role &&
            state.currentHuman == null && state.blood >= RoleTrainingBloodCost &&
            (int)role >= 0 && (int)role <= 3;
    }

    public bool AssignFollowerRole(int id, ParkFollowerRole role)
    {
        if (!CanAssignRole(id, role)) return false;
        ParkFollower ally = GetFollower(id);
        state.blood -= RoleTrainingBloodCost;
        ally.role = role;
        state.lastMessage = ally.title + " has been assigned as " + role +
            ". Training cost: " + RoleTrainingBloodCost + " Blood.";
        CommitChange();
        return true;
    }

    public float BloodPerSecond
    {
        get
        {
            float value = networkBaseProduction * state.supplyNetworkLevel;
            foreach (ParkFollower ally in FollowersSafe())
                if (ally.role == ParkFollowerRole.Hunter)
                    value += Mathf.Max(0f, ally.bloodPerSecond);
            return value * (1f + ScholarBonus);
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
            int scholars = CountRole(ParkFollowerRole.Scholar);
            float research = Mathf.Min(12f, scholars * 2f);
            return Mathf.Clamp(82f - state.currentHuman.willpower * 0.65f +
                (state.currentEncounterAmbush ? 8f : 0f) +
                (state.currentEncounterMesmerized ? 6f : 0f) + research, 12f, 94f);
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
            state.suspicion = Mathf.Max(0f, state.suspicion -
                (naturalSuspicionDecreasePerSecond + SpySuspicionDecay) * dt);
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
        FindHuman(UnityEngine.Random.Range(0, 3));
    }

    // Backward compatible API for existing callers. Uses the normal (noticed) encounter.
    public void FindHuman(int appearanceIndex)
    {
        FindHuman(CreateHumanPreview(appearanceIndex), false);
    }

    // Generate a real character when they spawn, not when Lucien presses E.
    // Preview does NOT affect the saved state until the player begins an encounter.
    public ParkHuman CreateHumanPreview(int appearanceIndex)
    {
        int chosen = Mathf.Clamp(appearanceIndex, 0, 2);
        string[] names = chosen == 0
            ? new [] { "A tired student", "A young researcher", "A night-shift nurse" }
            : chosen == 1
                ? new [] { "A security guard", "A lost traveller" }
                : new [] { "A lone walker", "A late-night runner", "A solitary musician" };
        return new ParkHuman
        {
            appearanceIndex = chosen,
            title = names[UnityEngine.Random.Range(0, names.Length)],
            willpower = UnityEngine.Random.Range(15, 96),
            fear = UnityEngine.Random.Range(10, 96),
            strength = UnityEngine.Random.Range(15, 96),
            intelligence = UnityEngine.Random.Range(15, 96),
            stealth = UnityEngine.Random.Range(10, 96),
            bloodYield = UnityEngine.Random.Range(9, 17)
        };
    }

    // Called only after Lucien reaches this NPC. The exact same generated stats
    // are used by NPC AI and by the economy/turning calculations.
    public void FindHuman(ParkHuman person, bool undetected)
    {
        if (state.currentHuman != null || HuntersAlerted || person == null) return;
        state.currentHuman = person;
        state.currentEncounterAmbush = undetected;
        state.currentEncounterMesmerized = false;
        state.lastMessage = undetected
            ? "You approach unseen. A perfect opening: quiet feeding and turning are easier."
            : "You catch the stranger. They know something is wrong. Choose what Lucien will do.";
        CommitChange();
    }

    // v0.6: Successful confrontation begins the same hunting flow as pressing E.
    // Mesmerism is tracked separately so the UI does not call it a stealth ambush.
    public void BeginConfrontedHunt(ParkHuman person, string method, bool mesmerized)
    {
        if (person == null || state.currentHuman != null || HuntersAlerted) return;
        state.currentHuman = person;
        state.currentEncounterAmbush = false;
        state.currentEncounterMesmerized = mesmerized;
        state.lastMessage = mesmerized
            ? "Mesmerism succeeded. The stranger is dazed: quieter feeding and +6 turning chance."
            : method + " succeeded. The stranger is subdued. Choose what Lucien will do.";
        CommitChange();
    }

    // Spending soul is permanent even if the confrontation attempt fails.
    public bool SpendSoulForMesmerize(float cost)
    {
        if (state.currentHuman != null || state.soulIntegrity < cost) return false;
        state.soulIntegrity = Mathf.Max(0f, state.soulIntegrity - Mathf.Max(0f, cost));
        state.lastMessage = "Lucien uses Mesmerism (-" + cost.ToString("0") + " Soul Integrity).";
        CommitChange();
        return true;
    }

    public void ReportConfrontationFailure(ParkHuman person, string method, float penalty)
    {
        if (person == null || state.currentHuman != null) return;
        float before = state.suspicion;
        RaiseSuspicion(Mathf.Max(0f, penalty));
        state.lastMessage = method + " failed against " + person.title +
            ". They are fleeing! Suspicion +" + (state.suspicion - before).ToString("0.0") + ".";
        CommitChange();
    }

    public void ReportConfrontationBackOff(ParkHuman person)
    {
        if (person == null || state.currentHuman != null) return;
        state.lastMessage = "Lucien backs away from " + person.title +
            ". The encounter ends without further Suspicion.";
        CommitChange();
    }

    // Called once only when a fleeing NPC passes outside the masked park.
    // An escape creates a NEW random visitor after the respawn cooldown.
    public void ReportWitnessEscape(ParkHuman person, bool alarmRaised)
    {
        if (person == null || state.currentHuman != null) return;
        float before = state.suspicion;
        if (alarmRaised) RaiseSuspicion(2.5f + person.intelligence / 45f);
        state.lastMessage = alarmRaised
            ? person.title + " escaped from the park and may tell others. Suspicion +" +
              (state.suspicion - before).ToString("0.0") + "."
            : person.title + " left the park without raising further alarm.";
        CommitChange();
    }

    // One world detection = one global Suspicion penalty. NPC handles the cooldown.
    public void ReportDetection(ParkHuman witness)
    {
        if (state.currentHuman != null || HuntersAlerted || witness == null) return;
        float penalty = 4f + witness.intelligence / 25f + witness.fear / 45f;
        float before = state.suspicion;
        RaiseSuspicion(penalty);
        state.lastMessage = witness.title + " noticed Lucien! Suspicion +" +
            (state.suspicion - before).ToString("0.0") + ".";
        CommitChange();
    }

    public void TakeSmallAmount()
    {
        if (!CanAct()) return;
        ParkHuman target = state.currentHuman;
        bool ambush = state.currentEncounterAmbush;
        bool quiet = ambush || state.currentEncounterMesmerized;
        int gained = target.bloodYield + (ambush ? Mathf.CeilToInt(target.bloodYield * 0.25f) : 0);
        state.blood += gained;
        state.humanity = Mathf.Max(0f, state.humanity - 1f);
        RaiseSuspicion((5f + target.fear / 25f) * (quiet ? 0.5f : 1f));
        CompleteEncounter("You took a little blood (+" + gained + ")." +
            (ambush ? " Silent approach bonus applied."
                : quiet ? " Mesmerism kept the feeding discreet." : " They may remember your face."));
    }

    public void FeedAndEraseMemory()
    {
        if (!CanAct()) return;
        ParkHuman target = state.currentHuman;
        bool ambush = state.currentEncounterAmbush;
        bool quiet = ambush || state.currentEncounterMesmerized;
        int gained = Mathf.Max(3, target.bloodYield - 5) + (ambush ? 3 : 0);
        state.blood += gained;
        RaiseSuspicion(quiet ? 0.5f : 2f);
        CompleteEncounter("The human survives, confused. Their memories fade. Blood +" + gained + "." +
            (ambush ? " Silent approach bonus applied."
                : quiet ? " Mesmerism kept the feeding discreet." : ""));
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
            float production = (float)Math.Round(0.20f + (target.strength + target.stealth) / 180f, 2);
            if (state.followers == null) state.followers = new List<ParkFollower>();
            state.followers.Add(new ParkFollower
            {
                id = state.nextFollowerId++,
                title = target.title,
                bloodPerSecond = production,
                intelligence = target.intelligence,
                stealth = target.stealth,
                strength = target.strength,
                fear = target.fear,
                willpower = target.willpower,
                appearanceIndex = target.appearanceIndex,
                role = ParkFollowerRole.Hunter
            });
            RaiseSuspicion(state.currentEncounterAmbush || state.currentEncounterMesmerized ? 2.5f : 5f);
            CompleteEncounter("Turning succeeded! A new vampire joins your Dynasty as a Hunter (+" +
                production.ToString("0.00") + " Blood/sec). Open MANAGE DYNASTY to assign roles.");
        }
        else
        {
            RaiseSuspicion(state.currentEncounterAmbush || state.currentEncounterMesmerized ? 14f : 18f);
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
        needsLegacySaveBackup = false;
        try { if (File.Exists(SavePath)) File.Delete(SavePath); }
        catch (Exception e) { Debug.LogWarning("[Mourneveil Park] Could not delete save: " + e.Message); }
        Refresh();
        Debug.Log("[Mourneveil Park] DEV: Progress reset (next autosave will write fresh progress).");
    }

    [ContextMenu("DEV: Restore Soul Integrity")]
    private void RestoreSoulForTesting()
    {
        state.soulIntegrity = 100f;
        state.lastMessage = "DEV: Soul Integrity restored to 100.";
        CommitChange();
    }

    // Convenient inspector-only prototype commands; no in-game cheat UI.
    [ContextMenu("DEV: Add sample vampire")]
    private void RecruitSampleVampireForTesting()
    {
        if (state.followers == null) state.followers = new List<ParkFollower>();
        state.nextFollowerId = Mathf.Max(1, state.nextFollowerId);
        int id = state.nextFollowerId++;
        state.followers.Add(new ParkFollower
        {
            id = id,
            title = "Midnight volunteer " + id,
            bloodPerSecond = 0.65f,
            intelligence = 82,
            stealth = 74,
            strength = 63,
            fear = 29,
            willpower = 70,
            appearanceIndex = id % 3,
            role = ParkFollowerRole.Hunter
        });
        state.lastMessage = "DEV: Sample vampire #" + id + " added as Hunter.";
        CommitChange();
    }

    [ContextMenu("DEV: Add 500 Blood")]
    private void AddBloodForTesting()
    {
        state.blood += 500f;
        state.lastMessage = "DEV: Added 500 Blood for testing role assignments.";
        CommitChange();
    }

    private bool CanAct()
    {
        return state.currentHuman != null && !HuntersAlerted;
    }

    private void RaiseSuspicion(float value)
    {
        // Humanity under 35 has one immediate gameplay consequence: less control.
        if (state.humanity < 35f) value += 5f;
        // Guardians protect against immediate suspicion, spies handle passive decay.
        if (value > 0f) value *= (1f - GuardianProtection);
        state.suspicion = Mathf.Clamp(state.suspicion + value, 0f, 100f);
    }

    private void CompleteEncounter(string message)
    {
        state.currentHuman = null;
        state.currentEncounterAmbush = false;
        state.currentEncounterMesmerized = false;
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
                string savedJson = File.ReadAllText(SavePath);
                ParkProgress loaded = JsonUtility.FromJson<ParkProgress>(savedJson);
                if (loaded != null && loaded.version == 1)
                {
                    state = loaded;
                    needsLegacySaveBackup = !savedJson.Contains("\"nextFollowerId\"");
                }
            }
            if (state.followers == null) state.followers = new List<ParkFollower>();
            MigrateFollowers();
            if (state.currentHuman == null)
            {
                state.currentEncounterAmbush = false;
                state.currentEncounterMesmerized = false;
            }
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

    // JsonUtility leaves missing fields at defaults in older Park saves.
    // Assign unique ids and recover approximate traits for old, stat-light allies.
    // Never modify legacy bloodPerSecond: an old Hunter keeps their production.
    private void MigrateFollowers()
    {
        if (state.followers == null) state.followers = new List<ParkFollower>();
        int next = Mathf.Max(1, state.nextFollowerId);
        // Reserve all existing positive identities before assigning ids to legacy allies.
        foreach (ParkFollower existing in state.followers)
            if (existing != null && existing.id >= next)
                next = existing.id + 1;
        HashSet<int> used = new HashSet<int>();
        foreach (ParkFollower ally in state.followers)
        {
            if (ally == null) continue;
            if (ally.id <= 0 || used.Contains(ally.id))
            {
                while (used.Contains(next)) next++;
                ally.id = next++;
            }
            used.Add(ally.id);
            next = Math.Max(next, ally.id + 1);
            if (string.IsNullOrEmpty(ally.title)) ally.title = "Unknown vampire #" + ally.id;
            ally.intelligence = Mathf.Clamp(ally.intelligence, 0, 100);
            ally.stealth = Mathf.Clamp(ally.stealth, 0, 100);
            if (ally.strength <= 0) ally.strength = 25 + ally.stealth / 2;
            if (ally.willpower <= 0) ally.willpower = 25 + ally.intelligence / 2;
            if (ally.fear <= 0) ally.fear = 50;
            ally.strength = Mathf.Clamp(ally.strength, 1, 100);
            ally.willpower = Mathf.Clamp(ally.willpower, 1, 100);
            ally.fear = Mathf.Clamp(ally.fear, 1, 100);
            ally.appearanceIndex = Mathf.Clamp(ally.appearanceIndex, 0, 2);
            if ((int)ally.role < 0 || (int)ally.role > 3) ally.role = ParkFollowerRole.Hunter;
            if (ally.bloodPerSecond < 0f) ally.bloodPerSecond = 0f;
        }
        state.nextFollowerId = next;
    }

    private void SaveProgress()
    {
        try
        {
            // Make a one-time copy of pre-v0.5 progress before writing upgraded JSON.
            if (needsLegacySaveBackup && File.Exists(SavePath))
            {
                string backupPath = Path.Combine(Application.persistentDataPath,
                    "mourneveil_park_v1.pre_v05.bak");
                if (!File.Exists(backupPath)) File.Copy(SavePath, backupPath);
                needsLegacySaveBackup = false;
            }
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
