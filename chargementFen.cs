// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe de chargement d'une position d'échecs à partir d'une chaîne FEN (Forsyth-Edwards Notation)
// └─ "ChargerFen" comme son nom l'indique ..

using System;

namespace Athos64
{
    public static class ChargementFen
    {
        public static void ChargerFen(Echiquier e, string fen, bool affichagePosition)
        {   //Charge une position d'échecs à partir d'une chaîne FEN (Forsyth-Edwards Notation)
            e.ViderEchiquier();     // reset complet du plateau et des bitboards
            e.DemiCoupActuel = 0;   // on remet le compteur d'historique à zéro

            // Utilisation de ReadOnlySpan pour découper la FEN sans créer de nouveaux objets string[]
            ReadOnlySpan<char> spanFen = fen.AsSpan();
            int start = 0;
            int partIndex = 0;
            ReadOnlySpan<char> placement = default, trait = default, roque = default,
                               enPassant = default, demicoup = default, coupentier = default;

            for (int i = 0; i <= spanFen.Length; i++)
            {
                if (i == spanFen.Length || spanFen[i] == ' ')
                {
                    ReadOnlySpan<char> part = spanFen.Slice(start, i - start);
                    if (part.Length > 0)
                    {
                        switch (partIndex)
                        {
                            case 0: placement = part; break;
                            case 1: trait = part; break;
                            case 2: roque = part; break;
                            case 3: enPassant = part; break;
                            case 4: demicoup = part; break;
                            case 5: coupentier = part; break;
                        }
                        partIndex++;
                    }
                    start = i + 1;
                }
            }

            int caseIndex = 56; // a8 = début FEN
            // 1. Placement des pièces sur l'échiquier
            foreach (char c in placement)
            {
                if (c == '/')
                {
                    caseIndex -= 16;
                    continue;
                }
                if (char.IsDigit(c))
                {
                    caseIndex += (c - '0');
                    continue;
                }
                ulong bit = 1UL << caseIndex;
                switch (c)
                {   // blancs
                    case 'P': e.PionsBlancs |= bit; break;
                    case 'N': e.CavaliersBlancs |= bit; break;
                    case 'B': e.FousBlancs |= bit; break;
                    case 'R': e.ToursBlanches |= bit; break;
                    case 'Q': e.ReineBlanche |= bit; break;
                    case 'K': e.RoiBlanc |= bit; break;
                    // noirs
                    case 'p': e.PionsNoirs |= bit; break;
                    case 'n': e.CavaliersNoirs |= bit; break;
                    case 'b': e.FousNoirs |= bit; break;
                    case 'r': e.ToursNoires |= bit; break;
                    case 'q': e.ReineNoire |= bit; break;
                    case 'k': e.RoiNoir |= bit; break;
                }
                caseIndex++;
            }

            // 2. Trait
            e.CoteBlanc = trait.Length > 0 && trait[0] == 'w';

            // 3. Roques
            e.RoqueBlancCoteRoiPossible = roque.Contains("K", StringComparison.Ordinal);
            e.RoqueBlancCoteDamePossible = roque.Contains("Q", StringComparison.Ordinal);
            e.RoqueNoirCoteRoiPossible = roque.Contains("k", StringComparison.Ordinal);
            e.RoqueNoirCoteDamePossible = roque.Contains("q", StringComparison.Ordinal);

            // 4. En passant
            e.CaseEnPassant = (enPassant.Length > 0 && enPassant[0] != '-') ? Echiquier.FromAlgebraic(enPassant.ToString()) : -1;

            // 5. Règle des 50 coups
            if (int.TryParse(demicoup, out int h)) e.RegleDes50Coups = h;

            // 6. Numéro du coup actuel
            if (int.TryParse(coupentier, out int f)) e.NumeroDeCoup = f;

            // 7. Calcul score matériel
            e.InitialiserScoresMateriels();

            // 8. Calcul de la clé Zobrist initiale
            e.CleActuelle = e.CalculerCleComplete();

            // 9.Position intiale dans l'historique des clés
            e.HistoriqueCles[e.DemiCoupActuel] = e.CleActuelle;
            e.DemiCoupActuel++; // DemiCoupActuel passe à 1

            // Pour DEBUG : afficher la position chargée si demandé
            if (affichagePosition) e.Afficher();
        }
    }
}