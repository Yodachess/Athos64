# Athos64

**Athos64** is a **UCI-compatible chess engine** developed in **C# / .NET 8**, using a bitboard-based board representation.

The engine currently includes **Negamax Alpha-Beta search**, a transposition table, several search heuristics, 
and **NNUE evaluation** through a native C++ library specifically developed for the project: **BrunoNNUE.dll**.

The project is primarily intended to be used with chess graphical interfaces supporting the UCI protocol.

---

## Features

### Search

Athos64 currently uses several standard chess-engine search techniques:

* Negamax with Alpha-Beta pruning
* Iterative deepening
* Transposition Table
* Zobrist hashing
* Quiescence Search
* Null Move Pruning
* Late Move Reductions (LMR)
* Killer Moves
* History Heuristic
* MVV-LVA
* Aspiration Windows
* Time management
* Multi-threaded search based on Lazy SMP

Some search heuristics may be enabled, disabled, or adjusted during development and testing.

---

## Evaluation

Athos64 supports one evaluation approach.

### NNUE Evaluation

Athos64 uses **NNUE (Efficiently Updatable Neural Network)** evaluation.

The C# interface is implemented in:   BrunoNNUE.cs
```

It communicates with:    BrunoNNUE.dll
```

This native library provides the functions required to initialize the networks and evaluate a chess position.

The goal is to keep Athos64's own search while using NNUE for position evaluation.

---

## NNUE Networks

The repository currently contains the NNUE networks used by the engine:

```text
nn-37f18f62d772.nnue
nn-c288c895ea92.nnue
```

The exact networks used depend on the version of `BrunoNNUE.dll`.

> **Important:** NNUE network files are not necessarily interchangeable with every version of the NNUE interface.
> Compatibility depends on the format expected by the implementation used in `BrunoNNUE.dll`.

---

## BrunoNNUE.dll

`BrunoNNUE.dll` is a native C++ library developed using NNUE components derived from **Stockfish**.

It acts as the bridge between Athos64 and the NNUE networks.

The architecture is:

```text
Athos64 (C#)
      │
      │ P/Invoke
      ▼
BrunoNNUE.dll (C++)
      │
      ▼
NNUE Network
      │
      ▼
Evaluation in centipawns
```

This separation makes it possible to keep Athos64's search and UCI logic in C#, while using a native implementation for NNUE evaluation.

---

## UCI Protocol

Athos64 communicates with chess interfaces through the **Universal Chess Interface (UCI)** protocol.

The main UCI commands include:

```text
uci
isready
ucinewgame
position
go
stop
quit
setoption
```

The engine can also provide diagnostic information during searches.

Example:

```text
info depth 12 score cp 35 pv e2e4 e7e5 ...
```

---

## Project Structure

The main source files are located directly in the project root:

```text
Athos64/
│
├── aide.cs
├── bitboard.cs
├── BrunoNNUE.cs
├── cavalier.cs
├── chargementFen.cs
├── echangeStatiqueEval.cs
├── echiquier.cs
├── echiquierCoups.cs
├── echiquierMouvements.cs
├── echiquierZobrist.cs
├── evaluationParams.cs
├── fou.cs
├── GestionTemps.cs
├── GestionUCI.cs
├── performance.cs
├── pion.cs
├── program.cs
├── Projet_BrunoMoteurUCI.cs
├── recherche.cs
├── reine.cs
├── roi.cs
├── tabletransposition.cs
├── tour.cs
│
├── BrunoNNUE.dll
├── nn-b1a57edbea57.nnue
├── nn-baff1ede1f90.nnue
│
├── Athos64.csproj
├── Athos64.sln
├── LICENSE.txt
├── .gitignore
└── .gitattributes
```

### Main Modules

| File                     | Purpose                                |
| ------------------------ | -------------------------------------- |
| `program.cs`             | Engine entry point                     |
| `GestionUCI.cs`          | UCI protocol handling                  |
| `GestionTemps.cs`        | Time management                        |
| `echiquier.cs`           | Board state and representation         |
| `bitboard.cs`            | Bitboard operations                    |
| `echiquierMouvements.cs` | Move generation and management         |
| `echiquierCoups.cs`      | Move handling                          |
| `echiquierZobrist.cs`    | Zobrist hashing                        |
| `recherche.cs`           | Search and best-move calculation       |
| `tabletransposition.cs`  | Transposition table                    |
| `evaluationParams.cs`    | Evaluation parameters                  |
| `echangeStatiqueEval.cs` | Static Exchange Evaluation             |
| `BrunoNNUE.cs`           | C# interface to `BrunoNNUE.dll`        |
| `chargementFen.cs`       | FEN position loading                   |
| `performance.cs`         | Performance and benchmarking utilities |

---

## Building

### Requirements

* Windows 10 or later
* Visual Studio 2022
* .NET 8 SDK
* **x64** configuration
* A compatible `BrunoNNUE.dll`

The project is configured for **64-bit** operation, particularly because the native NNUE library is compiled for this architecture.

### Building with Visual Studio

Open:

```text
Athos64.sln
```

Then select:

```text
Release
x64
```

and build the solution.

The output is generated in a directory similar to:

```text
bin\x64\Release\net8.0\
```

---

## Running Athos64

From the output directory:

```text
Athos64.exe
```

Athos64 runs as a UCI chess engine in console mode.

It can be launched directly or from any chess GUI supporting UCI engines.

For a basic test, send:

```text
uci
```

The engine should respond with information such as:

```text
id name Athos64
id author Bruno COURTOIS
uciok
```

Then send:

```text
isready
```

The expected response is:

```text
readyok
```

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

The engine will progressively output search information and eventually return:

```text
bestmove ...
```

---

## Generated Directories

The following directories are automatically generated by Visual Studio / MSBuild:

```text
bin/
obj/
```

They contain build and intermediate files rather than engine source code.

They should normally be excluded from the Git repository through:

```text
.gitignore
```

Build artifacts such as the following should generally not be committed:

```text
*.pdb
*.deps.json
*.runtimeconfig.json
bin/
obj/
```

---

## General Architecture

The current architecture can be summarized as follows:

```text
                    ┌─────────────────────┐
                    │      Chess GUI      │
                    │       / UCI         │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │       Athos64       │
                    │       C# / .NET 8    │
                    ├─────────────────────┤
                    │ UCI Management      │
                    │ Time Management     │
                    │ Board Representation│
                    │ Move Generation     │
                    │ Search              │
                    │ Transposition Table │
                    │ Evaluation          │
                    └──────────┬──────────┘
                               │
                       P/Invoke / C API
                               │
                               ▼
                    ┌─────────────────────┐
                    │    BrunoNNUE.dll    │
                    │       C++ / x64      │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │     NNUE Network    │
                    └─────────────────────┘
```

---

## NNUE Code Origin

The native NNUE component is based on NNUE code derived from **Stockfish**.

Stockfish is distributed under the **GNU General Public License version 3 (GPLv3)**.

Code derived from Stockfish remains subject to the terms and conditions of its license.

Athos64 is also distributed under the **GPLv3**.

See:

```text
LICENSE.txt
```

for the complete license text applicable to the project.

---

## License

Athos64 is distributed under the:

**GNU General Public License v3.0**

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
* classical evaluation;
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
* positional evaluation;
* NNUE networks;
* performance optimization;
* C# and C++ programming.

---

## Repository

Athos64 is hosted on GitHub:

**Yodachess / Athos64**

---

## Acknowledgements

Special thanks to the developers and contributors of **Stockfish** and to the open-source projects that have made the study and use of NNUE technology in chess engines possible.

Athos64 is **not a version of Stockfish**. Its search engine, board representation, and game logic are developed independently in C#, while certain NNUE components are used as a native evaluation library.

---

## Disclaimer


VERSION FRANCAISE    


# Athos64

**Athos64** est un **moteur d'échecs compatible UCI**, développé en **C# / .NET 8**, utilisant une représentation de l'échiquier basée sur les bitboards.

Le moteur intègre actuellement une recherche **Negamax avec élagage Alpha-Beta**, une table de transposition, plusieurs heuristiques de recherche 
ainsi qu'une **évaluation NNUE** via une bibliothèque native C++ développée spécifiquement pour le projet : **BrunoNNUE.dll**.

Le projet est principalement destiné à être utilisé avec des interfaces graphiques d'échecs compatibles avec le protocole UCI.

---

## Fonctionnalités

### Recherche

Athos64 utilise actuellement plusieurs techniques classiques de recherche utilisées dans les moteurs d'échecs :

* Negamax avec élagage Alpha-Beta
* Recherche itérative
* Table de transposition
* Hashage Zobrist
* Quiescence Search
* Null Move Pruning
* Late Move Reductions (LMR)
* Futility Pruning
* Killer Moves
* History Heuristic
* MVV-LVA
* Aspiration Windows
* Gestion du temps
* Recherche multi-thread basée sur Lazy SMP

Certaines heuristiques peuvent être activées, désactivées ou ajustées au cours du développement et des tests.

---

## Évaluation

Athos64 dispose de l'évaluation.

### Évaluation NNUE

Athos64 utilise une évaluation **NNUE (Efficiently Updatable Neural Network)**.

L'interface C# est implémentée dans :  BrunoNNUE.cs
```
Elle communique avec :   BrunoNNUE.dll
```

Cette bibliothèque native fournit les fonctions nécessaires pour initialiser les réseaux et évaluer une position.

L'objectif est de conserver la recherche propre à Athos64 tout en utilisant NNUE pour l'évaluation des positions.

---

## Réseaux NNUE

Le dépôt contient actuellement les réseaux NNUE utilisés par le moteur :

```text
nn-37f18f62d772.nnue
nn-c288c895ea92.nnue
```

Les réseaux effectivement utilisés dépendent de la version de `BrunoNNUE.dll`.

> **Important :** les fichiers de réseau NNUE ne sont pas nécessairement interchangeables avec toutes les versions de l'interface NNUE.
> Leur compatibilité dépend notamment du format attendu par l'implémentation utilisée dans `BrunoNNUE.dll`.

---

## BrunoNNUE.dll

`BrunoNNUE.dll` est une bibliothèque native C++ développée à partir de composants NNUE issus de **Stockfish**.

Elle sert d'interface entre Athos64 et les réseaux NNUE.

L'architecture est la suivante :

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
Évaluation en centipawns
```

Cette séparation permet de conserver la recherche et la logique UCI d'Athos64 en C#, tout en utilisant une implémentation native pour l'évaluation NNUE.

---

## Protocole UCI

Athos64 communique avec les interfaces d'échecs au moyen du protocole **Universal Chess Interface (UCI)**.

Les principales commandes UCI comprennent notamment :

```text
uci
isready
ucinewgame
position
go
stop
quit
setoption
```

Le moteur peut également fournir des informations de diagnostic pendant les recherches.

Exemple :

```text
info depth 12 score cp 35 pv e2e4 e7e5 ...
```

---

## Structure du projet

Les principaux fichiers source sont situés directement à la racine du projet :

```text
Athos64/
│
├── aide.cs
├── bitboard.cs
├── BrunoNNUE.cs
├── cavalier.cs
├── chargementFen.cs
├── echangeStatiqueEval.cs
├── echiquier.cs
├── echiquierCoups.cs
├── echiquierMouvements.cs
├── echiquierZobrist.cs
├── evaluationParams.cs
├── fou.cs
├── GestionTemps.cs
├── GestionUCI.cs
├── performance.cs
├── pion.cs
├── program.cs
├── Projet_BrunoMoteurUCI.cs
├── recherche.cs
├── reine.cs
├── roi.cs
├── tabletransposition.cs
├── tour.cs
│
├── BrunoNNUE.dll
├── nn-b1a57edbea57.nnue
├── nn-baff1ede1f90.nnue
│
├── Athos64.csproj
├── Athos64.sln
├── LICENSE.txt
├── .gitignore
└── .gitattributes
```

### Principaux modules

| Fichier                  | Fonction                              |
| ------------------------ | ------------------------------------- |
| `program.cs`             | Point d'entrée du moteur              |
| `GestionUCI.cs`          | Gestion du protocole UCI              |
| `GestionTemps.cs`        | Gestion du temps de réflexion         |
| `echiquier.cs`           | État et représentation de l'échiquier |
| `bitboard.cs`            | Opérations sur les bitboards          |
| `echiquierMouvements.cs` | Génération et gestion des mouvements  |
| `echiquierCoups.cs`      | Gestion des coups                     |
| `echiquierZobrist.cs`    | Hashage Zobrist                       |
| `recherche.cs`           | Recherche et calcul du meilleur coup  |
| `tabletransposition.cs`  | Table de transposition                |
| `evaluationParams.cs`    | Paramètres d'évaluation               |
| `echangeStatiqueEval.cs` | Static Exchange Evaluation            |
| `BrunoNNUE.cs`           | Interface C# avec `BrunoNNUE.dll`     |
| `chargementFen.cs`       | Chargement des positions FEN          |
| `performance.cs`         | Outils de mesure et de performance    |

---

## Compilation

### Prérequis

* Windows 10 ou supérieur
* Visual Studio 2022
* .NET 8 SDK
* Configuration **x64**
* Une version compatible de `BrunoNNUE.dll`

Le projet est configuré pour fonctionner en **64 bits**, notamment parce que la bibliothèque native NNUE est compilée pour cette architecture.

### Compilation avec Visual Studio

Ouvrir :

```text
Athos64.sln
```

Puis sélectionner :

```text
Release
x64
```

et compiler la solution.

Le résultat est généré dans un répertoire similaire à :

```text
bin\x64\Release\net8.0\
```

---

## Exécution d'Athos64

Depuis le répertoire de sortie :

```text
Athos64.exe
```

Athos64 fonctionne comme un moteur d'échecs UCI en mode console.

Il peut être lancé directement ou depuis n'importe quelle interface graphique compatible avec les moteurs UCI.

Pour effectuer un test simple, envoyer :

```text
uci
```

Le moteur doit notamment répondre :

```text
id name Athos64
id author Bruno COURTOIS
uciok
```

Puis envoyer :

```text
isready
```

La réponse attendue est :

```text
readyok
```

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

## Répertoires générés

Les répertoires suivants sont générés automatiquement par Visual Studio / MSBuild :

```text
bin/
obj/
```

Ils contiennent les fichiers de compilation et les fichiers intermédiaires, et non le code source du moteur.

Ils doivent normalement être exclus du dépôt Git via :

```text
.gitignore
```

Les fichiers suivants ne doivent généralement pas être versionnés :

```text
*.pdb
*.deps.json
*.runtimeconfig.json
bin/
obj/
```

---

## Architecture générale

L'architecture actuelle peut être résumée ainsi :

```text
                    ┌─────────────────────┐
                    │   Interface UCI     │
                    │     / GUI           │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │       Athos64       │
                    │       C# / .NET 8    │
                    ├─────────────────────┤
                    │ Gestion UCI          │
                    │ Gestion du temps     │
                    │ Représentation       │
                    │ de l'échiquier      │
                    │ Génération des coups │
                    │ Recherche            │
                    │ Table de transposition│
                    │ Évaluation           │
                    └──────────┬──────────┘
                               │
                       P/Invoke / C API
                               │
                               ▼
                    ┌─────────────────────┐
                    │    BrunoNNUE.dll    │
                    │       C++ / x64      │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │     Réseau NNUE     │
                    └─────────────────────┘
```

---

## Origine du code NNUE

Le composant NNUE natif est basé sur du code NNUE issu de **Stockfish**.

Stockfish est distribué sous licence **GNU General Public License version 3 (GPLv3)**.

Le code provenant de Stockfish reste soumis aux conditions de sa licence.

Athos64 est également distribué sous **GPLv3**.

Voir :   LICENSE.txt    pour le texte complet de la licence applicable au projet.

---

## Licence

Athos64 est distribué sous :

**GNU General Public License v3.0**

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
* évaluation classique ;
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
* évaluation positionnelle ;
* réseaux NNUE ;
* optimisation des performances ;
* programmation C# et C++.

---

## Dépôt

Athos64 est hébergé sur GitHub :

**Yodachess / Athos64**

---

## Remerciements

Un grand merci aux développeurs et contributeurs de **Stockfish**, ainsi qu'aux projets open source ayant permis l'étude et l'utilisation de la technologie NNUE dans les moteurs d'échecs.

Athos64 **n'est pas une version de Stockfish**. Son moteur de recherche, sa représentation de l'échiquier et sa logique de jeu sont développés indépendamment en C#, tandis que certains composants NNUE sont utilisés sous forme de bibliothèque native d'évaluation.

---

## Avertissement

Athos64 est fourni comme un projet de moteur d'échecs expérimental et en évolution constante.

Aucune garantie n'est donnée concernant la force de jeu, les performances, la compatibilité ou la stabilité du logiciel.


Athos64 is provided as an experimental and continuously evolving chess-engine project.

No guarantee is made regarding playing strength, performance, compatibility, or stability.
