# GameFrameWorkPackage

GameFrameWorkPackage is a reusable Unity mobile-game framework for casual, puzzle, idle, merge, tap/drag, and hyper-casual projects. Shared services live in `NebulaSoft Core`; each title adds its own gameplay rules, content, UI, and scenes under `Project Files` without changing the framework layer.

This repository currently targets **Unity 6000.3.12f1** and package version **1.2.2**. Its enabled build scenes are:

1. `Assets/Project Files/Game/Scenes/Init.unity`
2. `Assets/Project Files/Game/Scenes/Menu.unity`
3. `Assets/Project Files/Game/Scenes/Game.unity`

> The bundled reference game demonstrates one integration of the framework. Reuse the core services and add a focused game module for each new title; it is not necessary to retain the reference game's rules or content.

## Quick start

1. Clone the repository and open it in Unity Hub with Unity **6000.3.12f1**.
2. Let Unity resolve the dependencies declared in `Packages/manifest.json`.
3. Open `Assets/Project Files/Game/Scenes/Init.unity` and press Play. `Init` is the bootstrap scene; do not start a normal play session from `Menu` or `Game` unless the initializer has already run.
4. Use the EditMode tests in `Assets/Tests/Editor` from **Window → General → Test Runner** to validate the local setup.

### Optional service integrations

Firebase, Facebook login, advertising, and in-app purchases are optional product integrations. Before shipping a new game, configure the corresponding provider accounts, platform identifiers, consent flow, store products, and platform configuration files for *your own* application. Do not commit credentials, signing keys, API secrets, or production configuration copied from another app.

Firebase-dependent code is conditionally compiled with `FIREBASE`; Facebook support is guarded by `FACEBOOK`. The monetization layer also uses feature symbols such as `MODULE_IAP`, `MODULE_ADMOB`, `MODULE_ADSKIT`, and `UNITY_IAP_NEW`. A feature should be enabled only after its package and platform setup are complete.

## Project layout

```text
.
├── Assets/
│   ├── NebulaSoft Core/        # Reusable framework services and editor tooling
│   ├── Project Files/          # Reference-game and future-title data, gameplay, UI, art, and scenes
│   │   ├── Data/               # ScriptableObject databases and game configuration
│   │   └── Game/
│   │       ├── Scenes/         # Init, Menu, Game, and Level Editor scenes
│   │       ├── Scripts/        # Game-specific runtime and editor code
│   │       ├── Prefabs/        # UI, blocks, effects, power-ups, environments
│   │       └── Animations|Audio|Images|Models|Materials|Textures|Shaders/
│   ├── Addon/                  # Add-on gameplay/UI content and third-party packages
│   ├── Firebase|FacebookSDK|GoogleMobileAds|GooglePlayGames|LevelPlay/
│   │                           # Provider SDKs and integration assets
│   ├── Plugins/                # Native and third-party plugins
│   └── Tests/Editor/           # EditMode regression tests
├── Packages/                   # Unity Package Manager manifest and lock file
├── ProjectSettings/            # Unity project and build settings
├── docs/images/                # README diagrams and screenshots
└── tools/design/               # Local Figma bridge/export utilities
```

### Ownership boundaries

| Area | Owns | Change here when |
| --- | --- | --- |
| `Assets/NebulaSoft Core` | Generic initialization, persistence, UI, economy, monetization, tweening, and utilities | Improving a reusable capability without coupling it to a particular game's rules |
| `Assets/Project Files` | Reference-game content plus title-specific game flow, rules, presentation, and services | Adding or changing gameplay, content, UI, balancing, or progression for a title |
| SDK/vendor folders | Imported provider SDKs and third-party packages | Updating an integration according to the provider's migration guide; avoid direct feature edits |
| `Packages` and `ProjectSettings` | Dependency versions, editor/build/platform configuration | Upgrading Unity/packages or configuring a new app target |

Do not place new gameplay rules in imported SDK folders or modify third-party source to implement product features.

## Application lifecycle

### Bootstrap

`Init.unity` owns the persistent initialization sequence. `GameLoading` calls `Initializer.Init()`, then initializes registered modules and SDK behaviors/tasks. `Initializer` is kept alive across scene loads, binds the overlay and event system, initializes analytics/static modules, and then delegates to `ProjectInitSettings` and `SDKInitializer`.

```mermaid
flowchart LR
    A[Init.unity / GameLoading] --> B[Initializer.Init]
    B --> C[Persistent Initializer\nEventSystem, Overlay, analytics]
    C --> D[Project Init Settings]
    D --> E[Core InitModules\nSave, Tween, Audio, Currency, Haptic]
    D --> F[Game InitModules\nGame Data, Lives, Quest, Daily Reward, Dev Panel]
    E --> G[SDKInitializer]
    F --> G
    G --> H[SDK behaviors and loading tasks\nconsent, monetization, optional Firebase preload]
    H --> I[Menu.unity]
```

`Project Init Settings.asset` contains the registered `InitModule` instances. Each module derives from `InitModule` and creates or initializes one service. Modules should be independent of a particular level or scene whenever possible.

### Module registration and service initialization

![Project Init Settings Inspector](docs/images/project-init-settings.png)

The Inspector above is the project's service registry. It serializes an ordered `InitModule[]` inside `Project Init Settings.asset`; it is **not** a dependency-injection container. A module owns its serialized configuration, and its `CreateComponent()` method initializes the corresponding static manager/service or persistent runtime component.

```mermaid
flowchart LR
    A[InitModule class] -->|RegisterModule attribute| B[Project Init Settings custom Inspector]
    B -->|Serialized as ordered subasset| C[Project Init Settings.asset]
    D[GameLoading] --> E[Initializer.InitModules]
    E --> F[ProjectInitSettings.Init]
    F -->|For each module in list order| G[InitModule.CreateComponent]
    G --> H[Static service Init or persistent component]
    H --> I[Scene controllers and UI use the service]
```

At editor time, `[RegisterModule("Name", core, order)]` makes an `InitModule` discoverable by the custom `Project Init Settings` inspector. Modules marked `core: true` are required when a settings asset is created; their `order` determines creation priority. Non-core modules are optional entries added from the Inspector's **Add Module** menu, with one instance of each module type per settings asset.

At runtime, `GameLoading` calls `Initializer.Init()`, checks connectivity, then calls `Initializer.InitModules()`. `ProjectInitSettings.Init()` walks the serialized list in its displayed order and invokes `CreateComponent()` on every non-null module. After that, static modules and global music are initialized; `SDKInitializer` then initializes SDK behaviors and queued loading tasks before the next scene loads.

The modules shown in the screenshot currently register services as follows:

| Inspector module | Serialized configuration | Runtime registration/effect |
| --- | --- | --- |
| **Save Controller** | Autosave delay, clear-on-start option, WebGL key prefix | Calls `SaveController.Init(...)`; this must precede services that load player state |
| **Tween** | Custom easing functions and update-pool capacities | Adds the persistent `Tween` component to `Initializer.GameObject`, initializes it, then registers easing functions |
| **Audio Controller** | Audio clip library, pool size, and 3D audio defaults | Applies 3D settings and calls `AudioController.Init(...)` |
| **Currencies** | `CurrencyDatabase` | Calls `CurrencyController.Init(database)` for balances, currency definitions, and reward integration |
| **Haptic** | Optional verbose logging | Enables logging when requested, then calls `Haptic.Init()` to select the platform wrapper |
| **Dev Panel** | `DevPanelSettings` | Validates settings and links them through `DevPanelEnabler.LinkSettings(...)` |
| **Game Settings** | `GameData` | Calls `GameData.Init()`, exposes global defaults, and applies relevant remote-config overrides |
| **Lives System** | `LivesData` | Validates data and calls `LivesSystem.Init(data)`; it reads persisted life state from the save service |
| **Screen Settings** | Target frame rate and sleep-timeout options | Applies `Application.targetFrameRate` and `Screen.sleepTimeout` |
| **Quest** | `QuestDatabase` | Calls `QuestService.Init(database)` |
| **Daily Reward** | `DailyRewardDatabase` | Calls `DailyRewardService.Init(database)` after the save service is ready |

To add a framework or game service, create a `ScriptableObject` derived from `InitModule`, add `[RegisterModule("Feature Name", core: false)]`, serialize its settings, and call the service's initialization method from `CreateComponent()`. Add the module in `Project Init Settings.asset`, place it after every dependency it requires, and add an EditMode test for initialization and save/remote-config edge cases. Use `core: true` only when every project created from the framework requires the module.

### Menu-to-game runtime flow

`MenuController` prepares menu UI and common controllers. When the player starts, it validates/locks a life and loads `Game.unity`. `GameController` initializes scene services and delegates level construction to `LevelController`. The level controller loads data, accepts input through `RaycastController`, coordinates movement and effects, and resolves success or failure.

```mermaid
flowchart LR
    A[MenuController] -->|Play: validate and lock life| B[GameController]
    B --> C[LevelController]
    C --> D[Level data + LevelRepresentation]
    D --> E[Input / RaycastController]
    E --> F[Block movement, effects, interactables, power-ups]
    F -->|All target images complete| G[Complete]
    F -->|Timer or failure state| H[Game over / revive]
    G --> I[Save progress, rewards, UI, leaderboard event, ads]
    H --> J[UI, life handling, optional revive]
    I --> A
    J --> A
```

## Reusable core modules

The core is organized under `Assets/NebulaSoft Core/Modules`. The table describes responsibility and the normal integration boundary; it does not require every feature to be active in every build.

| Module | Responsibility | Primary entry/configuration | Boundary |
| --- | --- | --- | --- |
| **Initializer** | Persistent bootstrap, registered modules, loading tasks, consent and SDK behavior orchestration | `Initializer`, `GameLoading`, `Project Init Settings.asset`, `SDKInitializer` | Starts services; should not contain title-specific rules |
| **Save** | Local save-object creation, serialization, autosave, global and named saves | `SaveInitModule`, `SaveController`, `SavePresets` | Feature services own their save models; this module owns storage lifecycle |
| **UI** | Page/popup navigation, overlay, safe area, button audio/haptics feedback | `UIController`, `UIPage`, popup interfaces | UI pages display state; game rules remain in controllers/services |
| **Tween** | Time-based animations, callbacks, coroutines, reusable tween behaviors | `TweenInitModule`, `Tween`, `TweenCase` | Presentation/timing helper; it must not become gameplay state storage |
| **Audio** | Audio clips, music sources, sound playback, audio save state | `AudioInitModule`, `AudioController`, `MusicSource` | Central playback service; content references belong to game assets |
| **Currency** | Currency definitions, balances, remote overrides, rewards and UI views | `CurrencyInitModule`, `CurrencyController`, `CurrencyDatabase` | General economy primitives; game features request grants/spends through it |
| **Reward** | Extensible reward definitions, holders, previews and application helpers | `Reward`, `RewardsHolder`, registration attributes | Compose rewards for game features without hard-coding UI or store logic |
| **Monetization** | Ads, rewarded ads, IAP products, entitlement state, privacy/consent integration | `MonetizationSettings`, `AdsManager`, `IAPManager` | Provider adapters are isolated behind framework managers and compile symbols |
| **Haptic** | Cross-platform haptic patterns, settings, priority and native wrappers | `HapticInitModule`, `Haptic` | Gameplay/UI choose feedback intent; platform wrappers implement it |
| **Pool** | Reusable object pools and scene holders | `PoolManager`, `Pool`, `PoolGeneric` | Owns object reuse, not game-object behavior rules |
| **Analytics** | Analytics module registration and event dispatch integration | `AnalyticsModules`, `AnalyticsController` | Provider/event plumbing; event semantics are defined by the game feature |
| **Defines** | Attributes and tooling for conditional compile definitions | `DefineAttribute` and project symbols | Controls feature compilation; not a runtime dependency container |
| **Skins** | Shared skin-selection extension point used by game data | `SkinController` and game-specific skin databases | The template's concrete level skins live under `Project Files/Data/Skins` |
| **Inspector** | Custom inspector styles and editor presentation support | Core inspector assemblies/settings | Editor-only tooling; no runtime gameplay dependency |

## Reference-game modules

The current reference game is under `Assets/Project Files/Game/Scripts`. Its modules demonstrate how a title composes core services; a new game can replace or extend this layer without changing `NebulaSoft Core`.

| Group | Main types and assets | How it works |
| --- | --- | --- |
| **Game flow and session** | `GameInitModule`, `GameData`, `GameController`, `MenuController`, `ActiveSession`, `CameraController` | Loads global game settings, holds the selected/displayed level state, switches Menu/Game scenes, and orchestrates win, fail, revive, ads, and completion UI |
| **Level System** | `LevelController`, `LevelDatabase`, `LevelData`, `LevelRepresentation`, `BlockMovementManager`, `LevelBlockBehavior` | Reads a level definition, spawns environment/blocks/interactables, maintains the movement grid, triggers the timer, and completes when all target images are collected |
| **Level authoring** | `Data/Level System`, `Level Editor.unity`, editor windows/drawers | Stores level assets, block visuals, effects, figures, and interactable definitions; editor tooling creates and edits level content without changing runtime engine code |
| **Effects and interactables** | `Level System/Effects`, `Interactable Objects` | Adds reusable obstacles and reactions such as chains, ropes, keys, ice, bombs, pinned/combined effects, and color-aware interactable objects |
| **Input** | `RaycastController`, `InputController`, `IInputMode`, `DefaultClickMode` | Converts screen interaction into selectable/clickable game objects and forwards valid block actions to the level controller |
| **Power Ups** | `PUController`, `PUDatabase`, `PUSettings`, `PUBehavior`, behavior subclasses | Initializes unlocked power-ups, manages purchase/use/UI state, and applies mechanics such as hammer, merge, freeze, and free movement |
| **Lives** | `LivesSystem`, `LivesData`, `LivesSave`, `UIAddLivesPanel` | Persists the life count, restores lives over real time, supports infinite-life rewards, and locks a life before entering gameplay |
| **Quest and Daily Reward** | `QuestService`/`QuestDatabase`; `DailyRewardService`/`DailyRewardDatabase` | Initializes data-driven progression rewards; daily rewards enforce one claim per UTC day and reset the sequence after missed days |
| **Store and No Ads** | `UIStore`, store elements, `UINoAdsOffer`, IAP reward assets | Presents currencies/offers and integrates with the core monetization/reward systems |
| **Remote Config** | `ProgressRemoteConfigData`, `LevelRemoteConfigData`, reward/revive data, `RemoteConfigController` | Reads initialized remote JSON by key and applies runtime overrides, including level hashes/duration and balance/config values |
| **Firebase and profile** | Firebase handlers, profile UI, cloud sync, leaderboard, soft update | Optional cloud identity/progress, no-ads entitlement synchronization, leaderboards, and update checks; compiled safely out when `FIREBASE` is absent |
| **Presentation and retention UI** | `UI`, `Settings`, `Tutorial`, `Feature Announcement`, `Level Map` | Renders pages/popups/HUD, settings, guided onboarding, unlock announcements, and level-map elements |
| **Developer tooling** | `Dev Panel`, level editor code, `Assets/Tests/Editor` | Provides developer controls and EditMode coverage for selected framework/game regressions |

## Data, persistence, and configuration

### Data flow

Most content follows this path:

```text
ScriptableObject database / settings asset
    → InitModule or scene controller
    → Runtime service or behavior
    → SaveController-backed save object (when state must persist)
    → UI / gameplay result
```

Examples include level definitions through `LevelDatabase`, power-up definitions through `PUDatabase`, and retention/economy data through the lives, quest, daily-reward, reward, and currency databases. Keep designer-editable defaults in ScriptableObjects and player-specific state in save models.

`RemoteConfigController` provides an optional runtime override layer. A feature asks for a typed key with `TryGetConfig<T>()`; when a valid payload is present, it overrides the local default for that run. For example, `GameData` reads progress, ads, revive, and reward settings, while `LevelController` may use a level-specific override. Remote values should therefore be validated and backwards-compatible with local content.

Firebase is not required for local play. With `FIREBASE` enabled, the Firebase module can authenticate a player, synchronize selected progress/profile data, maintain no-ads entitlement, preload/submit leaderboards, and check soft updates. Local save remains the base progression store.

### Important configuration assets

| Concern | Main location | Use it to |
| --- | --- | --- |
| Startup modules | `Assets/Project Files/Data/Project Init Settings.asset` | Enable/configure `InitModule` instances and their initialization data |
| Game-wide settings | `Assets/Project Files/Data/Game Data.asset` | Assign the level database and set gameplay defaults, rewards, revive, and ad thresholds |
| Levels and visuals | `Assets/Project Files/Data/Level System/` | Maintain `Level Database.asset`, individual level assets, effects, environment, figures, and block visual data |
| Power-ups | `Assets/Project Files/Data/Power Ups/` | Configure the power-up database and mechanic-specific settings |
| Progression | `Assets/Project Files/Data/Lives Data.asset`, `Data/Quest/`, `Data/Daily Reward/` | Configure lives, quests, and the seven-day daily-reward schedule |
| Monetization and rewards | `Assets/Project Files/Data/Monetization Settings.asset`, `Data/Rewards/` | Configure monetization/reward definitions after provider setup |
| Scenes and build | `ProjectSettings/EditorBuildSettings.asset` | Maintain scene order and enabled build scenes |
| Unity packages | `Packages/manifest.json` | Pin or upgrade Unity Package Manager dependencies |

## Extending the template

Use this pattern when building a new capability:

1. **Put game-specific code in `Assets/Project Files/Game`.** Create a focused service/controller and, where appropriate, a ScriptableObject database and serializable save model.
2. **Add an `InitModule` only for a global service.** Derive from `InitModule`, initialize the service in `CreateComponent()`, then add/configure its asset through `Project Init Settings.asset`. Ensure dependencies such as Save are initialized before the feature needs them.
3. **Keep scene behavior thin.** Scene controllers should assemble scene components and invoke services; reusable behavior should not depend on a particular puzzle scene.
4. **Register UI and scene dependencies deliberately.** Add the necessary prefabs/pages/controllers to the owning scene and initialize them from the existing `UIController`/scene flow rather than relying on arbitrary global lookups.
5. **Use extension points for common systems.** Implement reward, power-up, level-effect, or interactable data/behavior through the existing registries and databases instead of editing vendor or core framework source.
6. **Add an EditMode test for deterministic logic.** Keep behavior-specific tests in `Assets/Tests/Editor` and test persistence, remote-data parsing, and edge cases separately from visual/manual QA.

## Tests and validation

Current EditMode tests cover areas including coin-safe progress, Firebase progress/name registry behavior, network connection behavior, quest rotation, and Figma export/bridge tooling. Run them in Unity Test Runner after changing framework logic or configuration that affects those systems.

Before publishing changes, verify:

- `README.md` paths point to assets that exist in this repository.
- The three build scenes still match `ProjectSettings/EditorBuildSettings.asset`.
- Optional SDK documentation does not imply a provider is mandatory.
- No provider credentials, signing files, secrets, or customer-specific identifiers were added.
- New game features do not require direct edits to imported SDK/vendor source.

## License

Project-owned source files in this repository are licensed under the [Apache License 2.0](LICENSE). Third-party SDKs, plugins, fonts, and assets remain subject to the license notices included with those dependencies; the Apache License does not replace their terms.
