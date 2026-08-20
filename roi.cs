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

        public static ulong ObtenirAttaques(ulong rois)
        {   // Génère les cases attaquées par les rois (1 case dans toutes les directions)  
            ulong attaques = 0;
            ulong masque = 1;

            for (int i = 0; i < 64; i++, masque <<= 1)
            {
                if ((rois & masque) != 0)
                {
                    int caseIndex = i;
                    int ligne = caseIndex / 8;
                    int colonne = caseIndex % 8;

                    // Les 8 mouvements possibles d'un roi
                    int[] deltas = { -9, -8, -7, -1, 1, 7, 8, 9 };

                    foreach (int delta in deltas)
                    {
                        int nouvelleCase = caseIndex + delta;
                        if (nouvelleCase >= 0 && nouvelleCase < 64)
                        {
                            int nouvelleLigne = nouvelleCase / 8;
                            int nouvelleColonne = nouvelleCase % 8;

                            // Vérifier si le mouvement est dans les limites de l'échiquier
                            if (Math.Abs(nouvelleLigne - ligne) <= 1 && Math.Abs(nouvelleColonne - colonne) <= 1)
                            {
                                attaques |= 1UL << nouvelleCase;
                            }
                        }
                    }
                }
            }
            return attaques;
        }

        public static ulong ObtenirMouvements(ulong roi, ulong occupationsAmies)
        {   // Génère les mouvements légaux pour un roi, retourne un Bitboard des cases accessibles.

            ulong mouvements = 0UL;
            int caseDepart = BitOperations.TrailingZeroCount(roi);

            int ligneDepart = caseDepart / 8;
            int colDepart = caseDepart % 8;

            foreach (int deplacement in Deplacements)
            {
                int caseArrivee = caseDepart + deplacement;

                if (caseArrivee >= 0 && caseArrivee < 64)
                {
                    int ligneArrivee = caseArrivee / 8;
                    int colArrivee = caseArrivee % 8;

                    // Filtre : max 1 case dans chaque direction
                    if (Math.Abs(ligneArrivee - ligneDepart) <= 1 &&
                        Math.Abs(colArrivee - colDepart) <= 1)
                    {
                        ulong masqueArrivee = Bitboard.CaseVersBitboard(caseArrivee);

                        if ((masqueArrivee & occupationsAmies) == 0)
                            mouvements |= masqueArrivee;
                    }
                }
            }

            return mouvements;
        }
    }
}