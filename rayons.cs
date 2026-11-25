// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Attaques des pièces glissantes par rayons précalculés (méthode « classique » de chessprogramming.org).
// Pour chaque case et chaque direction, le rayon complet jusqu'au bord est calculé une fois pour toutes.
// Les attaques sur un rayon s'obtiennent en retirant, après la première pièce rencontrée, la suite du rayon
// (qui est le rayon partant de cette pièce dans la même direction). Aucune boucle case par case.
// ├─ "AttaquesFou" cases attaquées par un fou depuis une case, avec ces occupations (la pièce bloquante est incluse)
// ├─ "AttaquesTour" cases attaquées par une tour depuis une case, avec ces occupations (la pièce bloquante est incluse)
// └─ "Attaques" attaques sur un rayon : premier bloqueur par le bit le plus bas (directions croissantes)
//                ou le plus haut (directions décroissantes)

using System.Numerics;

namespace Athos64
{
    public static class Rayons
    {
        // Directions : 0 Nord (+8), 1 Est (+1), 2 Nord-Est (+9), 3 Nord-Ouest (+7) : croissantes
        //              4 Sud (-8), 5 Ouest (-1), 6 Sud-Ouest (-9), 7 Sud-Est (-7) : décroissantes
        private static readonly int[] DeltaColonne = { 0, 1, 1, -1, 0, -1, -1, 1 };
        private static readonly int[] DeltaRangee = { 1, 0, 1, 1, -1, 0, -1, -1 };
        private static readonly ulong[] Rayon = InitialiserRayons();   // [direction * 64 + case]

        private static ulong[] InitialiserRayons()
        {
            var rayons = new ulong[8 * 64];
            for (int direction = 0; direction < 8; direction++)
            {
                for (int caseDepart = 0; caseDepart < 64; caseDepart++)
                {
                    ulong rayon = 0UL;
                    int colonne = caseDepart % 8 + DeltaColonne[direction];
                    int rangee = caseDepart / 8 + DeltaRangee[direction];
                    while (colonne >= 0 && colonne < 8 && rangee >= 0 && rangee < 8)
                    {
                        rayon |= 1UL << (rangee * 8 + colonne);
                        colonne += DeltaColonne[direction];
                        rangee += DeltaRangee[direction];
                    }
                    rayons[direction * 64 + caseDepart] = rayon;
                }
            }
            return rayons;
        }

        private static ulong Attaques(int direction, int caseDepart, ulong occupations)
        {
            ulong rayon = Rayon[direction * 64 + caseDepart];
            ulong bloqueurs = rayon & occupations;
            if (bloqueurs != 0)
            {
                int premier = direction < 4
                    ? BitOperations.TrailingZeroCount(bloqueurs)
                    : 63 - BitOperations.LeadingZeroCount(bloqueurs);
                rayon ^= Rayon[direction * 64 + premier];
            }
            return rayon;
        }

        public static ulong AttaquesFou(int caseDepart, ulong occupations)
            => Attaques(2, caseDepart, occupations) | Attaques(3, caseDepart, occupations)
             | Attaques(6, caseDepart, occupations) | Attaques(7, caseDepart, occupations);

        public static ulong AttaquesTour(int caseDepart, ulong occupations)
            => Attaques(0, caseDepart, occupations) | Attaques(1, caseDepart, occupations)
             | Attaques(4, caseDepart, occupations) | Attaques(5, caseDepart, occupations);
    }
}
