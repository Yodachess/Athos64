// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe de gestion des mouvements de la tour (Rook) en utilisant des bitboards.
// ├─ "ObtenirAttaques" génère les cases attaquées par les tours
// ├─ "ObtenirMouvements" génère les mouvements légaux pour une tour
// ├─ "AttaquesDepuis" génère les cases attaquées par une tour depuis une case de départ
// └─ "Rayon" génère les cases attaquées dans une direction donnée depuis une case de départ

using System.Numerics;

namespace Athos64
{
    public static class Tour
    {
        public static ulong ObtenirAttaques(ulong tours, ulong occupations)
        {   // Génère les cases attaquées par les tours (lignes et colonnes)
            ulong attaques = 0UL;
            while (tours != 0)
            {
                int caseDepart = BitOperations.TrailingZeroCount(tours);
                attaques |= AttaquesDepuis(caseDepart, occupations);
                tours &= tours - 1;
            }
            return attaques;
        }

        public static ulong ObtenirMouvements(ulong tour, ulong occupations, ulong occupationsAmies)
        {   // Génère les mouvements légaux pour une tour.
            ulong attaques = AttaquesDepuis(BitOperations.TrailingZeroCount(tour), occupations);
            // IMPORTANT : on garde uniquement les cases non amies
            return attaques & ~occupationsAmies;
        }

        private static ulong AttaquesDepuis(int caseDepart, ulong occupations)
        {   // Génère les cases attaquées par une tour depuis une case de départ, en tenant compte des occupations (pour les rayons bloqués)
            return
                Rayon(caseDepart, occupations, +8) |    // Nord
                Rayon(caseDepart, occupations, -8) |    // Sud  
                Rayon(caseDepart, occupations, +1) |    // Est
                Rayon(caseDepart, occupations, -1);     // Ouest
        }

        private static ulong Rayon(int caseDepart, ulong occupations, int delta)
        {   // Génère les cases attaquées dans une direction donnée (delta) depuis une case de départ,
            // en tenant compte des occupations (pour les rayons bloqués)
            ulong attaques = 0UL;
            int caseActuelle = caseDepart;
            while (true)
            {
                int suivante = caseActuelle + delta;
                if (suivante < 0 || suivante >= 64)
                    break;
                // anti wrap uniquement pour Est / Ouest
                if (delta == 1 || delta == -1)
                {
                    int colActuelle = caseActuelle % 8;
                    int colSuivante = suivante % 8;

                    if (Math.Abs(colSuivante - colActuelle) != 1)
                        break;
                }
                ulong masque = 1UL << suivante;
                attaques |= masque;
                if ((masque & occupations) != 0)
                    break;
                caseActuelle = suivante;
            }
            return attaques;
        }
    }
}