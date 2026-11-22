// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe de test de performance.
// └─ "Executer" exécute le benchmark sur une série de positions FEN stratégiques pour mesurer les performances du moteur.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using static Athos64.Echiquier;


namespace Athos64
{
    public static class Performance
    {   // Une sélection de positions FEN stratégiques (Départ, Tactique, Fin de partie)
        private static readonly List<string> PositionsDeTest = new List<string>
    {
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",             // 1. Position de départ standard
        // 2. "KiwiPete" (Position ultra-connue pour tester le tri et les perfs)
        // "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
        "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",    // 3. Position avec beaucoup de tactique et de captures (Idéal pour la Quiescence)
        "r1bqk2r/pp2bppp/2n1pn2/3p4/3P4/2N1PN2/PP2BPPP/R1BQK2R w KQkq - 4 8",   // 4. Position de milieu de jeu complexe
        "8/k7/3p4/p2P1p2/P2P1P2/8/8/K7 w - - 0 1",  // 5. Finale de pions et pièces (pour tester les structures et la TT)
        // 5-man positions
        "8/8/8/8/5kp1/P7/8/1K1N4 w - - 0 1",     // Kc2 - mate
        "8/8/8/5N2/8/p7/8/2NK3k w - - 0 1",      // Na2 - mate
        "8/3k4/8/8/8/4B3/4KB2/2B5 w - - 0 1",    // draw
        // 6-man positions
        "8/8/1P6/5pr1/8/4R3/7k/2K5 w - - 0 1",   // Re5 - mate
        "8/2p4P/8/kr6/6R1/8/8/1K6 w - - 0 1",    // Ka2 - mate
        "8/8/3P3k/8/1p6/8/1P6/1K3n2 b - - 0 1",  // Nd2 - draw
        // 7-man positions
        "8/R7/2q5/8/6k1/8/1P5p/K6R w - - 0 124", // Draw
        // Mate and stalemate positions
        "6k1/3b3r/1p1p4/p1n2p2/1PPNpP1q/P3Q1p1/1R1RB1P1/5K2 b - - 0 1",
        "r2r1n2/pp2bk2/2p1p2p/3q4/3PN1QP/2P3R1/P4PP1/5RK1 w - - 0 1",
        "8/8/8/8/8/6k1/6p1/6K1 w - -",
        "7k/7P/6K1/8/3B4/8/8/8 b - -",
    };

        public static void Executer(int profondeurCible = 6)
        {
            Console.WriteLine($"[BENCH] Démarrage du benchmark (Profondeur: {profondeurCible})...");

            ulong noeudsTotaux = 0;
            Stopwatch swGlobal = new Stopwatch();
            var gestionTemps = new GestionTemps();

            // 1. On instancie UN SEUL échiquier de travail unique
            Echiquier echiquierDeTravail = new Echiquier();

            // On force le nettoyage de la mémoire avant le test pour des mesures fiables
            GC.Collect();
            GC.WaitForPendingFinalizers();

            swGlobal.Start();
            for (int i = 0; i < PositionsDeTest.Count; i++)
            {
                string fen = PositionsDeTest[i];
                Console.WriteLine($"[BENCH] Position {i + 1}/{PositionsDeTest.Count}...");
                // 1. Configurer l'échiquier avec la FEN
                ChargementFen.ChargerFen(echiquierDeTravail, fen, false);     // Cet appel affiche la position dans la console
                // 2. Réinitialise le compteur de nœuds de Negamax pour cette position
                // Recherche.NombreDeNoeuds = 0;
                // 3. Lance la recherche (fenêtre Alpha/Béta initiale), on utilise la racine distanceRacine = 0
                // 3. Lance la recherche avec un délégué vide pour ignorer les messages 'info' pendant le benchmark
                Recherche.Chercher(echiquierDeTravail, profondeurCible, gestionTemps, (message) => { /* Ne rien faire */ });
                noeudsTotaux += (ulong)Recherche.NombreDeNoeuds;
                // Extraction et affichage de la meilleure ligne (PV)
                string lignePrincipale = Recherche.ObtenirLignePV(echiquierDeTravail, profondeurCible);

                Console.WriteLine($"        -> Nœuds calculés : {Recherche.NombreDeNoeuds:N0}");
                Console.WriteLine($"        -> Meilleure ligne: {lignePrincipale}");
                // ======================================================================
            }
            swGlobal.Stop();

            long tempsMs = swGlobal.ElapsedMilliseconds;
            if (tempsMs == 0) tempsMs = 1;      // Évite la division par zéro si le moteur est flash
            // Calcul des Nœuds Par Seconde (NPS)
            ulong nps = (noeudsTotaux * 1000) / (ulong)tempsMs;

            // --- AFFICHAGE DU RÉSULTAT STYLE STOCKFISH ---
            Console.WriteLine("--------------------------------================---");
            Console.WriteLine($"Nœuds totaux : {noeudsTotaux:N0}");
            Console.WriteLine($"Temps total  : {tempsMs} ms");
            Console.WriteLine($"Performances : {nps:N0} nps");
            Console.WriteLine("--------------------------------================---");

            // Pour communiquer le résultat final à l'interface UCI
            Console.WriteLine($"{noeudsTotaux} nodes {tempsMs} time {nps} nps");
        }
    }
}
