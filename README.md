# Athos64

**Athos64** is a **UCI-compatible chess engine** developed in **C# / .NET 8**, using a bitboard-based board representation.

The engine currently includes **Negamax Alpha-Beta search**, a transposition table, several search heuristics, and **NNUE evaluation** through a native C++ library specifically developed for the project: **BrunoNNUE.dll**.

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
* Futility Pruning
* Killer Moves
* History Heuristic
* MVV-LVA
* Aspiration Windows
* Time management
* Multi-threaded search based on Lazy SMP

Some search heuristics may be enabled, disabled, or adjusted during development and testing.

---

## Evaluation

Athos64 supports two evaluation approaches.

### Classical Evaluation

The classical evaluation uses various parameters covering, among other things:

* Material
* Doubled pawns
* Isolated pawns
* Passed pawns
* Advanced pawns
* Bishop pair
* Castling
* King safety
* Piece development
* Rook positioning
* Pawn structure
* Game phase

The evaluation parameters are grouped in:

```text
evaluationParams.cs
```

### NNUE Evaluation

Athos64 can also use **NNUE (Efficiently Updatable Neural Network)** evaluation.

The C# interface is implemented in:

```text
BrunoNNUE.cs
```

It communicates with:

```text
BrunoNNUE.dll
```

This native library provides the functions required to initialize the networks and evaluate a chess position.

The goal is to keep Athos64's own search while using NNUE for position evaluation.

---

## NNUE Networks

The repository currently contains the NNUE networks used by the engine:

```text
nn-b1a57edbea57.nnue
nn-baff1ede1f90.nnue
```

More recent networks have also been used during development, including:

```text
nn-37f18f62d772.nnue
nn-c288c895ea92.nnue
```

The exact networks used depend on the version of `BrunoNNUE.dll`.

> **Important:** NNUE network files are not necessarily interchangeable with every version of the NNUE interface. Compatibility depends on the format expected by the implementation used in `BrunoNNUE.dll`.

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

The repository is currently private.

---

## Acknowledgements

Special thanks to the developers and contributors of **Stockfish** and to the open-source projects that have made the study and use of NNUE technology in chess engines possible.

Athos64 is **not a version of Stockfish**. Its search engine, board representation, and game logic are developed independently in C#, while certain NNUE components are used as a native evaluation library.

---

## Disclaimer

Athos64 is provided as an experimental and continuously evolving chess-engine project.

No guarantee is made regarding playing strength, performance, compatibility, or stability.
