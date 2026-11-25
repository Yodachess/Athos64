// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe de gestion des mouvements de la tour (Rook) en utilisant des bitboards.
// ├─ "ObtenirAttaques" génère les cases attaquées par les tours
// ├─ "ObtenirMouvements" génère les mouvements légaux pour une tour
// └─ "AttaquesDepuis" génère les cases attaquées par une tour depuis une case de départ (rayons précalculés, rayons.cs)

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
        {   // Génère les cases attaquées par une tour depuis une case de départ, en tenant compte des occupations (pour les rayons bloqués).
            // Nord, Sud, Est et Ouest par rayons précalculés (voir rayons.cs).
            return Rayons.AttaquesTour(caseDepart, occupations);
        }
    }
}