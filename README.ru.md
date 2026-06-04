# Модификации для Frontier Pilot Simulator (продолжение разработки xv25ddd)

[English](README.md) | **Русский**

В этом репозитории собраны конфигурационные файлы и плагины BepInEx для Frontier Pilot Simulator.

> [!IMPORTANT]
> **Перед установкой обязательно сделайте резервную копию** оригинальной папки Native (`Frontier Pilot Simulator_Data/StreamingAssets/Descriptions/Native`).

---

## Требования и установка

Для работы плагинов BepInEx требуется установленный BepInEx 5.

### 1. Установка BepInEx 5

1. Скачайте BepInEx 5 со страницы релизов BepInEx:
   <https://github.com/BepInEx/BepInEx/releases>
2. Распакуйте архив в корневую папку игры (туда, где лежит `Frontier Pilot Simulator.exe`).
3. Запустите игру один раз, чтобы BepInEx создал структуру папок, после чего закройте её.

### 2. Установка

Скачайте архивы последней версии со [страницы релизов (Releases)](https://github.com/1-WT-1/FrontierPilotSimulatorMod/releases).

Мод разделён на несколько независимых пакетов — устанавливайте только то, что нужно:

| Пакет | Содержимое | Обязателен |
|---|---|---|
| `FPS_Core.zip` | ModSettingsCore + Локализация | Да, если используете любой другой пакет |
| `FPS_QoL.zip` | Плагины для улучшения пилотирования | Нет |
| `FPS_Overhaul.zip` | Ребаланс экономики, кораблей и система поломок | Нет |
| `FPS_UnknownOverrides.zip` | Правки vanilla, происхождение которых не проверено (Camera/Targets/Triggers) | Нет — экспериментально |

Распакуйте каждый выбранный архив в корневую папку игры. Если устанавливаете и QoL, и Overhaul — сначала QoL.

---

## QoL (`FPS_QoL.zip`)

* **OxEngineFix**: Исправляет смещение двигателя Ox на нестандартных крыльях.
* **Compass**: Добавляет числовой индикатор курса на HUD.
* **Headlights**: Ручное управление фарами. Клавиша `L`, цикличное переключение режимов.
* **Keep Cruise**: Сохраняет фиксированную тягу при входе в ангары и меню торговли.
* **VTOL Airbrake**: Позволяет использовать воздушный тормоз в режиме VTOL.
* **STOL Mode**: Векторизация мотогондол для STOL в режиме самолета. Клавиша `G` для переключения.
* **Crosswind Crab**: Выравнивает шасси по вектору скорости при посадке с боковым ветром. Клавиша `K` для переключения.
* **VelocityCamera**: Добавляет переключатель, чтобы камера отслеживала вектор скорости или направление носа во время игры.
* **Approaches**: Консольные команды для загрузки глиссадных маршрутов захода на посадку (`SetApproach`) и установки пользовательских 3D путевых точек (`SetWaypoint`).

> Все клавиши можно изменить в меню настроек модов прямо в игре.

---

## Overhaul (`FPS_Overhaul.zip`)

### Экономический баланс

* **Повышение цен в 10 раз**: Стоимость кораблей и оборудования увеличена десятикратно.
* **Цены на корабли (с учетом стандартного оборудования)**:
  * **Scarab**: **330 000**
  * **Ox**: **2 136 000**
  * **Ballena**: **3 000 000**

### Характеристики кораблей

* **Грузоподъемность**:
  * **Scarab**: вместимость контейнеров `1` → `2`
  * **Ox**: вместимость контейнеров `2` → `4`
  * **Ballena**: вместимость контейнеров `2` → `6`, грузоподъемность `15 000` → `20 000`

### Новые улучшения

28 новых улучшений для кораблей:

* **Active Flow Control (AFC)**: Снижает аэродинамическое сопротивление.
  * `AFC Synthetic Jet Actuators`, `AFC Compressed Air Injection`, `AFC Helium Flow Actuators`, `AFC Plasma Flow Actuators`
* **Active Overload Suppression (AOS)**: Стабилизирует груз при резких маневрах, предотвращая повреждения от перегрузок.
  * `AOS Passive Damper`, `AOS Inductive Stabilizer`, `AOS Inertia Bubble`, `AOS Gravitational Alignment`
* **Container Compression Systems (CCS)**: Увеличивает объем грузового отсека и количество слотов.
  * `CCS Field Compression Module`, `CCS Spatial Compression Module`, `CCS Mass Reorganization Module`
* **High-Performance Coatings (HPC)**: Увеличивает прочность корпуса и защиту от небесной радиации.
  * `HPC Thermal`, `HPC Ceramic`, `HPC Nanocomposite`
* **Fuel Cooling Systems (FCS)**: Криогенная система охлаждения топлива, увеличивающая его плотность и запас.
  * `FCS Evaporative Cooling`, `FCS Cryocoolers`, `FCS Liquid Nitrogen/Hydrogen/Helium`
* **Fuel Injectors (FIS)**: Повышает тягу двигателей за счет увеличения расхода топлива.
  * `FIS Standard`, `FIS High-Pressure`, `FIS Plasma`
* **Fuel Pumps (FPM)**: Снижает расход топлива двигателями.
  * `FPM Mechanical`, `FPM Electric`, `FPM Turbine`
* **Fuel Systems (FSS)**: Увеличивает объем топливных баков.
  * `FSS Extended`, `FSS High-Capacity`, `FSS Integrated`

### Новые товары

* **Промышленные и бытовые товары**: `Industrial Chemicals`, `Laboratory Chemicals`, `Household Chemicals`, `Industrial Filters`, `Clothes`
* **Припасы и батареи**: `B3 rations`, `B4 rations`, `G-type-2 batteries`, `G-type-3 batteries`

### Поломки и урон

* **Armor Bridge**: Активирует кастомные покрытия брони в системе урона.
* **Probabilistic Malfunction**: Заменяет таймер поломок на шанс случайного отказа.
* **Production Economy**: Добавляет поддержку кастомных производственных рецептов.

### Тестовая база

База доступна с самого начала:

* **Координаты**: `[-94170.0, 130.0, 90383.0]`
* **Ассортимент**: все 28 новых улучшений для всех кораблей.
