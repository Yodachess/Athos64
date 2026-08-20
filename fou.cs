// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe de gestion des mouvements de fou (Bishop) en utilisant des bitboards.
// ├─ "ObtenirAttaques" génère les cases attaquées par les fous
// ├─ "ObtenirMouvements" génère les mouvements légaux pour un fou
// ├─ "AttaquesDepuis" génère les cases attaquées par un fou depuis une case de départ
// ├─ "Rayon" génère les cases attaquées dans une direction donnée depuis une case de départ
// └─ "MemeDiagonale" vérifie que deux cases sont sur la même diagonale donnée par delta

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
        {   // On peut faire les 4 directions d'un coup, pas besoin de faire 4 boucles
            return
                Rayon(caseDepart, occupations, occupationsAmies, +9) |
                Rayon(caseDepart, occupations, occupationsAmies, +7) |
                Rayon(caseDepart, occupations, occupationsAmies, -7) |
                Rayon(caseDepart, occupations, occupationsAmies, -9);
        }
        private static ulong Rayon(int caseDepart, ulong occupations, ulong occupationsAmies, int delta)
        {   // On avance dans la direction donnée par delta jusqu'à rencontrer une bordure ou une pièce
            ulong attaques = 0UL;
            int caseActuelle = caseDepart;
            while (true)
            {
                int suivante = caseActuelle + delta;

                if (suivante < 0 || suivante >= 64)
                    break;

                if (!MemeDiagonale(caseActuelle, suivante, delta))
                    break;

                ulong masque = 1UL << suivante;

                // 🚫 pièce amie → on bloque sans ajouter
                if ((masque & occupationsAmies) != 0)
                    break;

                // ✅ case libre ou ennemie → on ajoute
                attaques |= masque;

                // 🛑 si occupée (donc ennemie ici), on stop
                if ((masque & occupations) != 0)
                    break;

                caseActuelle = suivante;
            }
            return attaques;
        }
        private static bool MemeDiagonale(int de, int vers, int delta)
        {   // Vérifie que les cases de et vers sont sur la même diagonale donnée par delta
            int collonneDe = de % 8;
            int collonneVers = vers % 8;

            if (delta == 9 || delta == -7)
                return collonneVers == collonneDe + 1;
            if (delta == 7 || delta == -9)
                return collonneVers == collonneDe - 1;
            return false;
        }
    }
}