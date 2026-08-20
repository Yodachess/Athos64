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
            flux.EcrireLigne("info string   uci         : Affiche les informations d'identification du moteur d'échecs");
            flux.EcrireLigne("info string   setoption   : Définit une option (setoption name [value ])");
            flux.EcrireLigne("info string   position    : Définit la position (position [fen <fenstring>] | startpos  moves <move1> ... <movei>)");
            flux.EcrireLigne("info string   ucinewgame  : Initialise une nouvelle partie");
            flux.EcrireLigne("info string   isready     : Vérifie si le moteur est prêt");
            flux.EcrireLigne("info string   go          : Lance la recherche du meilleur coup (go [depth <x>] [movetime <x>] [infinite])");
            flux.EcrireLigne("info string   stop        : Arrête la recherche en cours et affiche le meilleur coup trouvé");
            flux.EcrireLigne("info string   ponderhit   : Indique que le coup de réflexion a été joué");
            flux.EcrireLigne("info string   perft [x]   : Compte le nombre de positions légales à partir de la position actuelle jusqu'à une profondeur x");
            flux.EcrireLigne("info string   bench       : Execute un benchmark sur une sélection de positions FEN stratégiques");
            flux.EcrireLigne("info string   eval        : Affiche le score classique et le score NNUE pour la position courante");
            flux.EcrireLigne("info string   evalnnue    : Affiche le score NNUE à partir de la position courante");
            flux.EcrireLigne("info string   evalnnuefen : Affiche le score NNUE à partir d'une position FEN donnée");
            flux.EcrireLigne("info string   flip        : Inverse l'affichage de l'échiquier");
            flux.EcrireLigne("info string   optim       : Optimisation Texel avec fichiers .epd");
            flux.EcrireLigne("info string   testsee     : Test du static exchange evaluation (SEE)");
            flux.EcrireLigne("info string   triple      : Teste la détection de la répétition triple dans une séquence de coups");
            flux.EcrireLigne("info string   50coups     : Teste la règle des 50 coups dans une séquence de coups");
            flux.EcrireLigne("info string   d           : Affiche une représentation de la console");
            flux.EcrireLigne("info string   debug       : Active ou désactive le mode debug");
            flux.EcrireLigne("info string   ? / help    : Affiche cet écran d'aide");
            flux.EcrireLigne("info string   license     : Affiche les informations sur la licence");
            flux.EcrireLigne("info string   quit        : Ferme le programme proprement");

            // Écart visuel entre les deux langues
            flux.EcrireLigne("info string ");

            // --- ENGLISH SECTION ---
            flux.EcrireLigne("info string ====================================");
            flux.EcrireLigne("info string =   Athos64 - Available Commands   =");
            flux.EcrireLigne("info string ====================================");
            flux.EcrireLigne("info string   uci         : Displays chess engine identification information");
            flux.EcrireLigne("info string   setoption   : Sets an option (setoption name [value ])");
            flux.EcrireLigne("info string   position    : Sets the position (position [fen <fenstring>] | startpos  moves <move1> ... <movei>)");
            flux.EcrireLigne("info string   ucinewgame  : Initializes a new game");
            flux.EcrireLigne("info string   isready     : Checks if the engine is ready");
            flux.EcrireLigne("info string   go          : Starts searching for the best move (go [depth <x>] [movetime <x>] [infinite])");
            flux.EcrireLigne("info string   stop        : Stops the current search and displays the best move found");
            flux.EcrireLigne("info string   ponderhit   : Indicates that the ponder move has been played");
            flux.EcrireLigne("info string   perft [x]   : Counts the number of legal positions from the current position up to depth x");
            flux.EcrireLigne("info string   bench       : Runs a benchmark on a selection of strategic FEN positions");
            flux.EcrireLigne("info string   eval        : Displays the engine weights and score for the current position");
            flux.EcrireLigne("info string   evalnnue    : Displays the NNUE score from the current position");
            flux.EcrireLigne("info string   evalnnuefen : Displays the NNUE score from a given FEN position");
            flux.EcrireLigne("info string   flip        : Flips the chessboard display");
            flux.EcrireLigne("info string   optim       : Texel optimization using .epd files");
            flux.EcrireLigne("info string   testsee     : Test of Static Exchange Evaluation (SEE)");
            flux.EcrireLigne("info string   triple      : Tests threefold repetition detection in a move sequence");
            flux.EcrireLigne("info string   50coups     : Tests the 50-move rule in a move sequence");
            flux.EcrireLigne("info string   d           : Displays a representation of the board in console");
            flux.EcrireLigne("info string   debug       : Enables or disables debug mode");
            flux.EcrireLigne("info string   ? / help    : Displays this help screen");
            flux.EcrireLigne("info string   license     : Displays license information");
            flux.EcrireLigne("info string   quit        : Closes the program cleanly");
            flux.EcrireLigne("info string ==============================================");
        }
    }    
}