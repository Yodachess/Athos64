// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe (partie) de gestion de l'échiquier (clouages).
// ├─ "ObtenirPiecesClouees" retourne un bitboard des pièces du camp spécifié qui sont clouées au roi
// ├─ "DetecterClouageDirection" détecte si une pièce est clouée dans une direction donnée par rapport au roi
// └─ "EstPieceGlissanteValide" vérifie si une pièce glissante (fou, tour, reine) peut attaquer une case donnée.

using System;
using System.Collections.Generic;
using System.Numerics;

namespace Athos64
{
    public partial class Echiquier
    {
        #region Clouages
        public ulong ObtenirPiecesClouees(bool coteBlanc)
        {   // Retourne un bitboard des pièces du camp spécifié qui sont clouées au roi.
            ulong roi = coteBlanc ? RoiBlanc : RoiNoir;
            ulong piecesAmies = coteBlanc ? ObtenirPiecesBlanches() : ObtenirPiecesNoires();
            ulong piecesEnnemies = coteBlanc ? ObtenirPiecesNoires() : ObtenirPiecesBlanches();

            int caseRoi = BitOperations.TrailingZeroCount(roi);
            ulong clouees = 0UL;
            int[] directions = [8, -8, 1, -1, 9, -9, 7, -7];

            foreach (int dir in directions)
            {
                clouees |= DetecterClouageDirection(caseRoi, dir, piecesAmies, piecesEnnemies, coteBlanc);
            }

            return clouees;
        }
        private ulong DetecterClouageDirection(int caseRoi, int dir, ulong piecesAmies, ulong piecesEnnemies, bool coteBlanc)
        {   // Parcourt la direction spécifiée à partir du roi pour détecter une pièce clouée.
            int caseActuelle = caseRoi;
            ulong pieceCandidate = 0UL;

            while (true)
            {
                int suivante = caseActuelle + dir;

                if (suivante < 0 || suivante >= 64)
                    break;

                // anti-wrap
                if (dir == 1 || dir == -1 || dir == 7 || dir == -7 || dir == 9 || dir == -9)
                {
                    int colActuelle = caseActuelle % 8;
                    int colSuivante = suivante % 8;

                    if (Math.Abs(colSuivante - colActuelle) > 1)
                        break;
                }

                ulong masque = 1UL << suivante;

                // pièce amie
                if ((masque & piecesAmies) != 0)
                {
                    if (pieceCandidate != 0)
                        break;

                    pieceCandidate = masque;
                }
                // pièce ennemie
                else if ((masque & piecesEnnemies) != 0)
                {
                    if (pieceCandidate == 0)
                        break;

                    if (EstPieceGlissanteValide(masque, dir, coteBlanc))
                        return pieceCandidate;

                    break;
                }

                caseActuelle = suivante;
            }

            return 0UL;
        }
        private bool EstPieceGlissanteValide(ulong piece, int dir, bool coteBlanc)
        {   // Vérifie si la pièce ennemie rencontrée est un fou, une tour ou une reine qui peut clouer dans cette direction.
            ulong fous = coteBlanc ? FousNoirs : FousBlancs;
            ulong tours = coteBlanc ? ToursNoires : ToursBlanches;
            ulong reines = coteBlanc ? ReineNoire : ReineBlanche;

            bool diagonal = (dir == 7 || dir == -7 || dir == 9 || dir == -9);
            bool orthogonal = (dir == 1 || dir == -1 || dir == 8 || dir == -8);

            if (diagonal && ((piece & (fous | reines)) != 0))
                return true;

            if (orthogonal && ((piece & (tours | reines)) != 0))
                return true;

            return false;
        }
        #endregion
    }
}
