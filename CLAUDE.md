# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Projet

Athos64 : moteur d'échecs **UCI en C#**, 64 bits (bitboards), licence GPL-3.0-or-later. Il évalue les positions avec le **réseau de neurones NNUE de Stockfish 19** (`nn-1a298aa575a0.nnue` : un seul réseau, le petit réseau n'existe plus depuis SF19 ; il n'est pas dans git, à copier à côté de l'exe), appelé par `BrunoNNUE.cs` qui fait le pont vers `BrunoNNUE.dll` (DLL native construite à partir de https://github.com/VedantJoshi1409/stockfish_nnue_probe, mise à niveau avec le code et le réseau de Stockfish 19 : sources dans `BrunoNNUE-SF19\` (versionnées depuis le 2026-11-24, voir son `LISEZMOI.txt` ; construction : `MSBuild.exe BrunoNNUE.vcxproj /p:Configuration=Release /p:Platform=x64`), compilées avec VS 2022 en AVX2, avec un contexte incrémental par thread de recherche : position Stockfish + pile d'accumulateurs, mis à jour par JouerCoup/JouerCoupNul/Annuler dans echerche.cs, évaluation complète en repli ; l'ancien projet `BrunoNNUE-SF18` est conservé). Cadres du projet : `Athos64.csproj` (`net10.0`, `Nullable` activé, `AllowUnsafeBlocks`), `Athos64.sln`. `BrunoNNUE.dll` est copiée à côté de l'exe (`PreserveNewest`) ; le `.nnue` est à y placer à la main.

Tout le code, les identifiants et les commentaires sont en français : garder cette convention.

## Particularités voulues (ne pas les « corriger »)

- Depuis la position initiale, `go` répond tout de suite (profondeur 1, 0 nœud) avec un premier coup qui change d'un lancement à l'autre : c'est voulu, pour varier les parties (artificiel mais plus agréable).
- La recherche n'est pas reproductible en général (nœuds du `bench` différents d'un lancement à l'autre, premier coup aléatoire depuis la position initiale, threads multiples) : le perft sert de référence pour les coups. Exception, vérifiée le 2026-11-24 : avec `Threads` à 1 et `go depth N` depuis une position FEN (pas la position initiale), les nœuds, le coup et le score sont identiques d'un lancement à l'autre. `Diagnostics\comparer_recherche.ps1 -Dossier <dossier de l'exe> -Profondeur 11` en tire 10 positions : deux versions qui donnent les mêmes lignes ont exactement le même comportement (preuve pour un refactoring), et le total donne les nps (bruit d'environ 5 %).

- L'option `Depth` (12 par défaut) ne limite que les `go` sans limite de temps (2026-11-25) : avec `wtime`/`btime`, `movetime` ou `infinite`, c'est le temps qui arrête la recherche (plafond 64) ; `go depth N` reste prioritaire. Les limites d'extension d'échec et de quiescence se règlent sur la profondeur demandée, au plus 20.
- La table de transposition est gardée d'un coup à l'autre et vidée seulement à `ucinewgame` (2026-11-25) ; jamais de coupure par la table à la racine (sinon l'itération finit sans PV et un coup obsolète pouvait être joué).
- Essais de force rejetés (match de 200 parties, à ne pas refaire tel quel) : IIR (réduction d'un demi-coup sans coup de la table, profondeur >= 4) : -5 +/- 30 Elo (2026-11-25) ; malus d'historique (-profondeur² aux coups calmes essayés avant la coupure, mise à jour à gravité bornée à 50000, historique négatif utilisé au tri) : +3 +/- 28 Elo (2026-11-25) ; late move pruning (hors PV et hors échec, profondeur <= 4, coups calmes après le (3 + profondeur²)ᵉ) : -149 +/- 35 Elo (2026-11-26), probablement parce que les coups calmes sans historique ne sont pas départagés au tri.

## Pratiques (reprises de BrunoGUI_GenII et BrunoGUI_Dames)

**Manière de travailler**
- Avant de compiler : vérifier que l'exe du moteur ne tourne pas (un processus `Athos64` ouvert verrouille `bin`), sinon demander de le fermer. Ne jamais lancer le moteur ni une interface qui le pilote sans prévenir.
- Commit seulement après un essai ; après chaque commit, pousser sans demander (autorisation permanente de Bruno, 2026-11-22), jamais en force ; après un push, vérifier la CI toi-même (attendre la fin) et donner le résultat ; version publiée ou tag seulement sur demande.
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

- Migration vers `net10.0` faite le 2026-11-23 (`net8.0` n'est plus pris en charge depuis le 2026-11-10) : Visual Studio 2026 requis, VS 2022 ne prend pas en charge `net10.0`.
- CI GitHub ajoutée le 2026-11-24 (`.github\workflows\tests.yml` : compilation et `dotnet test Tests` à chaque push, sans le réseau NNUE). `Directory.Build.props` (2026-11-24) transforme les avertissements en erreurs (sauf les alertes de sécurité NuGet NU1901 à NU1904) : un avertissement fait échouer la compilation et la CI. `.editorconfig` fixe l'encodage, l'indentation et le style des accolades, pas les fins de ligne (`.gitattributes`).
- `gh` n'est pas installé : pour lire le résultat de la CI, interroger l'API publique `https://api.github.com/repos/Yodachess/Athos64/actions/runs` (`Invoke-RestMethod`).

## Commandes

```powershell
dotnet build Athos64.sln -c Release -p:Platform=x64
```

```powershell
dotnet test Tests
```

- `Tests\` (xUnit, `Athos64.Tests.csproj`, 36 tests, ~70 s) : perft de référence (position initiale, Kiwipete, positions 3 à 6 de chessprogramming.org), SEE, triple répétition, règle des 50 coups, cohérence de la clé Zobrist. Pas de test de la recherche ni de l'évaluation : la recherche n'est pas reproductible, seul le perft sert de référence. Les tests ne s'exécutent jamais en parallèle (état statique du moteur).
- Les commandes console `perft`, `bench`, `testsee`, `triple`, `50coups`, `eval`, `d` (dossier `Diagnostics\`) restent pour les essais à la main ; leurs vérifications sont reprises dans `Tests\`.
- Test de force (2026-11-25) : `Diagnostics\match.ps1 -Nouveau <dossier> -Reference <dossier> -Sortie <chemin>` joue un match cutechess-cli (installé dans `C:\Program Files (x86)\Cute Chess`) entre deux copies du dossier de l'exe (avec le `.nnue`), 200 parties en 10+0.1, 1 thread, `Depth` 20, sur les 40 ouvertures de `Diagnostics\ouvertures.pgn` jouées des deux couleurs (~35 min). Un changement qui modifie la recherche se garde si le match est gagné nettement (Elo et marge donnés par cutechess).
- Le moteur cherche les réseaux `.nnue` dans le dossier courant : lancer l'exe depuis son dossier.
- L'évaluation est celle du réseau NNUE seul. L'ancienne évaluation personnelle (pions, mobilité, sécurité du roi, scores MG/EG et phase mis à jour à chaque coup) a été retirée le 2026-11-22 ; il ne reste que `EvalParams.ValeursMG` (valeurs des pièces du SEE et du tri des captures). Elle reste dans l'historique git.
- La compilation est à zéro avertissement (2026-11-22) : garder cet état.
