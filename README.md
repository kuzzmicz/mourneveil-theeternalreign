<div align="center">

# 🦇 MOURNEVEIL

### THE ETERNAL REIGN

**Blood remembers what time forgets.**

*A gothic vampire incremental horror game about rebuilding a fallen dynasty — and deciding what it should become.*

**Unity 6 · C# · 2D · PC (planned) · Indie project by a two-person team**

</div>

---

## 🩸 About the game

You awaken in an ancient crypt, weakened by a long slumber, with no memory of who you are. Beside your coffin are a handful of blood vials — and, beyond the sealed doors, the last person who remembers your past.

You are **Lucien Mourneveil**, the surviving heir of a vampire dynasty destroyed by hunters. To restore your family, you must gather blood, rebuild your estate, recruit allies, and uncover the truth behind its downfall.

But power comes at a cost. Every hunt can draw attention, every ruthless choice can erode your humanity, and creating new vampires demands a fragment of your own soul.

**Will you restore your family's ideals — or become the monster others expect you to be?**

## 🎮 Planned gameplay

| System | Design goal |
| --- | --- |
| **Incremental progression** | Collect blood, purchase upgrades, unlock passive production and grow a vampire dynasty. |
| **Meaningful hunting choices** | Balance resource gains against suspicion, your humanity, and the safety of your bloodline. |
| **Vampire servants** | Recruit vampires with different attributes to automate tasks and expand production. |
| **Soul-based prestige** | Sacrifice fragments of your soul to grow your dynasty, then enter a long slumber to begin a new cycle with lasting progression. |
| **Gothic horror** | Experience blood hunger, eerie discoveries, supernatural events, and changing character appearances. |
| **Narrative choices** | Uncover hidden history and shape Lucien's relationships and the future of House Mourneveil. |

> These features describe our **current design direction**, not a list of finished systems. Scope and mechanics may change as we prototype and playtest.

## 🛠️ Current development status

**Stage: early prototype / pre-alpha.** The project is under active development and is **not yet a complete playable game**.

### Implemented in the prototype

- [x] Basic crypt resource loop: blood gathering, upgrades, and passive production.
- [x] Early save/load functionality.
- [x] First crypt horror-event experiment.
- [x] Playable prologue foundation: blood vials, memory text, hunger effects, door knocking, and introductory dialogue.

### In progress / planned next

- [ ] Finish integrating individually clickable blood-vial sprites into the prologue UI.
- [ ] Polish character portraits, backgrounds, sound design, and accessibility options.
- [ ] Build the first park encounter and hunting-choice prototype.
- [ ] Introduce Humanity and Suspicion to the playable systems.
- [ ] Prototype soul-based transformation, servant progression, and prestige.

> The features above refer to the prototype state reported during development; some content and UI elements are still being iterated on.

## 🕯️ Art direction

Mourneveil combines gothic architecture, crimson stained glass, candlelit crypts, dark burgundy palettes, and ornate aristocratic UI. We are exploring stylized character sprites and pixel-art-inspired dialogue portraits while refining a consistent visual direction.

<!-- Add an actual screenshot once a finished, in-game capture is available.
     Example: ![Crypt prototype](Docs/Images/crypt-screenshot.png)
-->

## ⚙️ Tech stack

- **Game engine:** Unity 6 (2D)
- **Programming language:** C#
- **UI:** Unity UI / TextMeshPro
- **Version control:** Git & GitHub
- **Development approach:** modular systems and data-driven content as the project grows

## 📁 Repository structure

The project currently uses a standard Unity repository layout:

```text
Assets/           Game scenes, scripts, art, audio and other assets
Packages/         Unity package manifest and dependencies
ProjectSettings/  Unity project configuration
Docs/             Public-facing technical or design documentation
```

Unity-generated cache folders such as `Library/`, `Temp/`, and `Logs/` are excluded from version control.

## ▶️ Opening the project

1. Install a compatible **Unity 6** editor version through **Unity Hub**.
2. Clone the repository:

   ```bash
   git clone https://github.com/kuzzmicz/mourneveil-theeternalreign.git
   ```
3. In Unity Hub, select **Add project from disk** and choose the cloned repository folder.
4. Let Unity import the project. Open the scene you want to test under `Assets/Scenes/` and enter Play Mode.

> The repository may change between prototype iterations. This is a development project, not a stable release. For reproducible testing, prefer the Unity editor version recorded in `ProjectSettings/ProjectVersion.txt`.

## 🗺️ Roadmap

- [x] Define the game's premise and primary characters.
- [x] Build the first incremental crypt prototype.
- [x] Implement the initial cinematic prologue sequence.
- [ ] Polish the prologue interaction and presentation.
- [ ] Create the park/hunting vertical slice.
- [ ] Introduce morality, risk, and servant progression systems.
- [ ] Develop the soul-based rebirth loop.
- [ ] Expand the castle and narrative into a playable demo.

## 👥 Team

Mourneveil is an independent passion project developed by **two creators** combining an interest in incremental game systems, programming, atmospheric horror, and gothic storytelling.

We use this repository to document our progress and showcase the engineering and design behind the game.

## 📜 Copyright and third-party assets

**Copyright © 2026 the respective Mourneveil contributors. All rights reserved.**

This public repository is provided for project visibility and portfolio purposes. **No general open-source license is granted** for the original code, art, music, story, or other game content unless a specific file states otherwise. Please contact the relevant copyright holders before reusing or redistributing original materials.

Third-party libraries and assets remain governed by their own licenses. All included assets must have appropriate usage rights.

---

<div align="center">

*The dynasty is gone. Its legacy is not.* 🦇

</div>
