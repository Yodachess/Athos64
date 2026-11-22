// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Valeurs des pièces utilisées par la recherche (tri des captures, SEE).
// L'évaluation de la position est faite par le réseau NNUE de Stockfish (BrunoNNUE.cs).
// L'ancienne évaluation personnelle (pions, mobilité, sécurité du roi...) a été retirée : elle reste dans l'historique git.

namespace Athos64
{
    public static class EvalParams
    {
        // Valeurs en centipions, dans l'ordre : pion, cavalier, fou, tour, dame, roi
        public static readonly int[] ValeursMG = { 100, 320, 340, 500, 990, 0 };
    }
}
