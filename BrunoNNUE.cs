// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe faisant le pont pour manipuler les réseaux de neurones
// ├─ Constructeur statique "NNUE" pour initialiser la DLL
// ├─ "Initialiser" initialise le réseau de neurones à partir du fichier fourni
// ├─ "EvaluerEchiquier" retourne l'évaluation complète à partir de l'échiquier fourni en entrée
// ├─ "RemplirPieceBoard" remplit le tableau de pièces à partir d'un bitboard
// └─ Évaluation incrémentale (un contexte natif par thread de recherche) :
//    ├─ "CreerContexte" / "DetruireContexte" / "DefinirPosition" : cycle de vie et position de départ
//    ├─ "JouerCoup" / "JouerCoupNul" / "Annuler" : rejouent les coups de la recherche dans le contexte
//    └─ "EvaluerNNUE(e, contexte)" : met à jour l'accumulateur depuis la position parente (repli : évaluation complète)

using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Athos64
{   
    public static class BrunoNNUE
    {   // Création de la DLL à partir de  https://github.com/VedantJoshi1409/stockfish_nnue_probe
        // puis mise à niveau avec le réseau de neurones de Stockfish 19 (un seul réseau)
        private const string NOM_DLL = "BrunoNNUE.dll";
        private static bool _nnueInitialise = false;
        private static bool _nnueDisponible = false;

        // 1. Initialisation : 
        [DllImport(NOM_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int NNUE_Init(IntPtr netFile);

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
        public static bool Initialiser(string reseau)
        {
            if (_nnueInitialise)    // Si les réseaux sont déjà initialisés, on ne réinitialise pas
                return _nnueDisponible;

            IntPtr pReseau = IntPtr.Zero;

            try
            {   // Allocation des chaînes en mémoire native au format UTF-8 (compatible C/C++)
                pReseau = Marshal.StringToCoTaskMemUTF8(reseau);

                // Appel de la fonction C++
                int resultat = NNUE_Init(pReseau);
                // (Note : la fonction C++ donne 1 = succès, 0 = échec)
                _nnueDisponible = resultat != 0;    
                _nnueInitialise = true;

                if (_nnueDisponible)
                    Console.WriteLine($"[NNUE] Réseau de neurones {reseau} chargé avec succès !\n");
                else
                    Console.WriteLine($"[NNUE ERREUR] Échec du chargement du réseau. Code = {resultat}");

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
                if (pReseau != IntPtr.Zero) Marshal.FreeCoTaskMem(pReseau);
            }
        }
        public static int Evaluer(Echiquier echiquier)
        {
            if (!_nnueInitialise)
            {
                if (!Initialiser(ProtocoleUCI.NetworkFile))
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


        // ===========================================================================================
        // Évaluation incrémentale : un contexte natif (position + accumulateurs) par thread de recherche.
        // La recherche y rejoue ses coups (JouerCoup / JouerCoupNul / Annuler) : l'accumulateur est alors
        // mis à jour à partir de celui de la position parente, au lieu d'être recalculé à chaque évaluation.
        // ===========================================================================================
        [DllImport(NOM_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr NNUE_CtxCreate();

        [DllImport(NOM_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void NNUE_CtxDestroy(IntPtr ctx);

        [DllImport(NOM_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NNUE_CtxSet(IntPtr ctx, int[] pieceBoard, int side, int rule50);

        [DllImport(NOM_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NNUE_CtxDo(IntPtr ctx, int from, int to, int promo);

        [DllImport(NOM_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NNUE_CtxDoNull(IntPtr ctx);

        [DllImport(NOM_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void NNUE_CtxUndo(IntPtr ctx);

        [DllImport(NOM_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NNUE_CtxEval(IntPtr ctx, int side, ulong occupation, int optimism, out int valeur);

        // Nombre d'évaluations où le contexte ne correspondait pas à l'échiquier (évaluées alors sans lui) : doit rester à 0
        private static long _desaccords = 0;
        public static long Desaccords => Interlocked.Read(ref _desaccords);

        public static IntPtr CreerContexte()
        {   // Retourne IntPtr.Zero si le réseau n'est pas disponible : la recherche évalue alors sans contexte
            if (!_nnueInitialise && !Initialiser(ProtocoleUCI.NetworkFile))
                return IntPtr.Zero;
            if (!_nnueDisponible)
                return IntPtr.Zero;
            try { return NNUE_CtxCreate(); }
            catch (Exception) { return IntPtr.Zero; }
        }

        public static void DetruireContexte(IntPtr contexte)
        {
            if (contexte != IntPtr.Zero)
                NNUE_CtxDestroy(contexte);
        }

        public static bool DefinirPosition(IntPtr contexte, Echiquier e)
        {   // Place le contexte sur la position de l'échiquier (racine de la recherche)
            if (contexte == IntPtr.Zero)
                return false;
            return NNUE_CtxSet(contexte, ConstruirePieceBoard(e), e.CoteBlanc ? 0 : 1, e.RegleDes50Coups) != 0;
        }

        public static bool JouerCoup(IntPtr contexte, int caseDepart, int caseArrivee, char? promotion)
        {   // À appeler juste avant e.JouerCoup ; retourne false si le contexte ne l'a pas joué (alors pas d'Annuler)
            if (contexte == IntPtr.Zero)
                return false;
            int promo = promotion switch { 'n' => 2, 'b' => 3, 'r' => 4, 'q' => 5, _ => 0 };
            return NNUE_CtxDo(contexte, caseDepart, caseArrivee, promo) != 0;
        }

        public static bool JouerCoupNul(IntPtr contexte)
        {
            return contexte != IntPtr.Zero && NNUE_CtxDoNull(contexte) != 0;
        }

        public static void Annuler(IntPtr contexte)
        {
            NNUE_CtxUndo(contexte);
        }

        public static int EvaluerNNUE(Echiquier e, IntPtr contexte)
        {   // Évaluation incrémentale si possible, sinon évaluation complète (même résultat)
            if (contexte != IntPtr.Zero)
            {
                int optimism = e.CoteBlanc ? Recherche.OptimismBlanc : Recherche.OptimismNoir;
                if (NNUE_CtxEval(contexte, e.CoteBlanc ? 0 : 1, e.ObtenirToutesLesPieces(), optimism, out int valeur) != 0)
                    return ConvertirEnCpStockfish(valeur, e);
                Interlocked.Increment(ref _desaccords);
            }
            return EvaluerNNUE(e);
        }

        private static int[] ConstruirePieceBoard(Echiquier echiquier)
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

            return pieceBoard;
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
                ((-142.72052667 * m + 372.35176398) * m - 340.71073572) * m
                + 415.23490212;

            return (int)Math.Round(100.0 * valeur / a);
        }
    }
}
