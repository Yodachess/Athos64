// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe de gestion des mouvements du Roi (King) en utilisant des bitboards.
// ├─ "ObtenirAttaques" génère les cases attaquées par les rois
// └─ "ObtenirMouvements" génère les mouvements légaux pour un roi

using System;
using System.Numerics;
using Athos64;

namespace Athos64
{
    public static class Roi
    {   // Masques pour les déplacements du roi (1 case dans toutes les directions)
        private static readonly int[] Deplacements = { -9, -8, -7, -1, 1, 7, 8, 9 };

        // Cases voisines de chaque case, calculées une fois pour toutes
        // (l'ancien calcul à chaque appel allouait un tableau : vu au profileur, 2026-11-25)
        private static readonly ulong[] Voisines = InitialiserVoisines();

        private static ulong[] InitialiserVoisines()
        {
            var voisines = new ulong[64];
            for (int caseDepart = 0; caseDepart < 64; caseDepart++)
            {
                int ligneDepart = caseDepart / 8;
                int colDepart = caseDepart % 8;
                foreach (int deplacement in Deplacements)
                {
                    int caseArrivee = caseDepart + deplacement;
                    // Filtre : dans l'échiquier, et au plus 1 case dans chaque direction
                    if (caseArrivee >= 0 && caseArrivee < 64
                        && Math.Abs(caseArrivee / 8 - ligneDepart) <= 1 && Math.Abs(caseArrivee % 8 - colDepart) <= 1)
                    {
                        voisines[caseDepart] |= 1UL << caseArrivee;
                    }
                }
            }
            return voisines;
        }

        public static ulong ObtenirAttaques(ulong rois)
        {   // Génère les cases attaquées par les rois (1 case dans toutes les directions)
            ulong attaques = 0;
            while (rois != 0)
            {
                attaques |= Voisines[BitOperations.TrailingZeroCount(rois)];
                rois &= rois - 1;
            }
            return attaques;
        }

        public static ulong ObtenirMouvements(ulong roi, ulong occupationsAmies)
        {   // Génère les mouvements légaux pour un roi, retourne un Bitboard des cases accessibles.
            if (roi == 0)
                return 0UL;
            return Voisines[BitOperations.TrailingZeroCount(roi)] & ~occupationsAmies;
        }
    }
}