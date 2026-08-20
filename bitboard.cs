// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe de gestion des bitboards.
// ├─ Constructeur statique "Bitboard" pour initialiser les masques de lignes, colonnes et diagonales
// ├─ "EstCaseBlanche" Indique instantanément si un index de case (0 à 63) est une case blanche
// ├─ "CaseVersBitboard" Convertit un index de case (0 à 63) en un bitboard avec un seul bit activé
// ├─ "BitboardVersIndex" Convertit un bitboard avec un seul bit activé en index de case (0 à 63) 
// ├─ "CaseVersIndex" Convertit une case (ex: "e4") en index (0 à 63) 
// ├─ "IndexVersCase" Convertit un index (0 à 63) en une case (ex: "e4")
// ├─ "DecalerNord" Déplace un bitboard vers le nord (haut)
// ├─ "DecalerSud" Déplace un bitboard vers le sud (bas)
// ├─ "DecalerEst" Déplace un bitboard vers l'est (droite)
// ├─ "DecalerOuest" Déplace un bitboard vers l'ouest (gauche)
// ├─ "DecalerNordEst" Déplace un bitboard vers le nord-est (haut-droite)
// ├─ "DecalerNordOuest" Déplace un bitboard vers le nord-ouest (haut-gauche)
// ├─ "DecalerSudEst" Déplace un bitboard vers le sud-est (bas-droite)
// ├─ "DecalerSudOuest" Déplace un bitboard vers le sud-ouest (bas-gauche)
// ├─ "CalculerDiagonalePositive" Calcule le masque d'une diagonale positive (de A1 à H8)
// ├─ "CalculerDiagonaleNegative" Calcule le masque d'une diagonale negative (de H1 à A8)
// └─ "AfficherBitboard" Affiche un bitboard sous forme de grille 8x8 (pour le débogage)

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Athos64
{
    public enum Case
    {   // Enumération des cases de l'échiquier (a1 à h8), avec leurs indices (00 à 63).
        a1 = 00, b1 = 01, c1 = 02, d1 = 03, e1 = 04, f1 = 05, g1 = 06, h1 = 07,
        a2 = 08, b2 = 09, c2 = 10, d2 = 11, e2 = 12, f2 = 13, g2 = 14, h2 = 15,
        a3 = 16, b3 = 17, c3 = 18, d3 = 19, e3 = 20, f3 = 21, g3 = 22, h3 = 23,
        a4 = 24, b4 = 25, c4 = 26, d4 = 27, e4 = 28, f4 = 29, g4 = 30, h4 = 31,
        a5 = 32, b5 = 33, c5 = 34, d5 = 35, e5 = 36, f5 = 37, g5 = 38, h5 = 39,
        a6 = 40, b6 = 41, c6 = 42, d6 = 43, e6 = 44, f6 = 45, g6 = 46, h6 = 47,
        a7 = 48, b7 = 49, c7 = 50, d7 = 51, e7 = 52, f7 = 53, g7 = 54, h7 = 55,
        a8 = 56, b8 = 57, c8 = 58, d8 = 59, e8 = 60, f8 = 61, g8 = 62, h8 = 63
    }

    public static class Bitboard
    {   // Classe utilitaire pour manipuler les bitboards (représentation 64 bits d'un échiquier).

        // Masques pour extraire les lignes, colonnes et diagonales
        public static readonly ulong[] MasqueLigne = new ulong[8];
        public static readonly ulong[] MasqueColonne = new ulong[8];
        public static readonly ulong[] MasqueDiagonalePositif = new ulong[15]; // Diagonales de A1 à H8
        public static readonly ulong[] MasqueDiagonaleNegatif = new ulong[15]; // Diagonales de H1 à A8

        // Masques pour les lignes de promotion
        public const ulong LignePromotionBlancs = 0xFF00000000000000UL; // Ligne 8 (A8-H8)
        public const ulong LignePromotionNoirs = 0x00000000000000FFUL;  // Ligne 1 (A1-H1)

        // Masques pour les lignes de départ des pions
        public const ulong LigneDepartBlancs = 0x000000000000FF00UL; // Ligne 2 (A2-H2)
        public const ulong LigneDepartNoirs = 0x00FF000000000000UL;  // Ligne 7 (A7-H7)

        public const ulong CasesNoires = 0xAA55AA55AA55AA55UL;
        public const ulong CasesBlanches = 0x55AA55AA55AA55AAUL;

        public static ulong ColonneA = 0x0101010101010101;
        public static ulong ColonneB = 0x0202020202020202;
        public static ulong ColonneC = 0x0404040404040404;
        public static ulong ColonneD = 0x0808080808080808;
        public static ulong ColonneE = 0x1010101010101010;
        public static ulong ColonneF = 0x2020202020202020;
        public static ulong ColonneG = 0x4040404040404040;
        public static ulong ColonneH = 0x8080808080808080;
        public static ulong Rang1 = 0x00000000000000FF;
        public static ulong Rang2 = 0x000000000000FF00;
        public static ulong Rang3 = 0x0000000000FF0000;
        public static ulong Rang4 = 0x00000000FF000000;
        public static ulong Rang5 = 0x000000FF00000000;
        public static ulong Rang6 = 0x0000FF0000000000;
        public static ulong Rang7 = 0x00FF000000000000;
        public static ulong Rang8 = 0xFF00000000000000;

        public static readonly ulong[] MasquesVoisins = new ulong[8]
        {
            MasqueColonne[1],                       // Colonne A : voisins = B
            MasqueColonne[0] | MasqueColonne[2],    // Colonne B : voisins = A + C
            MasqueColonne[1] | MasqueColonne[3],    // Colonne C : voisins = B + D
            MasqueColonne[2] | MasqueColonne[4],    // Colonne D : voisins = C + E
            MasqueColonne[3] | MasqueColonne[5],    // Colonne E : voisins = D + F
            MasqueColonne[4] | MasqueColonne[6],    // Colonne F : voisins = E + G
            MasqueColonne[5] | MasqueColonne[7],    // Colonne G : voisins = F + H
            MasqueColonne[6]                        // Colonne H : voisins = G
        };
        static Bitboard()
        {   // Initialisation statique des masques
            for (int ligne = 0; ligne < 8; ligne++)
                MasqueLigne[ligne] = 0xFFUL << (ligne * 8);

            for (int colonne = 0; colonne < 8; colonne++)
            {
                ulong masque = 0;
                for (int i = 0; i < 8; i++)
                    masque |= 1UL << (i * 8 + colonne);
                MasqueColonne[colonne] = masque;
            }
            // Initialisation des masques de diagonales (simplifiée ici)
            for (int d = 0; d < 15; d++)
            {
                MasqueDiagonalePositif[d] = CalculerDiagonalePositive(d);
                MasqueDiagonaleNegatif[d] = CalculerDiagonaleNegative(d);
            }
        }

        // Indique instantanément si un index de case (0 à 63) est une case blanche
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EstCaseBlanche(int caseIndex)
        {
            return ((1UL << caseIndex) & CasesBlanches) != 0;
        }
        // Retourne un bitboard avec un seul bit à 1 pour la case spécifiée
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong CaseVersBitboard(int caseIndex)
        {   // Convertit un index de case en bitboard (ex: Bitboard.CaseVersBitboard(4) retourne 0x0000000000000010 pour E1).
            return 1UL << caseIndex;
        }
        public static int BitboardVersIndex(ulong bitboard)
        {   // Convertit un bitboard avec un seul bit à 1 en index de case (ex: Bitboard.BitboardVersIndex(0x0000000000000010) retourne 4 pour E1).
            return BitOperations.TrailingZeroCount(bitboard);
        }
        public static int CaseVersIndex(string caseNotation)
        {   // Convertit une case (ex: "e4") en index (0 à 63)
            int colonne = caseNotation[0] - 'a';
            int ligne = caseNotation[1] - '1';
            return ligne * 8 + colonne;
        }
        public static string IndexVersCase(int caseIndex)
        {   // Convertit un index (0 à 63) en notation algébrique (ex: "e4")
            char colonne = (char)('a' + (caseIndex % 8));
            char ligne = (char)('1' + (caseIndex / 8));
            return $"{colonne}{ligne}";
        }

        // Déplace un bitboard vers le nord (haut)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong DecalerNord(ulong bitboard) => bitboard << 8;

        // Déplace un bitboard vers le sud (bas)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong DecalerSud(ulong bitboard) => bitboard >> 8;

        // Déplace un bitboard vers l'est (droite)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong DecalerEst(ulong bitboard) => (bitboard & ~MasqueColonne[7]) << 1;

        // Déplace un bitboard vers l'ouest (gauche)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong DecalerOuest(ulong bitboard) => (bitboard & ~MasqueColonne[0]) >> 1;

        // Déplace un bitboard vers le nord-est
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong DecalerNordEst(ulong bitboard) => (bitboard & ~MasqueColonne[7]) << 9;

        // Déplace un bitboard vers le nord-ouest
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong DecalerNordOuest(ulong bitboard) => (bitboard & ~MasqueColonne[0]) << 7;

        // Déplace un bitboard vers le sud-est
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong DecalerSudEst(ulong bitboard) => (bitboard & ~MasqueColonne[7]) >> 7;

        // Déplace un bitboard vers le sud-ouest
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong DecalerSudOuest(ulong bitboard) => (bitboard & ~MasqueColonne[0]) >> 9;

        private static ulong CalculerDiagonalePositive(int index)
        {   // Calcule le masque d'une diagonale positive (de A1 à H8)
            ulong masque = 0;
            int start = Math.Max(0, index - 7);
            int end = Math.Min(7, index);
            for (int i = start; i <= end; i++)
                masque |= CaseVersBitboard(i * 8 + (index - i));
            return masque;
        }
        private static ulong CalculerDiagonaleNegative(int index)
        {   // Calcule le masque d'une diagonale négative (de H1 à A8)
            ulong masque = 0;
            int start = Math.Max(0, index - 7);
            int end = Math.Min(7, index);
            for (int i = start; i <= end; i++)
                masque |= CaseVersBitboard(i * 8 + (7 - (index - i)));
            return masque;
        }
        public static void AfficherBitboard(ulong bitboard)
        {   // Affiche un bitboard sous forme de grille 8x8 (pour le débogage)
            for (int ligne = 7; ligne >= 0; ligne--)
            {
                for (int colonne = 0; colonne < 8; colonne++)
                {
                    int caseIndex = ligne * 8 + colonne;
                    Console.Write(((bitboard & (1UL << caseIndex)) != 0) ? "1 " : "0 ");
                }
                Console.WriteLine();
            }
        }
    }
}