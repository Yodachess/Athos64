# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Projet

Athos64 : moteur d'échecs **UCI en C#**, 64 bits (bitboards), licence GPL-3.0-or-later. Il évalue les positions avec les **réseaux de neurones NNUE de Stockfish 18** (`nn-b1a57edbea57.nnue` grand réseau, `nn-baff1ede1f90.nnue` petit réseau), appelés par `BrunoNNUE.cs` qui fait le pont vers `BrunoNNUE.dll` (DLL native construite à partir de https://github.com/VedantJoshi1409/stockfish_nnue_probe, mise à niveau avec les réseaux de Stockfish 18). Cadres du projet : `Athos64.csproj` (`net8.0`, `Nullable` activé, `AllowUnsafeBlocks`), `Athos64.sln`. Les fichiers `.nnue`, `BrunoNNUE.dll` et les `.epd` sont copiés à côté de l'exe (`PreserveNewest`).

Tout le code, les identifiants et les commentaires sont en français : garder cette convention.

## Pratiques (reprises de BrunoGUI_GenII et BrunoGUI_Dames)

**Manière de travailler**
- Avant de compiler : vérifier que l'exe du moteur ne tourne pas (un processus `Athos64` ouvert verrouille `bin`), sinon demander de le fermer. Ne jamais lancer le moteur ni une interface qui le pilote sans prévenir.
- Commit seulement après un essai ; push seulement sur demande explicite (« pousse les commits ») ; après un push demandé, vérifier la CI toi-même (attendre la fin) et donner le résultat ; version publiée ou tag seulement sur demande.
- `git add` avec la liste explicite des fichiers modifiés, jamais `git add -A` ni `commit -a` : Visual Studio modifie aussi le dépôt, et un changement qu'on n'a pas fait n'est pas à commiter sans accord.
- `git` n'est pas dans le PATH de PowerShell sur cette machine : utiliser celui de Visual Studio 2022 (`C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe`).
- Messages de commit en français ; jamais de nom de personne à la 3e personne dans le code, les tests ou ce fichier : écrire la raison, éventuellement datée.
- Tenir ce fichier à jour à chaque changement d'architecture ou de convention.

**Code**
- Pas de commentaires XML (`/// <summary>`) : commenter avec des `//` simples, dans le style des fichiers existants (en-tête de licence, puis description et arborescence en `├─` / `└─`).
- Zéro avertissement de compilation, y compris nullables.
- Une modification de la génération des coups, de la recherche ou de l'évaluation se mesure : perft pour les coups (valeurs de référence connues), `performance.cs` pour la vitesse, et un test de force avant de la garder.
- Éviter `dotnet build --no-incremental` : il recopie les fichiers de sortie (réseaux, DLL) et peut remettre une ancienne version.

## À vérifier avec Bruno

- `net8.0` n'est plus pris en charge depuis le 2026-11-10 ; BrunoGUI_GenII et BrunoGUI_Dames sont passés à `net10.0` (Visual Studio 2026 requis, VS 2022 ne le prend pas en charge). Migration non faite ici.
- Le dépôt n'a ni projet de tests, ni CI, ni `Directory.Build.props` : à ajouter plus tard si Bruno le veut, sur le modèle des deux autres projets.
