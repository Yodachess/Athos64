// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Commandes de diagnostic tapées à la main dans la console (hors protocole UCI).
// ├─ "Eval" affiche l'évaluation NNUE de la position actuelle (commandes "eval" et "evalnnue")
// ├─ "EvalFen" affiche l'évaluation NNUE d'une FEN donnée (commande "evalnnuefen")
// ├─ "Inverser" change le camp au trait (commande "flip")
// └─ "AfficherEchiquier" dessine l'échiquier dans la console (commande "d")

using System;
using System.Collections.Generic;
using System.Linq;

namespace Athos64
{
    public static class CommandesDiagnostic
    {
        public static void Eval(Echiquier echiquier)
        {   // Évalue la position actuelle avec le réseau neuronal NNUE
            if (!BrunoNNUE.Initialiser(ProtocoleUCI.NetworkFile))
            {
                Console.WriteLine("info string NNUE : réseaux indisponibles");
                return;
            }
            int valeurNNUE = BrunoNNUE.Evaluer(echiquier);
            int scoreNNUE = BrunoNNUE.ConvertirEnCpStockfish(valeurNNUE, echiquier);
            Console.WriteLine($"info string Evaluation NNUE : {scoreNNUE} cp");
        }

        public static void EvalFen(List<string> jetons)
        {   // Évalue une FEN fournie avec la commande : evalnnuefen <fen>
            if (!BrunoNNUE.Initialiser(ProtocoleUCI.NetworkFile))
            {
                Console.WriteLine("info string NNUE : réseaux indisponibles");
                return;
            }
            if (jetons.Count < 2)
            {
                Console.WriteLine("info string NNUE : FEN manquante");
                return;
            }
            string fen = string.Join(" ", jetons.Skip(1));
            int score = BrunoNNUE.NNUE_EvalFEN(fen);
            Console.WriteLine($"info string Evaluation NNUE à partir du FEN: {score} cp");
        }

        public static void Inverser(Echiquier echiquier)
        {   // Change le camp au trait sans jouer de coup
            echiquier.CoteBlanc = !echiquier.CoteBlanc;
            Console.WriteLine($"info string Trait inversé. Au tour des {(echiquier.CoteBlanc ? "Blancs" : "Noirs")}.");
        }

        public static void AfficherEchiquier(Echiquier echiquier)
        {   // Dessine l'échiquier dans la console (utile pour le débogage)
            Console.WriteLine($"\nEchiquier actuel :" +
                              $"\n------------------");
            echiquier.Afficher();
        }
    }
}
