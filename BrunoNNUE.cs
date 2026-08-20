// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe faisant le pont pour manipuler les réseaux de neurones
// ├─ Constructeur statique "NNUE" pour initialiser la DLL
// ├─ "Initialiser" initialise les réseaux de neurones à partir des fichiers fournis
// ├─ "EvaluerEchiquier" retourne l'évaluation à partir de l'échiquier fourni en entrée
// ├─ "RemplirPieceBoard" remplit le tableau de pièces à partir d'un bitboard
// ├─ "EvaluerPieces" retourne l'évaluation à partir des tableaux de cases et pièces
// └─ "AjouterPieces" ajoute les pièces et leurs cases à partir d'un bitboard

using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Athos64
{   
    public static class BrunoNNUE
    {   // Création de la DLL à partir de  https://github.com/VedantJoshi1409/stockfish_nnue_probe
        // puis mise à niveau avec les fichiers de réseaux de neurones de Stockfish 18 (big.nnue et small.nnue)
        private const string NOM_DLL = "BrunoNNUE.dll";
        private static bool _nnueInitialise = false;
        private static bool _nnueDisponible = false;

        // 1. Initialisation : 
        [DllImport(NOM_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int NNUE_Init(IntPtr bigNetFile, IntPtr smallNetFile);

        // 2. Évaluation par FEN : 
        [DllImport(NOM_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int NNUE_EvalFEN(string fen);

        // 3. Évaluation rapide par tableaux : 
        [DllImport(NOM_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int NNUE_EvalBoard(int[] pieceBoard, int side, int rule50, int optimism);

        // 4. Evaluation rapide par tableaux de pièces et de cases :
        [DllImport(NOM_DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int NNUE_EvalPieces(int[] pieces, int[] squares, int pieceAmount, int side, int rule50);


        // --- Wrapper C# propre pour mon moteur ---
        public static bool Initialiser(string bigNet, string smallNet)
        {
            if (_nnueInitialise)    // Si les réseaux sont déjà initialisés, on ne réinitialise pas
                return _nnueDisponible;

            IntPtr pBigNet = IntPtr.Zero;
            IntPtr pSmallNet = IntPtr.Zero;

            try
            {   // Allocation des chaînes en mémoire native au format UTF-8 (compatible C/C++)
                pBigNet = Marshal.StringToCoTaskMemUTF8(bigNet);
                pSmallNet = Marshal.StringToCoTaskMemUTF8(smallNet);

                // Appel de la fonction C++
                int resultat = NNUE_Init(pBigNet, pSmallNet);
                // (Note : la fonction C++ donne 1 = succès, 0 = échec)
                _nnueDisponible = resultat != 0;    
                _nnueInitialise = true;

                if (_nnueDisponible)
                    Console.WriteLine($"[NNUE] Réseaux de neurones {bigNet} et {smallNet} chargés avec succès !\n");
                else
                    Console.WriteLine($"[NNUE ERREUR] Échec du chargement des réseaux. Code = {resultat}");

                return _nnueDisponible;
            }
            catch (Exception ex)
            {
                _nnueDisponible = false;
                _nnueInitialise = true;
                Console.WriteLine($"[NNUE ERREUR] Échec : {ex.Message}");
                return false;
            }
            finally
            {   // Libération de la mémoire native allouée pour éviter les fuites
                if (pBigNet != IntPtr.Zero) Marshal.FreeCoTaskMem(pBigNet);
                if (pSmallNet != IntPtr.Zero) Marshal.FreeCoTaskMem(pSmallNet);
            }
        }
        public static int Evaluer(Echiquier echiquier)
        {
            if (!_nnueInitialise)
            {
                if (!Initialiser(ProtocoleUCI.BigNetworkFile,
                                 ProtocoleUCI.SmallNetworkFile))
                    return 0;
            }

            int score = EvaluerEchiquier(echiquier);

            return score;
        }

        public static int EvaluerNNUE(Echiquier e)
        {   // Test de l'utilisation de NNUE si activé dans les paramètres
            int valeurNNUE = BrunoNNUE.Evaluer(e);
            return BrunoNNUE.ConvertirEnCpStockfish(valeurNNUE, e);
        }


        public static int EvaluerEchiquier(Echiquier echiquier)
        {   // Convertir l'échiquier en tableau de pièces pour le NNUE de Stockfish
            int[] pieceBoard = new int[64];

            // Blancs
            RemplirPieceBoard(pieceBoard, echiquier.PionsBlancs, 1);      // W_PAWN
            RemplirPieceBoard(pieceBoard, echiquier.CavaliersBlancs, 2);  // W_KNIGHT
            RemplirPieceBoard(pieceBoard, echiquier.FousBlancs, 3);       // W_BISHOP
            RemplirPieceBoard(pieceBoard, echiquier.ToursBlanches, 4);   // W_ROOK
            RemplirPieceBoard(pieceBoard, echiquier.ReineBlanche, 5);    // W_QUEEN
            RemplirPieceBoard(pieceBoard, echiquier.RoiBlanc, 6);        // W_KING

            // Noirs
            RemplirPieceBoard(pieceBoard, echiquier.PionsNoirs, 9);      // B_PAWN
            RemplirPieceBoard(pieceBoard, echiquier.CavaliersNoirs, 10); // B_KNIGHT
            RemplirPieceBoard(pieceBoard, echiquier.FousNoirs, 11);      // B_BISHOP
            RemplirPieceBoard(pieceBoard, echiquier.ToursNoires, 12);    // B_ROOK
            RemplirPieceBoard(pieceBoard, echiquier.ReineNoire, 13);     // B_QUEEN
            RemplirPieceBoard(pieceBoard, echiquier.RoiNoir, 14);        // B_KING

            int optimism = echiquier.CoteBlanc ? Recherche.OptimismBlanc : Recherche.OptimismNoir;
            int score = NNUE_EvalBoard(pieceBoard, echiquier.CoteBlanc ? 0 : 1, echiquier.RegleDes50Coups, optimism);

            return score;
        }

        private static void RemplirPieceBoard(int[] pieceBoard, ulong bitboard, int piece)
        {   // Remplit le tableau pieceBoard avec les pièces à partir du bitboard
            while (bitboard != 0)
            {
                int caseEchiquier = BitOperations.TrailingZeroCount(bitboard);
                pieceBoard[caseEchiquier] = piece;
                bitboard &= bitboard - 1;
            }
        }
        public static int EvaluerPieces(Echiquier echiquier)
        {   // Convertir l'échiquier en tableaux de pièces et de cases pour le NNUE de Stockfish
            int[] pieces = new int[32];
            int[] squares = new int[32];
            int nombrePieces = 0;

            AjouterPieces(echiquier.PionsBlancs, 1, pieces, squares, ref nombrePieces);
            AjouterPieces(echiquier.CavaliersBlancs, 2, pieces, squares, ref nombrePieces);
            AjouterPieces(echiquier.FousBlancs, 3, pieces, squares, ref nombrePieces);
            AjouterPieces(echiquier.ToursBlanches, 4, pieces, squares, ref nombrePieces);
            AjouterPieces(echiquier.ReineBlanche, 5, pieces, squares, ref nombrePieces);
            AjouterPieces(echiquier.RoiBlanc, 6, pieces, squares, ref nombrePieces);

            AjouterPieces(echiquier.PionsNoirs, 9, pieces, squares, ref nombrePieces);
            AjouterPieces(echiquier.CavaliersNoirs, 10, pieces, squares, ref nombrePieces);
            AjouterPieces(echiquier.FousNoirs, 11, pieces, squares, ref nombrePieces);
            AjouterPieces(echiquier.ToursNoires, 12, pieces, squares, ref nombrePieces);
            AjouterPieces(echiquier.ReineNoire, 13, pieces, squares, ref nombrePieces);
            AjouterPieces(echiquier.RoiNoir, 14, pieces, squares, ref nombrePieces);

            // Stockfish attend true = Blancs, false = Noirs
            bool blancsAuTrait = echiquier.CoteBlanc;

            return NNUE_EvalPieces(pieces, squares, nombrePieces, blancsAuTrait ? 1 : 0, echiquier.RegleDes50Coups);
        }

        private static void AjouterPieces(ulong bitboard, int codePiece, int[] pieces, int[] squares, ref int nombrePieces)
        {   // Ajoute les pièces et leurs cases à partir d'un bitboard
            while (bitboard != 0)
            {
                int caseIndex = BitOperations.TrailingZeroCount(bitboard);

                pieces[nombrePieces] = codePiece;
                squares[nombrePieces] = caseIndex;
                nombrePieces++;

                bitboard &= bitboard - 1;
            }
        }
        public static int ConvertirEnCpStockfish(int valeur, Echiquier e)
        {
            int materiel =
                BitOperations.PopCount(e.PionsBlancs) +
                BitOperations.PopCount(e.PionsNoirs) +
                3 * (BitOperations.PopCount(e.CavaliersBlancs) +
                      BitOperations.PopCount(e.CavaliersNoirs)) +
                3 * (BitOperations.PopCount(e.FousBlancs) +
                      BitOperations.PopCount(e.FousNoirs)) +
                5 * (BitOperations.PopCount(e.ToursBlanches) +
                      BitOperations.PopCount(e.ToursNoires)) +
                9 * (BitOperations.PopCount(e.ReineBlanche) +
                      BitOperations.PopCount(e.ReineNoire));

            double m = Math.Clamp(materiel, 17, 78) / 58.0;

            double a =
                ((-72.32565836 * m + 185.93832038) * m - 144.58862193) * m
                + 416.44950446;

            return (int)Math.Round(100.0 * valeur / a);
        }
    }
}