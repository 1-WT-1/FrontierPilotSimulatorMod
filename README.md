# Frontier Pilot Simulator mod by xv25ddd continued

**English** | [Русский](README.ru.md)

This repository contains game configurations and BepInEx plugins for Frontier Pilot Simulator.

> [!IMPORTANT]
> **Always backup your original Native directory** (`Frontier Pilot Simulator_Data/StreamingAssets/Descriptions/Native`) before modifying game files.

---

## Requirements and Installation

To run these mods, you need BepInEx 5 installed in your game.

### 1. Install BepInEx 5

1. Download BepInEx 5 from the BepInEx releases page:
   <https://github.com/BepInEx/BepInEx/releases>
2. Extract the archive directly into your game's root directory (where `Frontier Pilot Simulator.exe` is located).
3. Run the game once to let BepInEx initialize its folders, then close the game.

### 2. Install

Download the latest release zip files from the [Releases page](https://github.com/1-WT-1/FrontierPilotSimulatorMod/releases):

* **Native JSON Overrides (`Native-Overrides.zip`)**: Extract this archive directly into your game's root directory (where `Frontier Pilot Simulator.exe` is located) to install the Native JSON config overrides.
* **BepInEx C# Plugins (`BepInEx-Plugins.zip`)**: Extract this archive directly into your game's root directory to install the BepInEx C# plugins

---

## BepInEx C# Plugins

* **Armor Bridge**: Makes custom armor upgrades functional.
* **Compass**: Adds numerical heading indicators.
* **Damage Logger**: Development tool that logs component damage.
* **Headlights**: Adds manual headlight control. Press 'L' to cycle modes. Key can be changed in BepInEx config.
* **Probabilistic Malfunction**: Changes the malfunction mechanic from time-based to chance-based.
* **Production Economy**: Processes custom factory recipes.
* **VTOL Airbrake**: Enables the airbrake toggle key in VTOL mode.

---

## Native Configuration Overrides

Modified game configuration files for economy balance, ship specifications, and custom items.

### Economy & Price Balance

* **10x Cost Adjustments**: Upgrade and ship prices are increased by **10x** across the board.
* **Ship Prices (Including default gear)**:
  * **Scarab**: **330,000**
  * **Ox**: **2,136,000**
  * **Ballena**: **3,000,000**

### Ship Balance & Tuning

* **Cargo Capacity**:
  * **Scarab**: Max cargo count increased from `1` to `2`
  * **Ox**: Max cargo count increased from `2` to `4`
  * **Ballena**: Max cargo count increased from `2` to `6`, and cargo capacity increased from `15,000` to `20,000`

### New Upgrades

28 upgrades added to ships:

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
* **Fuel Systems (FSS)**: Expands fuel capacity.
  * `FSS Extended`, `FSS High-Capacity`, `FSS Integrated`

### New Tradeables

Custom commercial commodities added to the economy:

* **Industrial & Household Goods**: `Industrial Chemicals`, `Laboratory Chemicals`, `Household Chemicals`, `Industrial Filters`, and `Clothes` (bought and sold at regional bases).
* **Consumables & Power Cells**: Consumables (`B3 rations`, `B4 rations`) and battery units (`G-type-2 batteries`, `G-type-3 batteries`) integrated into local trade lanes.

### Test Base (bases_test_base)

Staging base open by default:

* **Location**: Coordinate `[-94170.0, 130.0, 90383.0]`.
* **Inventory**: Sells all 28 custom upgrades for all three ships.
