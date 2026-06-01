# Модификации для Frontier Pilot Simulator (продолжение разработки xv25ddd)

[English](README.md) | **Русский**

В этом репозитории собраны конфигурационные файлы и плагины BepInEx для Frontier Pilot Simulator.

> [!IMPORTANT]
> **Перед установкой обязательно сделайте резервную копию** оригинальной папки Native (`Frontier Pilot Simulator_Data/StreamingAssets/Descriptions/Native`).

---

## Требования и установка

Для работы модификаций требуется установленный BepInEx 5.

### 1. Установка BepInEx 5

1. Скачайте BepInEx 5 со страницы релизов BepInEx:
   <https://github.com/BepInEx/BepInEx/releases>
2. Распакуйте архив в корневую папку игры (туда, где лежит `Frontier Pilot Simulator.exe`).
3. Запустите игру один раз, чтобы BepInEx создал структуру папок, после чего закройте её.

### 2. Установка мода

Скачайте архивы последней версии со [страницы релизов (Releases)](https://github.com/1-WT-1/FrontierPilotSimulatorMod/releases):

* **Конфигурации JSON (`Native-Overrides.zip`)**: Распакуйте архив в корневую папку игры, чтобы заменить стандартные конфигурации в папке `Native`.
* **C# плагины BepInEx (`BepInEx-Plugins.zip`)**: Распакуйте архив в корневую папку игры для установки плагинов.

---

## C# плагины BepInEx

* **Armor Bridge**: Активирует работу кастомных улучшений брони.
* **Compass**: Добавляет числовые индикаторы курса.
* **Damage Logger**: Инструмент разработчика для логирования входящего урона по компонентам.
* **Headlights**: Добавляет возможность ручного управления фарами. Клавиша по умолчанию — 'L', можно изменить в конфиге BepInEx.
* **Approaches**: Добавляет консольные команды (`SetApproach` для загрузки маршрутов глиссады из JSON и `SetWaypoint` для установки пользовательских 3D-точек маршрута).
* **Probabilistic Malfunction**: Меняет механику поломок оборудования с таймера на процентный шанс возникновения.
* **Production Economy**: Добавляет поддержку новых производственных рецептов на фабриках.
* **VTOL Airbrake**: Позволяет использовать воздушный тормоз клавишей-переключателем в режиме VTOL.

---

## Конфигурационные файлы Native

Измененные параметры игры, отвечающие за экономический баланс, характеристики кораблей и новые предметы.

### Экономический баланс

* **Повышение цен в 10 раз**: Стоимость кораблей и оборудования увеличена десятикратно.
* **Цены на корабли (с учетом стандартного оборудования)**:
  * **Scarab**: **330 000**
  * **Ox**: **2 136 000**
  * **Ballena**: **3 000 000**

### Баланс и характеристики кораблей

* **Грузоподъемность**:
  * **Scarab**: вместимость контейнеров увеличена с `1` до `2`.
  * **Ox**: вместимость контейнеров увеличена с `2` до `4`.
  * **Ballena**: вместимость контейнеров увеличена с `2` до `6`, а грузоподъемность — с `15 000` до `20 000`.

### Новые улучшения

Добавлены 28 новых улучшений для кораблей:

* **Active Flow Control (AFC)**: Снижает аэродинамическое сопротивление корпуса.
  * `AFC Synthetic Jet Actuators`, `AFC Compressed Air Injection`, `AFC Helium Flow Actuators`, `AFC Plasma Flow Actuators`
* **Active Overload Suppression (AOS)**: Стабилизирует груз при резких маневрах, предотвращая повреждения от перегрузок.
  * `AOS Passive Damper`, `AOS Inductive Stabilizer`, `AOS Inertia Bubble`, `AOS Gravitational Alignment`
* **Container Compression Systems (CCS)**: Увеличивает максимальный объем груза и количество слотов под контейнеры.
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

Новые коммерческие товары, интегрированные в экономику игры:

* **Промышленные и бытовые товары**: промышленная, лабораторная и бытовая химия (`Industrial Chemicals`, `Laboratory Chemicals`, `Household Chemicals`), промышленные фильтры (`Industrial Filters`) и одежда (`Clothes`). Доступны для покупки и продажи на региональных базах.
* **Припасы и батареи**: сухпайки (`B3 rations`, `B4 rations`) и аккумуляторы (`G-type-2 batteries`, `G-type-3 batteries`), добавленные в торговые маршруты.

### Тестовая база (bases_test_base)

База доступна для быстрого старта и тестирования оборудования:

* **Координаты**: `[-94170.0, 130.0, 90383.0]`.
* **Ассортимент**: в продаже присутствуют все 28 новых улучшений и все три корабля.
