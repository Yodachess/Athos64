// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe (partie) de gestion de l'échiquier (perft et notation des coups).
// ├─ "Perft" effectue un test de performance en comptant tous les mouvements légaux jusqu'à une profondeur donnée
// ├─ "TestPerft" lancement du test à la profondeur donnée ou 5 si pas de profondeur spécifiée
// ├─ "DivisionPerft" effectue un test de performance et affiche le nombre de mouvements pour chaque coup initial
// ├─ "TraduireEnNotationAlgebraique" traduire un mouvement en notation algébrique (ex: e2e4, g1f3, e7e8q)
// └─ "IndiceVersAlgebrique" convertit un indice de case (0-63) en notation algébrique (ex: 0 -> a1, 63 -> h8).

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Athos64
{
    public partial class Echiquier
    {
        // ******************************************
        // Perft : compte le nombre de positions atteignables à une profondeur donnée.
        // https://www.chessprogramming.org/Perft_Results
        public ulong Perft(int profondeur)
        {
            if (profondeur == 0)
                return 1;

            ulong noeuds = 0;

            // On crée une liste locale pour cet étage de profondeur
            List<Mouvement> coups = new List<Mouvement>(100);
            GenererMouvementsLegauxCommun(coups); // On remplit la liste

            foreach (var coup in coups)
            {
                var etat = SauvegarderEtat();
                JouerCoup(coup.CaseDepart, coup.CaseArrivee, coup.Promotion);

                if (!RoiEnEchec(!CoteBlanc))
                {   // On vérifie si le roi de celui qui vient de jouer est en échec
                    noeuds += Perft(profondeur - 1);
                }
                RestaurerEtat(etat);
            }
            return noeuds;
        }
        public void TestPerft(List<string> jetons)
        {   // On extrait la profondeur : on regarde le jeton 1 (ex: "perft 5")
            // Si pas de jeton, on prend 5 par défaut.
            int profondeur = (jetons.Count > 1 && int.TryParse(jetons[1], out int p)) ? p : 5;

            Console.WriteLine($"--- Lancement Perft jusqu'à profondeur {profondeur} ---");

            for (int i = 1; i <= profondeur; i++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                ulong noeuds = Perft(i);
                sw.Stop();

                double secondes = sw.Elapsed.TotalSeconds;
                double nps = secondes > 0 ? noeuds / secondes : 0;

                Console.WriteLine($"Depth {i} = {noeuds} | Temps: {secondes:F3}s | NPS: {nps:N0}");
            }
        }
        public void DivisionPerft(int profondeur)
        {
            Console.WriteLine($"--- Division Perft (Profondeur {profondeur}) ---");
            ulong totalNoeuds = 0;

            // Même logique : on prépare la liste
            List<Mouvement> coups = new List<Mouvement>(100);
            GenererMouvementsLegauxCommun(coups);

            foreach (var coup in coups)
            {
                var etat = SauvegarderEtat();
                JouerCoup(coup.CaseDepart, coup.CaseArrivee, coup.Promotion);

                if (!RoiEnEchec(!CoteBlanc))
                {
                    ulong noeudsBranche = Perft(profondeur - 1);
                    totalNoeuds += noeudsBranche;
                    // Affichage du coup (ex: e2e4) et du nombre de sous-noeuds
                    string notationCoup = IndiceVersAlgebrique(coup.CaseDepart) + IndiceVersAlgebrique(coup.CaseArrivee);
                    Console.WriteLine($"{notationCoup}: {noeudsBranche}");
                }
                RestaurerEtat(etat);
            }
            Console.WriteLine($"\nTotal noeuds : {totalNoeuds}");
            Console.WriteLine("----------------------------------\n");
        }
        public string TraduireEnNotationAlgebraique(Mouvement m)
        {   // Si le mouvement est nul ou vide
            if (m.CaseDepart == 0 && m.CaseArrivee == 0) return "aucun";
 
            string depart = IndiceVersAlgebrique(m.CaseDepart);
            string arrivee = IndiceVersAlgebrique(m.CaseArrivee);
            // On ajoute la promotion si elle existe (ex: e7e8q)
            string promo = m.Promotion.HasValue ? m.Promotion.Value.ToString() : "";

            return depart + arrivee + promo;
        }

        public string IndiceVersAlgebrique(int indice)
        {
            int colonne = indice % 8;
            int rangee = indice / 8;
            return $"{(char)('a' + colonne)}{rangee + 1}";
        }
    }
}
