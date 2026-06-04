# Frontier Pilot Simulator mod by xv25ddd continued

**English** | [Русский](README.ru.md)

This repository contains game configurations and BepInEx plugins for Frontier Pilot Simulator.

> [!IMPORTANT]
> **Always backup your original Native directory** (`Frontier Pilot Simulator_Data/StreamingAssets/Descriptions/Native`) before modifying game files.

---

## Requirements and Installation

To run the BepInEx mods you need BepInEx 5 installed.

### 1. Install BepInEx 5

1. Download BepInEx 5 from the BepInEx releases page:
   <https://github.com/BepInEx/BepInEx/releases>
2. Extract the archive directly into your game's root directory (where `Frontier Pilot Simulator.exe` is located).
3. Run the game once to let BepInEx initialize its folders, then close the game.

### 2. Install

Download the release zip files from the [Releases page](https://github.com/1-WT-1/FrontierPilotSimulatorMod/releases).

There are four separate packs — install what you want:

| Pack | Contents | Required |
|---|---|---|
| `FPS_Core.zip` | ModSettingsCore + Localization | Yes, if using any other pack |
| `FPS_QoL.zip` | Flight assist plugins | No |
| `FPS_Overhaul.zip` | Economy/ship rebalance + damage mods | No |
| `FPS_UnknownOverrides.zip` | Unverified vanilla edits (Camera/Targets/Triggers) | No — experimental |

Extract each zip directly into the game's root directory. If you install both QoL and Overhaul, install QoL first.

---

## QoL (`FPS_QoL.zip`)

* **OxEngineFix**: Fixes Ox engine offset on non-default wings.
* **Compass**: Adds a numerical heading indicator to the HUD.
* **Headlights**: Manual headlight control. Press `L` to cycle modes.
* **Keep Cruise**: Persists fixed thrust across hangars and trade menus.
* **VTOL Airbrake**: Enables the airbrake toggle in VTOL.
* **STOL Mode**: Engine nacelle vectoring for STOL in plane mode. Press `G` to toggle.
* **Crosswind Crab**: Aligns landing gear with velocity vector during crosswind landings. Press `K` to toggle.
* **VelocityCamera**: Adds a toggle to change whether the camera tracks the velocity vector or the nose direction while playing.
* **Approaches**: Console commands to load glideslope approach routes (`SetApproach`) and set custom 3D waypoints (`SetWaypoint`).

> All keys can be changed in the mod settings menu in-game.

---

## Overhaul (`FPS_Overhaul.zip`)

### Price Balance

* **10x Cost Adjustments**: Upgrade and ship prices are increased by **10x** across the board.
* **Ship Prices (including default gear)**:
  * **Scarab**: **330,000**
  * **Ox**: **2,136,000**
  * **Ballena**: **3,000,000**

### Ship Tuning

* **Cargo Capacity**:
  * **Scarab**: Max cargo count `1` → `2`
  * **Ox**: Max cargo count `2` → `4`
  * **Ballena**: Max cargo count `2` → `6`, cargo capacity `15,000` → `20,000`

### New Upgrades

28 upgrades added across all ships:

* **Active Flow Control (AFC)**: Reduces aerodynamic drag.
  * `AFC Synthetic Jet Actuators`, `AFC Compressed Air Injection`, `AFC Helium Flow Actuators`, `AFC Plasma Flow Actuators`
* **Active Overload Suppression (AOS)**: Dampens cargo momentum shifts to prevent overload damage.
  * `AOS Passive Damper`, `AOS Inductive Stabilizer`, `AOS Inertia Bubble`, `AOS Gravitational Alignment`
* **Container Compression Systems (CCS)**: Increases cargo capacity and max cargo count.
  * `CCS Field Compression Module`, `CCS Spatial Compression Module`, `CCS Mass Reorganization Module`
* **High-Performance Coatings (HPC)**: Increases hull HP and sky radiation resistance.
  * `HPC Thermal`, `HPC Ceramic`, `HPC Nanocomposite`
* **Fuel Cooling Systems (FCS)**: Cryogenic cooling that increases fuel capacity.
  * `FCS Evaporative Cooling`, `FCS Cryocoolers`, `FCS Liquid Nitrogen/Hydrogen/Helium`
* **Fuel Injectors (FIS)**: Boosts engine thrust at the cost of higher fuel consumption.
  * `FIS Standard`, `FIS High-Pressure`, `FIS Plasma`
* **Fuel Pumps (FPM)**: Reduces engine fuel consumption.
  * `FPM Mechanical`, `FPM Electric`, `FPM Turbine`
* **Fuel Systems (FSS)**: Expands fuel tank capacity.
  * `FSS Extended`, `FSS High-Capacity`, `FSS Integrated`

### New Tradeables

* **Industrial & Household Goods**: `Industrial Chemicals`, `Laboratory Chemicals`, `Household Chemicals`, `Industrial Filters`, `Clothes`
* **Consumables & Power Cells**: `B3 rations`, `B4 rations`, `G-type-2 batteries`, `G-type-3 batteries`

### Damage & Malfunctions

* **Armor Bridge**: Makes custom armor coatings functional against damage.
* **Probabilistic Malfunction**: Changes malfunctions from time-based to chance-based.
* **Production Economy**: Processes custom factory production recipes.

### Test Base

Staging base available from the start:

* **Location**: `[-94170.0, 130.0, 90383.0]`
* **Inventory**: Sells all 28 custom upgrades for all three ships.
