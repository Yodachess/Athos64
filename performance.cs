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
using System.Diagnostics.Metrics;
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

        /// Exécute la suite de tests unitaires automatisée pour valider l'algorithme du SEE.
        public static void ExecuterSuiteTestsSEE()
        {
            Console.WriteLine("================================================");
            Console.WriteLine("🧪 Atgos64 - Test du Static Exchange Evaluation ");
            Console.WriteLine("================================================");

            // Instanciation de l'échiquier de Bruno
            Echiquier echiquierTest = new Echiquier();

            // Définition de la structure des cas de test
            var casDeTests = new[]
            {
                new {
                    Nom = "Test 1: Capture simple et rentable (Gain matériel net)",
                    Fen = "r1bxk2r/pppp1ppp/2n2n2/4p3/4P3/2N2N2/PPPP1PPP/R1BQKB1R w KQkq - 0 1",
                    CaseCible = 36, // Case e5
                    ValeurVictime = 100, // Pion noir (index 1 dans ValeursPieces = 100)
                    ValeurAttaquant = 320, // Cavalier blanc (index 2 dans ValeursPieces = 320)
                    Attendu = -220
                },
                new {
                    Nom = "Test 2: Le piège du sacrifice (Le Minimax inversé doit renoncer)",
                    Fen = "rnbqkbnr/ppp1pppp/8/3p4/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2",
                    CaseCible = 35, // Case d5
                    ValeurVictime = 100, // Pion noir
                    ValeurAttaquant = 100, // Pion blanc
                    Attendu = 0 // Les blancs doivent refuser l'échange perdant après le premier pli
                },
                new {
                    Nom = "Test 3: Attaque à Rayons X (X-Rays des Tours libérées)",
                    Fen = "7k/8/8/3r4/4P3/8/3R4/3R3K w - - 0 1",
                    CaseCible = 35, // Case d5
                    ValeurVictime = 500, // Tour noire
                    ValeurAttaquant = 100, // Pion blanc
                    Attendu = 500 // Le pion libère les deux Tours blanches derrière, gain de la tour net
                },
                new {
                    Nom = "Test 4: La batterie lourde complexe (Équilibre parfait)",
                    Fen = "2r2rk1/pp2q1pp/2n1p2b/2p1p3/R3P3/1Q1P1NP1/PP3PBP/3R2K1 w - - 0 1",
                    CaseCible = 36, // Case e5
                    ValeurVictime = 100, // Pion noir
                    ValeurAttaquant = 320, // Cavalier blanc
                    Attendu = -220 
                }
            };

            int succes = 0;

            // Boucle d'exécution des tests
            foreach (var test in casDeTests)
            {
                Console.WriteLine($"\n👉 {test.Nom}");

                try
                {
                    // 1. Chargement de la FEN sur l'échiquier. 
                    // Note : Adapte la méthode si ton chargeur FEN porte un autre nom (ex: echiquierTest.ChargerFen(test.Fen))
                    ChargementFen.ChargerFen(echiquierTest, test.Fen, false);

                    // 2. Exécution du calcul SEE de Bruno sur la case cible
                    int resultatObtenu = EchangeStatiqueEval.ObtenirSEE(
                        echiquierTest,
                        test.CaseCible,
                        test.ValeurVictime,
                        test.ValeurAttaquant
                    );

                    // 3. Affichage et comparaison des résultats
                    Console.WriteLine($"   FEN      : {test.Fen}");
                    Console.WriteLine($"   Case     : {test.CaseCible}");
                    Console.WriteLine($"   Attendu  : {test.Attendu}");
                    Console.WriteLine($"   Obtenu   : {resultatObtenu}");

                    if (resultatObtenu == test.Attendu)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("   Verdict  : [SUCCÈS] ✅");
                        succes++;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("   Verdict  : [ÉCHEC] ❌ (Erreur de logique Minimax ou Rayon-X)");
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"   Verdict  : [CRASH] 💥 L'évaluation a levé une exception : {ex.Message}");
                    Console.WriteLine($"   Stack    : {ex.StackTrace}");
                }

                // Réinitialisation de la couleur de la console
                Console.ResetColor();
            }

            // Bilan final
            Console.WriteLine("\n=====================================================================");
            Console.Write("📊 BILAN DU DIAGNOSTIC : ");
            if (succes == casDeTests.Length)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{succes} / {casDeTests.Length} TESTS RÉUSSIS. Le SEE est parfait ! 😎");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{succes} / {casDeTests.Length} TESTS RÉUSSIS. Il reste un bug sous la roche.");
            }
            Console.ResetColor();
            Console.WriteLine("=====================================================================");
        }
    }
}
