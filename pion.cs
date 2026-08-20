// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe de gestion des mouvements des pions (Pawn) en utilisant des bitboards.
// ├─ "ObtenirAttaquesBlanches" génère les cases attaquées par les pions blancs
// ├─ "ObtenirAttaquesNoires" génère les cases attaquées par les pions noirs
// ├─ "ObtenirMouvementsBlancs" génère les mouvements légaux pour les pions blancs
// ├─ "ObtenirMouvementsNoirs" génère les mouvements légaux pour les pions noirs
// ├─ "ObtenirPrisesEnPassantBlancs" génère les prises en passant pour les pions blancs
// └─ "ObtenirPrisesEnPassantNoirs" génère les prises en passant pour les pions noirs

using System;
using Athos64;

namespace Athos64
{
    /* Gère les mouvements et captures des pions (blancs et noirs).
    Bitboards : Chaque type de pièce est représenté par un ulong, ce qui permet des opérations rapides.
    Mouvements des pions : Les pions blancs avancent vers le nord, les noirs vers le sud.
    Promotion : Vérifiée avec ObtenirPionsPromouvables.
    Prise en passant : Gérée séparément avec ObtenirPrisesEnPassantBlancs/Noirs.
    */
    public static class Pion
    {
        private const ulong LigneDepartBlancs = 0x000000000000FF00UL;
        private const ulong LigneDepartNoirs = 0x00FF000000000000UL;
        public const ulong LignePromotionBlancs = 0xFF00000000000000UL;
        public const ulong LignePromotionNoirs = 0x00000000000000FFUL;

        // --- CORRECTION DES MASQUES D'ATTAQUE ---
        // Nord-Ouest (+7) : ne doit pas venir de la colonne A
        // Nord-Est (+9) : ne doit pas venir de la colonne H
        public static ulong ObtenirAttaquesBlanches(ulong pionsBlancs)
            => (pionsBlancs << 7 & ~Bitboard.ColonneH) | (pionsBlancs << 9 & ~Bitboard.ColonneA);

        // Sud-Est (-7) : ne doit pas venir de la colonne A
        // Sud-Ouest (-9) : ne doit pas venir de la colonne H
        public static ulong ObtenirAttaquesNoires(ulong pionsNoirs)
            => (pionsNoirs >> 7 & ~Bitboard.ColonneA) | (pionsNoirs >> 9 & ~Bitboard.ColonneH);

        public static ulong ObtenirMouvementsBlancs(ulong pionsBlancs, ulong occupations, ulong ennemies)
        {   // On génère les mouvements possibles pour les pions blancs en fonction de leur position, des cases occupées et des ennemies.
            ulong mouvements = 0UL;

            // 1. Avance d'une case
            ulong avanceUneCase = (pionsBlancs << 8) & ~occupations;
            mouvements |= avanceUneCase;
            // 2. Double poussée (seulement si la case intermédiaire est vide)
            ulong pionsDepart = pionsBlancs & LigneDepartBlancs;
            ulong avanceDeuxCases = ((pionsDepart << 8) & ~occupations) << 8 & ~occupations;
            mouvements |= avanceDeuxCases;
            // 3. Captures (Masques corrigés pour correspondre aux attaques)
            ulong capturesNordOuest = (pionsBlancs << 7) & ~Bitboard.ColonneH & ennemies;
            ulong capturesNordEst = (pionsBlancs << 9) & ~Bitboard.ColonneA & ennemies;
            mouvements |= capturesNordOuest | capturesNordEst;

            return mouvements;
        }

        public static ulong ObtenirMouvementsNoirs(ulong pionsNoirs, ulong occupations, ulong ennemies)
        {   // On génère les mouvements possibles pour les pions noirs en fonction de leur position, des cases occupées et des ennemies.
            ulong mouvements = 0UL;

            // 1. Avance d'une case
            ulong avanceUneCase = (pionsNoirs >> 8) & ~occupations;
            mouvements |= avanceUneCase;
            // 2. Double poussée
            ulong pionsDepart = pionsNoirs & LigneDepartNoirs;
            ulong avanceDeuxCases = ((pionsDepart >> 8) & ~occupations) >> 8 & ~occupations;
            mouvements |= avanceDeuxCases;
            // 3. Captures
            ulong capturesSudEst = (pionsNoirs >> 7) & ~Bitboard.ColonneA & ennemies;
            ulong capturesSudOuest = (pionsNoirs >> 9) & ~Bitboard.ColonneH & ennemies;
            mouvements |= capturesSudEst | capturesSudOuest;

            return mouvements;
        }

        public static ulong ObtenirPrisesEnPassantBlancs(ulong pionsBlancs, int caseEnPassant)
        {   // Si aucune case d'en passant n'est disponible, on retourne 0
            if (caseEnPassant == -1) return 0UL;
            ulong ep = 1UL << caseEnPassant;
            // On utilise les mêmes décalages et masques que pour les captures normales
            return ((pionsBlancs << 7) & ~Bitboard.ColonneH & ep)
                 | ((pionsBlancs << 9) & ~Bitboard.ColonneA & ep);
        }

        public static ulong ObtenirPrisesEnPassantNoirs(ulong pionsNoirs, int caseEnPassant)
        {   // Si aucune case d'en passant n'est disponible, on retourne 0
            if (caseEnPassant == -1) return 0UL;
            ulong ep = 1UL << caseEnPassant;
            return ((pionsNoirs >> 7) & ~Bitboard.ColonneA & ep)
                 | ((pionsNoirs >> 9) & ~Bitboard.ColonneH & ep);
        }
    }
}