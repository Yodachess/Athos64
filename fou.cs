// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe de gestion des mouvements de fou (Bishop) en utilisant des bitboards.
// ├─ "ObtenirAttaques" génère les cases attaquées par les fous
// ├─ "ObtenirMouvements" génère les mouvements légaux pour un fou
// └─ "AttaquesDepuis" génère les cases attaquées par un fou depuis une case de départ (rayons précalculés, rayons.cs)

using System.Numerics;

namespace Athos64
{
    public static class Fou
    {
        public static ulong ObtenirAttaques(ulong fous, ulong occupations)
        {   // Ici on veut juste les cases attaquées, pas de blocage par les pièces amies
            ulong attaques = 0UL;
            while (fous != 0)
            {
                int caseDepart = BitOperations.TrailingZeroCount(fous);
                attaques |= AttaquesDepuis(caseDepart, occupations, 0UL); // pas de notion d'ami ici
                fous &= fous - 1;
            }
            return attaques;
        }
        public static ulong ObtenirMouvements(ulong fou, ulong occupations, ulong occupationsAmies)
        {   // Ici on veut les cases atteignables, donc on bloque par les pièces amies
            int caseDepart = BitOperations.TrailingZeroCount(fou);
            return AttaquesDepuis(caseDepart, occupations, occupationsAmies);
        }
        private static ulong AttaquesDepuis(int caseDepart, ulong occupations, ulong occupationsAmies)
        {   // Les 4 diagonales par rayons précalculés (voir rayons.cs).
            // Une pièce amie bloque sans être ajoutée ; une pièce ennemie bloque et est ajoutée (capture).
            return Rayons.AttaquesFou(caseDepart, occupations | occupationsAmies) & ~occupationsAmies;
        }
    }
}