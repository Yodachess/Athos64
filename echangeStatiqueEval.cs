// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe pour l'Évaluation statique d'un échange.
// ├─ "ObtenirSEE" calcule le SEE (Static Exchange Evaluation) pour une case cible donnée
// ├─ "ChoisirPlusPetitAttaquant" identifie le plus petit attaquant d'une case cible
// └─ "ObtenirTousLesAttaquants" identifie tous les attaquants d'une case cible, en tenant compte des occupations

using System;
using System.Numerics;
using static Athos64.Echiquier;

namespace Athos64
{
    public static class EchangeStatiqueEval
    {
        public static int ObtenirSEE(Echiquier e, int caseCible, int pieceCaptureeValeur, int pieceAttaquanteValeur)
        {   // Objectif : Calculer le SEE (Static Exchange Evaluation) pour une case cible donnée.
            int[] gains = new int[32];
            int d = 0;

            // Pli 0 : On capture la pièce présente sur la case cible
            gains[0] = pieceCaptureeValeur;

            ulong toutesLesPieces = e.ObtenirToutesLesPieces();
            ulong piecesBlanches = e.ObtenirPiecesBlanches();
            ulong piecesNoires = e.ObtenirPiecesNoires();

            // Phase 1 : Identifier et retirer l'attaquant du Pli 0
            ulong attaquants = ObtenirTousLesAttaquants(e, caseCible, toutesLesPieces);
            ulong attaquantsDuCampInitial = e.CoteBlanc ? (attaquants & piecesBlanches) : (attaquants & piecesNoires);

            int caseAttaquanteInitiale = -1;
            ulong tempAttaquants = attaquantsDuCampInitial;
            while (tempAttaquants != 0)
            {
                int c = BitOperations.TrailingZeroCount(tempAttaquants);
                int v;
                ChoisirPlusPetitAttaquant(e, 1UL << c, out v);
                if (Math.Abs(v - pieceAttaquanteValeur) <= 30)
                {
                    caseAttaquanteInitiale = c;
                    break;
                }
                tempAttaquants &= tempAttaquants - 1;
            }

            if (caseAttaquanteInitiale == -1 && attaquantsDuCampInitial != 0)
                caseAttaquanteInitiale = BitOperations.TrailingZeroCount(attaquantsDuCampInitial);

            if (caseAttaquanteInitiale != -1)
            {
                ulong masqueEffacement = ~(1UL << caseAttaquanteInitiale);
                toutesLesPieces &= masqueEffacement;
                if (e.CoteBlanc) piecesBlanches &= masqueEffacement;
                else piecesNoires &= masqueEffacement;
            }

            // La pièce qui vient de capturer au Pli 0 devient la victime au Pli 1
            int valeurVictimeSuivante = pieceAttaquanteValeur;
            bool coteBlancActuel = !e.CoteBlanc;

            attaquants = ObtenirTousLesAttaquants(e, caseCible, toutesLesPieces);

            // Phase 2 : Simulation des captures (Plis 1+)
            while (true)
            {
                ulong attaquantsDuCamp = coteBlancActuel ? (attaquants & piecesBlanches) : (attaquants & piecesNoires);
                // string campStr = coteBlancActuel ? "BLANCS" : "NOIRS";

                if (attaquantsDuCamp == 0)
                {
                    break;
                }

                int valeurPieceAttaquante;
                int casePlusPetitAttaquant = ChoisirPlusPetitAttaquant(e, attaquantsDuCamp, out valeurPieceAttaquante);
                if (casePlusPetitAttaquant < 0 || casePlusPetitAttaquant >= 64) break;

                d++;
                if (d >= 31) break;

                // On stocke la valeur BRUTE de la pièce capturée
                gains[d] = valeurVictimeSuivante;

                valeurVictimeSuivante = valeurPieceAttaquante;

                ulong masqueRetrait = ~(1UL << casePlusPetitAttaquant);
                toutesLesPieces &= masqueRetrait;
                if (coteBlancActuel) piecesBlanches &= masqueRetrait;
                else piecesNoires &= masqueRetrait;

                coteBlancActuel = !coteBlancActuel;
                attaquants = ObtenirTousLesAttaquants(e, caseCible, toutesLesPieces);
            }

            // Phase 3 : Le Repli Minimax Standard du SEE Wiki
            while (d > 0)
            {
                // Formule historique : mon gain est réduit par ce que l'adversaire prend ensuite,
                // SAUF si l'adversaire a un gain négatif, auquel cas il préfère s'arrêter (Math.Max(0, ...))
                int reponseAdversaire = Math.Max(0, gains[d]);
                gains[d - 1] = gains[d - 1] - reponseAdversaire;
                d--;
            }
            return gains[0];
        }

        private static int ChoisirPlusPetitAttaquant(Echiquier e, ulong attaquantsDuCamp, out int valeurPiece)
        {
            ulong pions = attaquantsDuCamp & (e.PionsBlancs | e.PionsNoirs);
            if (pions != 0) { valeurPiece = EvalParams.ValeursMG[0]; return BitOperations.TrailingZeroCount(pions); }

            ulong cavaliers = attaquantsDuCamp & (e.CavaliersBlancs | e.CavaliersNoirs);
            if (cavaliers != 0) { valeurPiece = EvalParams.ValeursMG[1]; return BitOperations.TrailingZeroCount(cavaliers); }

            ulong fous = attaquantsDuCamp & (e.FousBlancs | e.FousNoirs);
            if (fous != 0) { valeurPiece = EvalParams.ValeursMG[2]; return BitOperations.TrailingZeroCount(fous); }

            ulong tours = attaquantsDuCamp & (e.ToursBlanches | e.ToursNoires);
            if (tours != 0) { valeurPiece = EvalParams.ValeursMG[3]; return BitOperations.TrailingZeroCount(tours); }

            ulong dames = attaquantsDuCamp & (e.ReineBlanche | e.ReineNoire);
            if (dames != 0) { valeurPiece = EvalParams.ValeursMG[4]; return BitOperations.TrailingZeroCount(dames); }

            ulong roi = attaquantsDuCamp & (e.RoiBlanc | e.RoiNoir);
            valeurPiece = 99999;
            return BitOperations.TrailingZeroCount(roi);
        }

        private static ulong ObtenirTousLesAttaquants(Echiquier e, int caseCible, ulong toutesLesPieces)
        {
            ulong attaquants = 0UL;
            ulong cibleBB = 1UL << caseCible;

            attaquants |= (((cibleBB >> 9) & ~Bitboard.ColonneH) & e.PionsBlancs) & toutesLesPieces;
            attaquants |= (((cibleBB >> 7) & ~Bitboard.ColonneA) & e.PionsBlancs) & toutesLesPieces;
            attaquants |= (((cibleBB << 7) & ~Bitboard.ColonneH) & e.PionsNoirs) & toutesLesPieces;
            attaquants |= (((cibleBB << 9) & ~Bitboard.ColonneA) & e.PionsNoirs) & toutesLesPieces;

            attaquants |= (Cavalier.ObtenirAttaques(cibleBB) & (e.CavaliersBlancs | e.CavaliersNoirs)) & toutesLesPieces;
            attaquants |= Fou.ObtenirAttaques(cibleBB, toutesLesPieces) & (e.FousBlancs | e.FousNoirs | e.ReineBlanche | e.ReineNoire);
            attaquants |= Tour.ObtenirAttaques(cibleBB, toutesLesPieces) & (e.ToursBlanches | e.ToursNoires | e.ReineBlanche | e.ReineNoire);
            attaquants |= (Roi.ObtenirAttaques(cibleBB) & (e.RoiBlanc | e.RoiNoir)) & toutesLesPieces;

            return attaquants;
        }
    }
}