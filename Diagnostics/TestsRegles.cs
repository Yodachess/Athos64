// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Tests des règles du jeu, lancés à la main depuis la console (commandes "triple" et "50coups").
// ├─ "Repetition" vérifie que la triple répétition est vue par la recherche (score 0)
// └─ "CinquanteCoups" vérifie que le compteur de 50 coups à 100 donne une nulle (score 0)

using System;

namespace Athos64
{
    public static class TestsRegles
    {
        public static void Repetition()
        {   // Teste la détection de la répétition triple dans une séquence de coups
            Echiquier e = new();
            ChargementFen.ChargerFen(e, Echiquier.FenDepart, true);  // DemiCoupActuel passe à 0
            // --- SÉQUENCE DE COUPS (Allers-retours de Cavaliers) ---
            e.JouerCoup(6, 21);     // 1. Nf3 (g1 -> f3)
            e.JouerCoup(62, 45);    // 1... Nf6 (g8 -> f6)
            e.JouerCoup(21, 6);     // 2. Ng1 (f3 -> g1) - Retour Blancs (2ème apparition position initiale)
            e.JouerCoup(45, 62);    // 2... Ng8 (f6 -> g8) - Retour Noirs
            e.JouerCoup(6, 21);     // 3. Nf3 (g1 -> f3)
            e.JouerCoup(62, 45);    // 3... Nf6 (g8 -> f6)
            // 4. Ng1 (f3 -> g1) - Ce coup va provoquer la TRIPLE répétition au pli suivant
            var etatPrecedent = e.JouerCoup(21, 6);
            // On appelle Negamax en simulant un nœud enfant (distanceRacine = 1)
            // On s'attend à ce qu'il voie la répétition et coupe immédiatement à 0
            int score = Recherche.Negamax(e, profondeur: 2, distanceRacine: 1, alpha: -10000, beta: 10000);
            e.AnnulerCoup(etatPrecedent);
            if (score == 0)
            {   // Affichage du verdict dans la console
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" [OK] Test Répétition : Negamax a bien renvoyé 0 (Nulle).");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($" [ÉCHEC] Test Répétition : Negamax a renvoyé {score} au lieu de 0.");
            }
            Console.ResetColor();
        }

        public static void CinquanteCoups(Echiquier e)
        {   // Teste la règle des 50 coups dans une séquence de coups
            Console.WriteLine("\n--- TEST UNITAIRE : RÈGLE DES 50 COUPS ---");
            // On charge un FEN où le compteur (le 5ème champ "99") dit qu'on est à 99 demi-coups sans capture ni pion
            ChargementFen.ChargerFen(e, "8/8/8/8/4k3/8/8/4K3 w - - 99 75", true);
            Console.WriteLine($"[DEBUG] Compteur après FEN = {e.RegleDes50Coups}"); // Doit afficher 99
            // Roi blanc bouge : e1 (4) -> f1 (5). Ce coup fait passer le compteur à 100 !
            var etatPrecedent = e.JouerCoup(4, 5);
            // On appelle Negamax au pli enfant (distanceRacine = 1)
            int score = Recherche.Negamax(e, 2, 1, -10000, 10000);
            // On nettoie le coup de test
            e.AnnulerCoup(etatPrecedent);
            if (score == 0)     // Résultat
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" [OK] Test 50 Coups : Le compteur à 100 (50 coups complets) renvoie bien 0.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($" [ÉCHEC] Test 50 Coups : Score de {score} au lieu de 0.");
            }
            Console.ResetColor();
        }
    }
}
