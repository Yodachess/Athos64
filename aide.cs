// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe d'affichage de l'Aide.
// └─ "Afficher" affiche l'écran d'aide avec les commandes disponibles

using System;
using System.Numerics;
using Athos64;

namespace Athos64
{
    public static class Aide
    {
        public static void Afficher(GestionFlux flux)
        {
            // --- SECTION FRANÇAISE ---
            flux.EcrireLigne("info string =======================================");
            flux.EcrireLigne("info string =   Athos64 - Commandes Disponibles   =");
            flux.EcrireLigne("info string =======================================");
            flux.EcrireLigne("info string   uci         : Affiche l'identification du moteur et ses options");
            flux.EcrireLigne("info string   setoption   : Définit une option (setoption name <nom> value <valeur>)");
            flux.EcrireLigne("info string                 Options : Threads (4), Depth (12, go sans limite de temps), Ponder (true), EvalFile (réseau NNUE)");
            flux.EcrireLigne("info string   position    : Définit la position (position [startpos | fen <fen>] [moves <coup1> ... <coupN>])");
            flux.EcrireLigne("info string   ucinewgame  : Initialise une nouvelle partie (vide la table de transposition)");
            flux.EcrireLigne("info string   isready     : Vérifie si le moteur est prêt");
            flux.EcrireLigne("info string   go          : Lance la recherche du meilleur coup");
            flux.EcrireLigne("info string                 go [depth <x>] [movetime <ms>] [infinite] [ponder]");
            flux.EcrireLigne("info string                    [wtime <ms>] [btime <ms>] [winc <ms>] [binc <ms>] [movestogo <x>]");
            flux.EcrireLigne("info string   stop        : Arrête la recherche en cours et affiche le meilleur coup trouvé");
            flux.EcrireLigne("info string   ponderhit   : Indique que le coup attendu (ponder) a été joué");
            flux.EcrireLigne("info string   perft [x]   : Compte les positions légales jusqu'à la profondeur x (5 par défaut)");
            flux.EcrireLigne("info string   bench [x]   : Benchmark sur une sélection de positions FEN, à la profondeur x (6 par défaut)");
            flux.EcrireLigne("info string   eval        : Identique à evalnnue");
            flux.EcrireLigne("info string   evalnnue    : Affiche le score NNUE de la position courante");
            flux.EcrireLigne("info string   evalnnuefen <fen> : Affiche le score NNUE de la position FEN donnée");
            flux.EcrireLigne("info string   flip        : Inverse l'affichage de l'échiquier");
            flux.EcrireLigne("info string   testsee     : Teste le Static Exchange Evaluation (SEE)");
            flux.EcrireLigne("info string   triple      : Teste la détection de la triple répétition dans une séquence de coups");
            flux.EcrireLigne("info string   50coups     : Teste la règle des 50 coups dans une séquence de coups");
            flux.EcrireLigne("info string   d           : Affiche l'échiquier dans la console");
            flux.EcrireLigne("info string   debug [on|off] : Active ou désactive le mode debug (sans argument : affiche l'état)");
            flux.EcrireLigne("info string   help, ?, /help, /? : Affiche cet écran d'aide");
            flux.EcrireLigne("info string   license     : Affiche les informations sur la licence");
            flux.EcrireLigne("info string   quit        : Ferme le programme proprement");

            // Écart visuel entre les deux langues
            flux.EcrireLigne("info string ");

            // --- ENGLISH SECTION ---
            flux.EcrireLigne("info string ====================================");
            flux.EcrireLigne("info string =   Athos64 - Available Commands   =");
            flux.EcrireLigne("info string ====================================");
            flux.EcrireLigne("info string   uci         : Displays the engine identification and its options");
            flux.EcrireLigne("info string   setoption   : Sets an option (setoption name <name> value <value>)");
            flux.EcrireLigne("info string                 Options: Threads (4), Depth (12, go without time limit), Ponder (true), EvalFile (NNUE network)");
            flux.EcrireLigne("info string   position    : Sets the position (position [startpos | fen <fen>] [moves <move1> ... <moveN>])");
            flux.EcrireLigne("info string   ucinewgame  : Starts a new game (clears the transposition table)");
            flux.EcrireLigne("info string   isready     : Checks if the engine is ready");
            flux.EcrireLigne("info string   go          : Starts searching for the best move");
            flux.EcrireLigne("info string                 go [depth <x>] [movetime <ms>] [infinite] [ponder]");
            flux.EcrireLigne("info string                    [wtime <ms>] [btime <ms>] [winc <ms>] [binc <ms>] [movestogo <x>]");
            flux.EcrireLigne("info string   stop        : Stops the current search and displays the best move found");
            flux.EcrireLigne("info string   ponderhit   : Indicates that the expected (ponder) move has been played");
            flux.EcrireLigne("info string   perft [x]   : Counts the legal positions up to depth x (default 5)");
            flux.EcrireLigne("info string   bench [x]   : Benchmark on a selection of FEN positions at depth x (default 6)");
            flux.EcrireLigne("info string   eval        : Same as evalnnue");
            flux.EcrireLigne("info string   evalnnue    : Displays the NNUE score of the current position");
            flux.EcrireLigne("info string   evalnnuefen <fen> : Displays the NNUE score of the given FEN position");
            flux.EcrireLigne("info string   flip        : Flips the chessboard display");
            flux.EcrireLigne("info string   testsee     : Tests the Static Exchange Evaluation (SEE)");
            flux.EcrireLigne("info string   triple      : Tests threefold repetition detection in a move sequence");
            flux.EcrireLigne("info string   50coups     : Tests the 50-move rule in a move sequence");
            flux.EcrireLigne("info string   d           : Displays the board in the console");
            flux.EcrireLigne("info string   debug [on|off] : Enables or disables debug mode (no argument: displays the state)");
            flux.EcrireLigne("info string   help, ?, /help, /? : Displays this help screen");
            flux.EcrireLigne("info string   license     : Displays license information");
            flux.EcrireLigne("info string   quit        : Closes the program cleanly");
            flux.EcrireLigne("info string ==============================================");
        }
    }
}
