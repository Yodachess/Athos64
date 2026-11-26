# Athos64

**Athos64** is a **UCI-compatible chess engine** developed in **C# / .NET 10**, using a bitboard-based board representation.

The engine includes a **Negamax Alpha-Beta search** (PVS), a lock-free transposition table, several search heuristics,
and **NNUE evaluation** (Stockfish 19 network) through a native C++ library specifically developed for the project: **BrunoNNUE.dll**.

The project is primarily intended to be used with chess graphical interfaces supporting the UCI protocol.

---

## Features

### Search

Athos64 uses several standard chess-engine search techniques:

* Negamax with Alpha-Beta pruning and Principal Variation Search (null-window search)
* Iterative deepening
* Aspiration windows
* Transposition table (lock-free, kept between moves, cleared on `ucinewgame`)
* Zobrist hashing
* Quiescence search, with SEE pruning of losing captures and delta pruning
* Check extension
* Null Move Pruning
* Reverse Futility Pruning
* Futility Pruning
* Late Move Reductions (LMR)
* Move ordering: transposition-table move, MVV-LVA refined by SEE, killer moves, history heuristic
* Draw detection: threefold repetition and fifty-move rule
* Time management (target time and hard limit, stops between iterations, pondering)
* Multi-threaded search based on Lazy SMP

Some search heuristics may be enabled, disabled, or adjusted during development and testing.
Each change to the search is measured by a match (cutechess-cli) before being kept.

---

## Evaluation

Athos64 evaluates positions with the **NNUE (Efficiently Updatable Neural Network)** of Stockfish 19 only.
The former hand-crafted evaluation has been removed; only piece values remain, used by the SEE and the capture ordering.

The C# interface is implemented in `BrunoNNUE.cs`, which communicates with `BrunoNNUE.dll`.

The evaluation is **incremental**: each search thread has its own native context (Stockfish position and accumulator stack),
updated on every move made, null move and unmade move. A full evaluation is used as a fallback.

The goal is to keep Athos64's own search while using NNUE for position evaluation.

---

## NNUE Network

The engine uses the following NNUE network (Stockfish 19, a single network):

```text
nn-1a298aa575a0.nnue
```

It is included in the release zip. It is not stored in the Git repository (about 98 MB): copy it next to the executable.
The engine looks for it in the current directory, so start the executable from its own folder.

> **Important:** NNUE network files are not necessarily interchangeable with every version of the NNUE interface.
> Compatibility depends on the format expected by the implementation used in `BrunoNNUE.dll`.

---

## BrunoNNUE.dll

`BrunoNNUE.dll` is a native C++ library built from the NNUE code of **Stockfish 19**
(starting from https://github.com/VedantJoshi1409/stockfish_nnue_probe), compiled with Visual Studio 2022 for AVX2.
Its sources are in `BrunoNNUE-SF19/` (see its `LISEZMOI.txt`).

It acts as the bridge between Athos64 and the NNUE network:

```text
Athos64 (C#)
      │
      │ P/Invoke
      ▼
BrunoNNUE.dll (C++)
      │
      ▼
NNUE network
      │
      ▼
Evaluation in centipawns
```

This separation makes it possible to keep Athos64's search and UCI logic in C#, while using a native implementation for NNUE evaluation.

---

## UCI Protocol

Athos64 communicates with chess interfaces through the **Universal Chess Interface (UCI)** protocol.

Supported commands:

```text
uci
isready
ucinewgame
position
go
stop
ponderhit
setoption
quit
```

Options:

| Option     | Default                | Description |
| ---------- | ---------------------- | ----------- |
| `Threads`  | 4                      | Number of search threads (Lazy SMP) |
| `Depth`    | 12                     | Maximum depth for a `go` without time limit; with a clock, `movetime` or `infinite`, time stops the search |
| `Ponder`   | true                   | Thinking on the opponent's time |
| `EvalFile` | `nn-1a298aa575a0.nnue` | NNUE network file |

The transposition table has a fixed size of 128 MB.

The engine provides search information during searches, for example:

```text
info depth 12 score cp 35 nodes ... nps ... time ... pv e2e4 e7e5 ...
```

From the starting position, `go` answers immediately with a random first move, to vary the games.

Additional console commands are available for testing (`perft`, `bench`, `eval`, `d`, `testsee`, `help`...).

---

## Project Structure

The main source files are located directly in the project root:

```text
Athos64/
│
├── program.cs
├── aide.cs
├── GestionUCI.cs
├── GestionUCIGo.cs
├── GestionUCIPosition.cs
├── GestionTemps.cs
├── echiquier.cs
├── echiquierClouages.cs
├── echiquierCoups.cs
├── echiquierMouvements.cs
├── echiquierPerft.cs
├── echiquierZobrist.cs
├── chargementFen.cs
├── bitboard.cs
├── rayons.cs
├── pion.cs
├── cavalier.cs
├── fou.cs
├── tour.cs
├── reine.cs
├── roi.cs
├── recherche.cs
├── tabletransposition.cs
├── echangeStatiqueEval.cs
├── evaluationParams.cs
├── BrunoNNUE.cs
│
├── BrunoNNUE.dll
├── BrunoNNUE-SF19/        (sources of BrunoNNUE.dll)
├── nn-1a298aa575a0.nnue   (not versioned)
│
├── Diagnostics/           (console tests, benchmark, match and comparison scripts)
├── Tests/                 (xUnit tests)
├── .github/workflows/     (CI: build and tests on every push)
│
├── Athos64.csproj
├── Athos64.sln
├── Directory.Build.props  (warnings treated as errors)
├── .editorconfig
├── .gitattributes
├── .gitignore
├── LICENSE.txt
└── README.md
```

### Main Modules

| File                          | Purpose |
| ----------------------------- | ------- |
| `program.cs`                  | Engine entry point |
| `aide.cs`                     | Help screen |
| `GestionUCI.cs`               | UCI protocol handling, options |
| `GestionUCIGo.cs`             | `go` command, time allocation, `stop` and `ponderhit` |
| `GestionUCIPosition.cs`       | `ucinewgame`, `position` command, UCI move conversion |
| `GestionTemps.cs`             | Time management |
| `echiquier.cs`                | Board state and representation |
| `echiquierClouages.cs`        | Pinned pieces |
| `echiquierCoups.cs`           | Making and unmaking moves |
| `echiquierMouvements.cs`      | Move structure and legal move generation |
| `echiquierPerft.cs`           | Perft and move notation |
| `echiquierZobrist.cs`         | Zobrist hashing |
| `chargementFen.cs`            | FEN position loading |
| `bitboard.cs`                 | Bitboard operations |
| `rayons.cs`                   | Sliding-piece attacks from precomputed rays |
| `pion.cs` ... `roi.cs`        | Piece-specific attacks and moves |
| `recherche.cs`                | Search and best-move calculation |
| `tabletransposition.cs`       | Lock-free transposition table |
| `echangeStatiqueEval.cs`      | Static Exchange Evaluation (SEE) |
| `evaluationParams.cs`         | Piece values (SEE and capture ordering) |
| `BrunoNNUE.cs`                | C# interface to `BrunoNNUE.dll` |
| `Diagnostics/performance.cs`  | Performance and benchmarking utilities |

---

## Building

### Requirements

* Windows 10 or later
* Visual Studio 2026 (or the .NET 10 SDK alone)
* **x64** configuration
* A processor supporting AVX2 (for `BrunoNNUE.dll`)

### Building with Visual Studio

Open `Athos64.sln`, select `Release` and `x64`, then build the solution.

### Building from the command line

```powershell
dotnet build Athos64.sln -c Release -p:Platform=x64
```

The output is generated in:

```text
bin\x64\Release\net10.0\
```

Copy `nn-1a298aa575a0.nnue` into this folder (`BrunoNNUE.dll` is copied automatically).

### Tests

```powershell
dotnet test Tests
```

The xUnit tests check perft reference values (starting position, Kiwipete, positions 3 to 6 of chessprogramming.org),
the SEE, threefold repetition, the fifty-move rule and the Zobrist key consistency.

---

## Running Athos64

From the output directory (or the folder of the unzipped release):

```text
Athos64.exe
```

Athos64 runs as a UCI chess engine in console mode. It can be launched directly or from any chess GUI supporting UCI engines.

For a basic test, send `uci`. The engine answers with its name, author, options and:

```text
id name Athos64 V 1.40 NNUE_SF19 x64
id author Bruno Courtois
...
uciok
```

Then send `isready`; the expected response is `readyok`.

---

## Testing a Position

A position can be loaded using a FEN string:

```text
position fen r1bq1r1k/ppp1b1pp/3p1n2/2P1np2/8/1QNBPN2/PP3PPP/R1B2RK1 w - - 5 11
```

Then start a search:

```text
go depth 15
```

The engine progressively outputs search information and eventually returns:

```text
bestmove ...
```

---

## General Architecture

```text
            ┌───────────────────────────┐
            │        Chess GUI          │
            └─────────────┬─────────────┘
                          │ UCI
                          ▼
            ┌───────────────────────────┐
            │   Athos64 (C# / .NET 10)  │
            ├───────────────────────────┤
            │ UCI management            │
            │ Time management           │
            │ Board representation      │
            │ Move generation           │
            │ Search (Lazy SMP)         │
            │ Transposition table       │
            └─────────────┬─────────────┘
                          │ P/Invoke / C API
                          ▼
            ┌───────────────────────────┐
            │   BrunoNNUE.dll (C++)     │
            │   AVX2, incremental       │
            └─────────────┬─────────────┘
                          │
                          ▼
            ┌───────────────────────────┐
            │  NNUE network (SF 19)     │
            └───────────────────────────┘
```

---

## NNUE Code Origin

The native NNUE component is based on NNUE code derived from **Stockfish**.

Stockfish is distributed under the **GNU General Public License version 3 (GPLv3)**.
Code derived from Stockfish remains subject to the terms and conditions of its license.

Athos64 is also distributed under the **GPLv3** (GPL-3.0-or-later). See `LICENSE.txt`.

---

## License

Athos64 is distributed under the **GNU General Public License v3.0 or later**.

In general, the GPLv3 allows:

* use of the software;
* study of its source code;
* modification;
* redistribution;
* redistribution of modified versions;

provided that the conditions of the GPLv3 are respected.

Components originating from other projects remain subject to their respective licenses.

---

## Project Status

Athos64 is an **active development project**.

The engine is regularly tested and improved in areas including:

* playing strength;
* stability;
* search;
* performance;
* multi-threading;
* NNUE integration;
* UCI compatibility.

Results and implementation details may therefore change between versions.

---

## Author

**Bruno COURTOIS**

Athos64 is a personal project focused on the development and study of chess engines.

The project is used to explore and improve skills in:

* chess-engine programming;
* search algorithms;
* bitboards;
* NNUE networks;
* performance optimization;
* C# and C++ programming.

---

## Repository

Athos64 is hosted on GitHub: **Yodachess / Athos64**

---

## Acknowledgements

Special thanks to the developers and contributors of **Stockfish** and to the open-source projects that have made the study and use of NNUE technology in chess engines possible.

Athos64 is **not a version of Stockfish**. Its search engine, board representation, and game logic are developed independently in C#, while certain NNUE components are used as a native evaluation library.

---

## Disclaimer

Athos64 is provided as an experimental and continuously evolving chess-engine project.

No guarantee is made regarding playing strength, performance, compatibility, or stability.

---
---

# Athos64 (version française)

**Athos64** est un **moteur d'échecs compatible UCI**, développé en **C# / .NET 10**, utilisant une représentation de l'échiquier basée sur les bitboards.

Le moteur intègre une recherche **Negamax avec élagage Alpha-Beta** (PVS), une table de transposition sans verrou, plusieurs heuristiques de recherche
ainsi qu'une **évaluation NNUE** (réseau de Stockfish 19) via une bibliothèque native C++ développée spécifiquement pour le projet : **BrunoNNUE.dll**.

Le projet est principalement destiné à être utilisé avec des interfaces graphiques d'échecs compatibles avec le protocole UCI.

---

## Fonctionnalités

### Recherche

Athos64 utilise plusieurs techniques classiques de recherche :

* Negamax avec élagage Alpha-Beta et Principal Variation Search (recherche à fenêtre nulle)
* Approfondissement itératif
* Fenêtres d'aspiration
* Table de transposition (sans verrou, gardée d'un coup à l'autre, vidée à `ucinewgame`)
* Hashage Zobrist
* Recherche de quiescence, avec élagage des captures perdantes au SEE et Delta Pruning
* Extension d'échec
* Null Move Pruning
* Reverse Futility Pruning
* Futility Pruning
* Late Move Reductions (LMR)
* Tri des coups : coup de la table de transposition, MVV-LVA affiné par le SEE, coups tueurs, historique
* Détection des nulles : triple répétition et règle des 50 coups
* Gestion du temps (temps visé et limite maximale, arrêt entre les itérations, ponder)
* Recherche multi-thread basée sur Lazy SMP

Certaines heuristiques peuvent être activées, désactivées ou ajustées au cours du développement et des tests.
Chaque modification de la recherche est mesurée par un match (cutechess-cli) avant d'être gardée.

---

## Évaluation

Athos64 évalue les positions uniquement avec le réseau **NNUE (Efficiently Updatable Neural Network)** de Stockfish 19.
L'ancienne évaluation écrite à la main a été retirée ; il ne reste que les valeurs des pièces, utilisées par le SEE et le tri des captures.

L'interface C# est implémentée dans `BrunoNNUE.cs`, qui communique avec `BrunoNNUE.dll`.

L'évaluation est **incrémentale** : chaque thread de recherche a son propre contexte natif (position Stockfish et pile d'accumulateurs),
mis à jour à chaque coup joué, coup nul et coup annulé. Une évaluation complète sert de repli.

L'objectif est de conserver la recherche propre à Athos64 tout en utilisant NNUE pour l'évaluation des positions.

---

## Réseau NNUE

Le moteur utilise le réseau NNUE suivant (Stockfish 19, un seul réseau) :

```text
nn-1a298aa575a0.nnue
```

Il est fourni dans le zip de la release. Il n'est pas dans le dépôt Git (environ 98 Mo) : le copier à côté de l'exécutable.
Le moteur le cherche dans le dossier courant : lancer l'exécutable depuis son propre dossier.

> **Important :** les fichiers de réseau NNUE ne sont pas nécessairement interchangeables avec toutes les versions de l'interface NNUE.
> Leur compatibilité dépend du format attendu par l'implémentation utilisée dans `BrunoNNUE.dll`.

---

## BrunoNNUE.dll

`BrunoNNUE.dll` est une bibliothèque native C++ construite à partir du code NNUE de **Stockfish 19**
(au départ https://github.com/VedantJoshi1409/stockfish_nnue_probe), compilée avec Visual Studio 2022 en AVX2.
Ses sources sont dans `BrunoNNUE-SF19/` (voir son `LISEZMOI.txt`).

Elle sert d'interface entre Athos64 et le réseau NNUE :

```text
Athos64 (C#)
      │
      │ P/Invoke
      ▼
BrunoNNUE.dll (C++)
      │
      ▼
Réseau NNUE
      │
      ▼
Évaluation en centipions
```

Cette séparation permet de conserver la recherche et la logique UCI d'Athos64 en C#, tout en utilisant une implémentation native pour l'évaluation NNUE.

---

## Protocole UCI

Athos64 communique avec les interfaces d'échecs au moyen du protocole **Universal Chess Interface (UCI)**.

Commandes prises en charge :

```text
uci
isready
ucinewgame
position
go
stop
ponderhit
setoption
quit
```

Options :

| Option     | Défaut                 | Description |
| ---------- | ---------------------- | ----------- |
| `Threads`  | 4                      | Nombre de threads de recherche (Lazy SMP) |
| `Depth`    | 12                     | Profondeur maximale d'un `go` sans limite de temps ; avec une pendule, `movetime` ou `infinite`, c'est le temps qui arrête la recherche |
| `Ponder`   | true                   | Réflexion pendant le temps de l'adversaire |
| `EvalFile` | `nn-1a298aa575a0.nnue` | Fichier du réseau NNUE |

La table de transposition a une taille fixe de 128 Mo.

Le moteur fournit des informations pendant la recherche, par exemple :

```text
info depth 12 score cp 35 nodes ... nps ... time ... pv e2e4 e7e5 ...
```

Depuis la position initiale, `go` répond tout de suite avec un premier coup aléatoire, pour varier les parties.

Des commandes console supplémentaires servent aux essais (`perft`, `bench`, `eval`, `d`, `testsee`, `help`...).

---

## Structure du projet

Les principaux fichiers source sont situés directement à la racine du projet :

```text
Athos64/
│
├── program.cs
├── aide.cs
├── GestionUCI.cs
├── GestionUCIGo.cs
├── GestionUCIPosition.cs
├── GestionTemps.cs
├── echiquier.cs
├── echiquierClouages.cs
├── echiquierCoups.cs
├── echiquierMouvements.cs
├── echiquierPerft.cs
├── echiquierZobrist.cs
├── chargementFen.cs
├── bitboard.cs
├── rayons.cs
├── pion.cs
├── cavalier.cs
├── fou.cs
├── tour.cs
├── reine.cs
├── roi.cs
├── recherche.cs
├── tabletransposition.cs
├── echangeStatiqueEval.cs
├── evaluationParams.cs
├── BrunoNNUE.cs
│
├── BrunoNNUE.dll
├── BrunoNNUE-SF19/        (sources de BrunoNNUE.dll)
├── nn-1a298aa575a0.nnue   (non versionné)
│
├── Diagnostics/           (tests console, bench, scripts de match et de comparaison)
├── Tests/                 (tests xUnit)
├── .github/workflows/     (CI : compilation et tests à chaque push)
│
├── Athos64.csproj
├── Athos64.sln
├── Directory.Build.props  (avertissements traités comme des erreurs)
├── .editorconfig
├── .gitattributes
├── .gitignore
├── LICENSE.txt
└── README.md
```

### Principaux modules

| Fichier                       | Fonction |
| ----------------------------- | -------- |
| `program.cs`                  | Point d'entrée du moteur |
| `aide.cs`                     | Écran d'aide |
| `GestionUCI.cs`               | Gestion du protocole UCI, options |
| `GestionUCIGo.cs`             | Commande `go`, temps alloué, `stop` et `ponderhit` |
| `GestionUCIPosition.cs`       | `ucinewgame`, commande `position`, conversion des coups UCI |
| `GestionTemps.cs`             | Gestion du temps de réflexion |
| `echiquier.cs`                | État et représentation de l'échiquier |
| `echiquierClouages.cs`        | Pièces clouées |
| `echiquierCoups.cs`           | Jouer et annuler les coups |
| `echiquierMouvements.cs`      | Structure des mouvements et génération des coups légaux |
| `echiquierPerft.cs`           | Perft et notation des coups |
| `echiquierZobrist.cs`         | Hashage Zobrist |
| `chargementFen.cs`            | Chargement des positions FEN |
| `bitboard.cs`                 | Opérations sur les bitboards |
| `rayons.cs`                   | Attaques des pièces glissantes par rayons précalculés |
| `pion.cs` ... `roi.cs`        | Attaques et mouvements propres à chaque pièce |
| `recherche.cs`                | Recherche et calcul du meilleur coup |
| `tabletransposition.cs`       | Table de transposition sans verrou |
| `echangeStatiqueEval.cs`      | Static Exchange Evaluation (SEE) |
| `evaluationParams.cs`         | Valeurs des pièces (SEE et tri des captures) |
| `BrunoNNUE.cs`                | Interface C# avec `BrunoNNUE.dll` |
| `Diagnostics/performance.cs`  | Outils de mesure et de performance |

---

## Compilation

### Prérequis

* Windows 10 ou supérieur
* Visual Studio 2026 (ou le SDK .NET 10 seul)
* Configuration **x64**
* Un processeur prenant en charge AVX2 (pour `BrunoNNUE.dll`)

### Compilation avec Visual Studio

Ouvrir `Athos64.sln`, sélectionner `Release` et `x64`, puis compiler la solution.

### Compilation en ligne de commande

```powershell
dotnet build Athos64.sln -c Release -p:Platform=x64
```

Le résultat est généré dans :

```text
bin\x64\Release\net10.0\
```

Copier `nn-1a298aa575a0.nnue` dans ce dossier (`BrunoNNUE.dll` y est copiée automatiquement).

### Tests

```powershell
dotnet test Tests
```

Les tests xUnit vérifient les valeurs de référence du perft (position initiale, Kiwipete, positions 3 à 6 de chessprogramming.org),
le SEE, la triple répétition, la règle des 50 coups et la cohérence de la clé Zobrist.

---

## Exécution d'Athos64

Depuis le répertoire de sortie (ou le dossier du zip décompressé) :

```text
Athos64.exe
```

Athos64 fonctionne comme un moteur d'échecs UCI en mode console. Il peut être lancé directement ou depuis n'importe quelle interface graphique compatible avec les moteurs UCI.

Pour un test simple, envoyer `uci`. Le moteur répond avec son nom, son auteur, ses options et :

```text
id name Athos64 V 1.40 NNUE_SF19 x64
id author Bruno Courtois
...
uciok
```

Puis envoyer `isready` ; la réponse attendue est `readyok`.

---

## Tester une position

Une position peut être chargée à l'aide d'une chaîne FEN :

```text
position fen r1bq1r1k/ppp1b1pp/3p1n2/2P1np2/8/1QNBPN2/PP3PPP/R1B2RK1 w - - 5 11
```

Puis lancer une recherche :

```text
go depth 15
```

Le moteur affiche progressivement les informations de recherche et retourne finalement :

```text
bestmove ...
```

---

## Architecture générale

```text
            ┌───────────────────────────┐
            │  Interface graphique      │
            └─────────────┬─────────────┘
                          │ UCI
                          ▼
            ┌───────────────────────────┐
            │   Athos64 (C# / .NET 10)  │
            ├───────────────────────────┤
            │ Gestion UCI               │
            │ Gestion du temps          │
            │ Représentation échiquier  │
            │ Génération des coups      │
            │ Recherche (Lazy SMP)      │
            │ Table de transposition    │
            └─────────────┬─────────────┘
                          │ P/Invoke / API C
                          ▼
            ┌───────────────────────────┐
            │   BrunoNNUE.dll (C++)     │
            │   AVX2, incrémentale      │
            └─────────────┬─────────────┘
                          │
                          ▼
            ┌───────────────────────────┐
            │  Réseau NNUE (SF 19)      │
            └───────────────────────────┘
```

---

## Origine du code NNUE

Le composant NNUE natif est basé sur du code NNUE issu de **Stockfish**.

Stockfish est distribué sous licence **GNU General Public License version 3 (GPLv3)**.
Le code provenant de Stockfish reste soumis aux conditions de sa licence.

Athos64 est également distribué sous **GPLv3** (GPL-3.0-or-later). Voir `LICENSE.txt`.

---

## Licence

Athos64 est distribué sous **GNU General Public License v3.0 ou ultérieure**.

De manière générale, la GPLv3 autorise notamment :

* l'utilisation du logiciel ;
* l'étude de son fonctionnement ;
* la modification du code ;
* la redistribution ;
* la redistribution des versions modifiées ;

sous réserve du respect des conditions de la GPLv3.

Les composants provenant d'autres projets restent soumis à leurs licences respectives.

---

## État du projet

Athos64 est un **projet en développement actif**.

Le moteur est régulièrement testé et amélioré dans les domaines suivants :

* force de jeu ;
* stabilité ;
* recherche ;
* performances ;
* multi-threading ;
* intégration NNUE ;
* compatibilité UCI.

Les résultats et les détails d'implémentation peuvent donc évoluer d'une version à l'autre.

---

## Auteur

**Bruno COURTOIS**

Athos64 est un projet personnel consacré au développement et à l'étude des moteurs d'échecs.

Le projet permet notamment d'explorer et d'améliorer les connaissances dans les domaines suivants :

* programmation de moteurs d'échecs ;
* algorithmes de recherche ;
* bitboards ;
* réseaux NNUE ;
* optimisation des performances ;
* programmation C# et C++.

---

## Dépôt

Athos64 est hébergé sur GitHub : **Yodachess / Athos64**

---

## Remerciements

Un grand merci aux développeurs et contributeurs de **Stockfish**, ainsi qu'aux projets open source ayant permis l'étude et l'utilisation de la technologie NNUE dans les moteurs d'échecs.

Athos64 **n'est pas une version de Stockfish**. Son moteur de recherche, sa représentation de l'échiquier et sa logique de jeu sont développés indépendamment en C#, tandis que certains composants NNUE sont utilisés sous forme de bibliothèque native d'évaluation.

---

## Avertissement

Athos64 est fourni comme un projet de moteur d'échecs expérimental et en évolution constante.

Aucune garantie n'est donnée concernant la force de jeu, les performances, la compatibilité ou la stabilité du logiciel.
