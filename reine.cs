// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe de gestion des mouvements de la Reine (Queen) en utilisant des bitboards.
// ├─ "ObtenirAttaques" génère les cases attaquées par les reines
// └─ "ObtenirMouvements" génère les mouvements légaux pour une reine

using System;
using System.Numerics;
using Athos64;

namespace Athos64
{
    public static class Reine
    {
        // Génère les mouvements légaux pour une reine (combinaison de la tour et du fou).
        // retourne un Bitboard des cases accessibles.
        public static ulong ObtenirMouvements(ulong reine, ulong occupations, ulong occupationsAmies)
        {   // Génère les mouvements légaux pour une reine (combinaison de la tour et du fou).
            return (Tour.ObtenirMouvements(reine, occupations, occupationsAmies) |
                    Fou.ObtenirMouvements(reine, occupations, occupationsAmies));
        }

        public static ulong ObtenirAttaques(ulong reines, ulong occupations)
        {   // Génère les cases attaquées par les reines (combinaison de la tour et du fou).
            return Fou.ObtenirAttaques(reines, occupations) | Tour.ObtenirAttaques(reines, occupations);
        }

    }
}