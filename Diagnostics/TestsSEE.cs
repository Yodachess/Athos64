// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Tests du Static Exchange Evaluation (SEE), lancés à la main depuis la console (commande "testsee").
// └─ "Executer" exécute la suite de tests et affiche le verdict de chaque cas puis le bilan

using System;

namespace Athos64
{
    public static class TestsSEE
    {
        // Exécute la suite de tests automatisée pour valider l'algorithme du SEE.
        public static void Executer()
        {
            Console.WriteLine("================================================");
            Console.WriteLine("🧪 Athos64 - Test du Static Exchange Evaluation ");
            Console.WriteLine("================================================");

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
                    ChargementFen.ChargerFen(echiquierTest, test.Fen, false);

                    // 2. Exécution du calcul SEE sur la case cible
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
