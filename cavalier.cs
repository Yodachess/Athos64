// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe de gestion des mouvements de cavalier (Knight) en utilisant des bitboards.
// ├─ "ObtenirAttaques" génère les cases attaquées par les cavaliers
// └─ "ObtenirMouvements" génère les mouvements légaux pour un cavalier

using System;
using System.Numerics;
using Athos64;

namespace Athos64
{
    public static class Cavalier
    {   // Masques pour les déplacements en "L" du cavalier
        private static readonly int[] Deplacements = { 15, 17, 10, -6, -15, -17, -10, 6 };
        public static ulong ObtenirAttaques(ulong cavalier)
        {   // On génère les attaques possibles pour un cavalier en fonction de sa position, sans tenir compte des pièces amies ou ennemies.
            ulong l = cavalier;
            return
                (l << 17 & ~Bitboard.ColonneA) |
                (l << 15 & ~Bitboard.ColonneH) |
                (l << 10 & ~(Bitboard.ColonneA | Bitboard.ColonneB)) |
                (l << 6 & ~(Bitboard.ColonneG | Bitboard.ColonneH)) |
                (l >> 17 & ~Bitboard.ColonneH) |
                (l >> 15 & ~Bitboard.ColonneA) |
                (l >> 10 & ~(Bitboard.ColonneG | Bitboard.ColonneH)) |
                (l >> 6 & ~(Bitboard.ColonneA | Bitboard.ColonneB));
        }

        public static ulong ObtenirMouvements(ulong cavalier, ulong occupationsAmies)
        {   //  On génère les mouvements possibles pour un cavalier en fonction de sa position et des pièces amies
            // (pour bloquer les cases occupées par nos propres pièces).
            ulong mouvements = 0UL;
            ulong positions = cavalier;
            while (positions != 0)
            {
                int caseDepart = BitOperations.TrailingZeroCount(positions);
                ulong from = 1UL << caseDepart;
                ulong attaques = ObtenirAttaques(from);

                // enlève les pièces amies
                attaques &= ~occupationsAmies;
                mouvements |= attaques;
                positions &= positions - 1;
            }
            return mouvements;
        }
    }
}