<div align="center">

# 🦇 MOURNEVEIL
### THE ETERNAL REIGN

**Blood remembers what time forgets.**

*A gothic vampire incremental horror game about rebuilding a fallen dynasty — and deciding what it should become.*

**Unity 6 · C# · 2D · PC (planned) · Two-person indie project**

**Development status: Pre-alpha / playable prototypes**

</div>

---

## 🩸 About the game

You awaken in an ancient crypt, weakened by a long slumber, with no memory of who you are. Five blood vials wait beside your coffin. Beyond the doors stands someone who remembers what happened before you fell asleep.

You are **Lucien Mourneveil**, a surviving heir of a vampire dynasty destroyed by hunters. With the help of **Rowan**, you begin uncovering the past while rebuilding your bloodline.

Hunt to survive. Gather blood and establish a network of servants. Decide whether to spare your victims, erase their memories, or turn them into vampires. Every decision has consequences: witnesses raise **Suspicion**, cruelty erodes **Humanity**, and creating new vampires consumes **Soul Integrity**.

**Will you restore your family's ideals — or become the monster others expect you to be?**

> **Project scope:** Mourneveil is an actively developed **pre-alpha** project. It contains playable systems and prototype scenes in the Unity Editor, but it is **not a finished game or a stable public release**. Details may change through playtesting.

## 🎮 Gameplay at a glance

The current prototype combines a resource-management loop with direct character control and choice-driven encounters:

1. **Awaken in the crypt** — experience the introductory story, drink the blood vials, recover fragments of memory, and meet Rowan.
2. **Enter the park** — control Lucien with the keyboard, approach passersby, and decide whether to sneak or run.
3. **Read the situation** — NPCs have different attributes and awareness levels; they may become suspicious, flee, or confront you.
4. **Choose the outcome** — feed, erase memories, drain, attempt a transformation, or let someone go.
5. **Grow the dynasty** — assign newly turned vampires to jobs that generate resources or support the bloodline.
6. **Manage the consequences** — balance Blood production against Humanity, Soul Integrity, and Suspicion.

## 🛠️ Current development status

### Implemented in the prototype

**Crypt and narrative prologue**

- [x] Initial incremental resource loop with Blood, upgrades, and passive production.
- [x] Basic persistence / save and load.
- [x] An early horror-event experiment in the crypt.
- [x] Playable prologue with five interactive blood vials, memory sequences, hunger effects, knocking at the door, and character dialogue.
- [x] Portrait-based dialogue interface and transition into the first hunt.

**Park — interactive hunting**

- [x] Keyboard-controlled Lucien: movement, running, and sneaking.
- [x] Proximity-based hunting interactions rather than hunting from anywhere on the map.
- [x] Passersby with individual attributes affecting detection and encounters.
- [x] Awareness / suspicion reactions and bonuses for approaching unseen.
- [x] NPC behaviours including wandering, fleeing, and confronting.
- [x] Fleeing NPCs leaving the playable area and being replaced after a delay.
- [x] Confrontation choices: **Overpower, Intimidate, Mesmerize, Back Off**.
- [x] Hunting result panels that remain visible until the player continues.
- [x] Blood, Humanity, Soul Integrity, and Suspicion integrated into the park prototype.

**Dynasty and incremental systems**

- [x] Vampire transformation attempts and recruitment of individual servants.
- [x] Dynasty-management UI with servant attributes and role assignment.
- [x] Four servant roles: **Hunter**, **Spy**, **Scholar**, and **Guardian**.
- [x] Passive Blood generation and upgradeable supply routes.
- [x] Prototype persistence for park progression and servants.
- [x] Developer tools for testing and resetting prototype progress.

**Art and animation**

- [x] Gothic crypt and moonlit park backgrounds, plus character portraits.
- [x] A consistent, intentionally restrained **96 × 144 px pixel-art** direction for park characters.
- [x] Separate walking, running, and sneaking animation frames for Lucien.
- [x] Walking and fleeing animation frames for park NPCs.
- [x] Visual feedback for awareness and confrontation.

### In progress / planned

- [ ] Further polish the prologue, sound, readability, and accessibility.
- [ ] Improve park depth, animation transitions, and interface consistency.
- [ ] Add hostile vampire hunters and more advanced encounter behaviours.
- [ ] Expand the dynasty with progression, distinct talents, and additional strategic choices.
- [ ] Implement the **soul-based rebirth / prestige** loop and lasting progression.
- [ ] Develop estate restoration, additional locations, and deeper narrative branches.
- [ ] Balance the economy, refine save handling, and prepare a cohesive playable demo.

> Checked items indicate features available in the **prototype**, not necessarily production-ready systems.

## 🎮 Controls — Park prototype

| Input | Action |
| --- | --- |
| `W` `A` `S` `D` / arrow keys | Move Lucien |
| `Shift` + movement | Run |
| `Ctrl` + movement | Sneak |
| `E` near an NPC | Start a hunt or confrontation |
| Mouse | Select targets, choose actions, and manage the dynasty |

Controls and interactions may change as the game evolves.

## 👑 The vampire dynasty

Turning someone is more than increasing a servant counter. Recruited vampires retain individual attributes and can be assigned different jobs:

| Role | Prototype function |
| --- | --- |
| **Hunter** | Produces Blood passively; effectiveness depends on relevant attributes. |
| **Spy** | Helps reduce Suspicion over time. |
| **Scholar** | Supports overall Blood production and transformation chances. |
| **Guardian** | Helps limit Suspicion generated by dangerous actions. |

Changing roles comes with a resource cost. Deciding *who* to turn and *where* to assign them is intended to become a core strategic part of the incremental loop.

## 🕯️ Art direction

Mourneveil combines **gothic architecture, crimson stained glass, candlelit crypts, moonlit parks, dark burgundy palettes, and aristocratic motifs**.

The game uses different visual treatments for different purposes:

- **Narrative scenes:** atmospheric illustrated backgrounds and character portraits.
- **Park gameplay:** deliberately readable, less detailed retro pixel-art characters, with **96 × 144 px** animation frames.
- **UI:** functional prototype interfaces being refined toward a coherent gothic style.

<!-- Add a real in-game capture to the repository before enabling this image.
![Mourneveil park gameplay](Docs/Images/park-gameplay.png)
-->

## ⚙️ Tech stack

| Area | Technology / approach |
| --- | --- |
| Engine | **Unity 6**, 2D |
| Language | **C#** |
| Interface | **Unity UI (Canvas)** and **TextMeshPro** |
| Game logic | Resource progression, encounter choices, and state-driven NPC behaviour |
| Animation | Frame-based pixel-art sprites controlled by runtime scripts |
| Persistence | Prototype save/load systems |
| Version control | **Git & GitHub** |
| Target platform | **PC** (planned) |

The park currently uses a **Canvas-based prototype implementation**. Its systems, visuals, and architecture may be refactored as development progresses.

## 📁 Repository structure

```text
Assets/
  Art/                Backgrounds, portraits, and other game art
    ParkAnimations/   Park character animation PNGs
  Scenes/             Unity scenes (including prototype gameplay scenes)
  Scripts/
    Park/             Park gameplay, NPC behaviour, UI, and animation scripts
  ...                 Additional Unity assets and scripts
Packages/             Unity packages and dependencies
ProjectSettings/      Unity project configuration
Docs/                 Project documentation (where available)
```

Unity-generated folders such as `Library/`, `Temp/`, `Logs/`, and `Obj/` are excluded from Git.

## ▶️ Opening the project

1. Install a compatible **Unity 6** version using **Unity Hub**. For the exact editor version, check [`ProjectSettings/ProjectVersion.txt`](ProjectSettings/ProjectVersion.txt).
2. Clone this repository:

   ```bash
   git clone https://github.com/kuzzmicz/mourneveil-theeternalreign.git
   ```

3. In Unity Hub, choose **Add project from disk** and select the cloned folder.
4. Allow Unity to import all packages and assets.
5. Open a prototype scene from `Assets/Scenes/` and enter **Play Mode**.

> This repository is a development workspace, not a packaged game. Some scenes and editor-only debug tools are intended for internal testing, and compatibility may vary between commits.

## 🗺️ Roadmap

- [x] Establish the premise, Lucien, Rowan, and the first narrative sequence.
- [x] Build the initial crypt incremental prototype.
- [x] Create the interactive crypt prologue.
- [x] Build the first playable park/hunting vertical slice.
- [x] Add movement, stealth, NPC awareness, fleeing, and confrontation.
- [x] Prototype Humanity, Suspicion, Soul Integrity, and a multi-role vampire dynasty.
- [x] Integrate park walking, running, sneaking, and fleeing sprites.
- [ ] Polish presentation, UI, audio, and moment-to-moment feedback.
- [ ] Introduce vampire-hunter threats and richer NPC interactions.
- [ ] Expand servant progression and the strategic incremental economy.
- [ ] Implement soul-based rebirth and persistent unlocks.
- [ ] Build estate progression and additional story chapters.
- [ ] Produce a tested, downloadable demo.

## 👥 Team

Mourneveil is an independent passion project developed by **two creators** with interests in game development, programming, gothic storytelling, and incremental systems.

This repository documents our experiments and progress and serves as a portfolio showcase of the game's design and implementation.

## 📜 Copyright and third-party assets

**Copyright © 2026 the respective Mourneveil contributors. All rights reserved.**

This public repository is available for viewing and project/portfolio visibility. **No general open-source license is granted** for original code, art, audio, writing, or other content unless a specific file explicitly states otherwise. Please contact the relevant rights holders before reusing or redistributing those materials.

Third-party packages and assets remain subject to their respective terms and licenses. Prototype assets should be reviewed for appropriate usage and attribution before any public release.

---

<div align="center">

*Blood remembers what time forgets.* 🦇

</div>

*The dynasty is gone. Its legacy is not.* 🦇

</div>
