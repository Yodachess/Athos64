// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe (partie) de gestion de l'échiquier.
// ├─ "InitialiserPositionDeDepart" initialise la position de départ standard
// ├─ "GenererMouvementsLegaux" génère un bitboard de tous les mouvements légaux pour le côté qui joue
// ├─ "InitialiserScoresMateriels" initialise les scores matériels pour l'évaluation
// ├─ "IdentifierTypePiece" retourne le type de pièce sur une case donnée
// ├─ "ObtenirToutesLesPieces" retourne un bitboard de toutes les pièces sur l'échiquier
// ├─ "ObtenirPiecesBlanches" 
// ├─ "ObtenirPiecesNoires" 
// ├─ "ObtenirBitboard" convertit une case en bitboard
// ├─ "ViderEchiquier" vide l'échiquier
// ├─ "FromAlgebraic" convertit une notation algébrique en index de case
// ├─ "JouerCoupNul" gère le coup nul (changement de trait et en-passant)
// ├─ "AnnulerCoupNul" annule le coup nul (changement de trait et en-passant)
// ├─ "AttaquesDepuis" génère les cases attaquées par une tour depuis une case de départ
// ├─ [DEBUG] "Afficher" affiche l'échiquier dans la console pour le débogage
// ├─ [DEBUG] "PlacerPiece" place une pièce sur l'échiquier (pour les tests)
// ├─ [DEBUG] "EffacerUnePiece" efface une pièce de l'échiquier (pour les tests)
// └─ [DEBUG] "EffacerPieces" efface toutes les pièces de l'échiquier (pour les tests)

using System;
using System.Numerics;
using Athos64;

namespace Athos64
{
    public partial class Echiquier
    {
        // Bitboards pour chaque type de pièce
        public ulong PionsBlancs; 
        public ulong PionsNoirs;
        public ulong CavaliersBlancs; 
        public ulong CavaliersNoirs; 
        public ulong FousBlancs; 
        public ulong FousNoirs; 
        public ulong ToursBlanches; 
        public ulong ToursNoires; 
        public ulong ReineBlanche; 
        public ulong ReineNoire;    
        public ulong RoiBlanc; 
        public ulong RoiNoir;

        public ulong OccupationBlancs => PionsBlancs | CavaliersBlancs | FousBlancs |ToursBlanches | ReineBlanche | RoiBlanc;
        public ulong OccupationNoirs => PionsNoirs | CavaliersNoirs | FousNoirs | ToursNoires | ReineNoire | RoiNoir;

        public bool CoteBlanc; 
        public int CaseEnPassant; 

        // Droits au roque
        public bool RoqueBlancCoteRoiPossible; 
        public bool RoqueBlancCoteDamePossible; 
        public bool RoqueNoirCoteRoiPossible;   
        public bool RoqueNoirCoteDamePossible; 

        public ulong CleActuelle;
        public static string FenDepart { get; set; } = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        public int RegleDes50Coups { get; set; } // Nombre de demi-coups depuis la dernière capture ou poussée de pion
        public int NumeroDeCoup { get; set; }    // Le numéro du coup complet (incrémenté après chaque coup des noirs)
        public enum TypePiece { Vide, Pion, Cavalier, Fou, Tour, Reine, Roi }
        public static readonly int[] ValeursPieces = [0, 100, 320, 330, 500, 900, 20000];

        // Ordre Zobrist : Pion, Cavalier, Fou, Tour, Reine, Roi (Blancs puis Noirs)
        private static readonly int[] ValeursMG = [100, 320, 330, 500, 900, 0, -100, -320, -330, -500, -900, 0];
        private static readonly int[] ValeursEG = [150, 310, 340, 550, 950, 0, -150, -310, -340, -550, -950, 0];
        private static readonly int[] PoidsPhase = [0, 1, 1, 2, 4, 0, 0, 1, 1, 2, 4, 0];

        public int ScoreMaterielMG;
        public int ScoreMaterielEG;
        public int PhaseActuelle;
        public Echiquier()
        {   // Initialise l'échiquier à la position de départ standard
            InitialiserPositionDeDepart();
        }
        public void InitialiserPositionDeDepart()
        {   // Position de départ standard
            PionsBlancs = 0x000000000000FF00UL;
            PionsNoirs = 0x00FF000000000000UL;
            CavaliersBlancs = 0x0000000000000042UL;
            CavaliersNoirs = 0x4200000000000000UL;
            FousBlancs = 0x0000000000000024UL;
            FousNoirs = 0x2400000000000000UL;
            ToursBlanches = 0x0000000000000081UL;
            ToursNoires = 0x8100000000000000UL;
            ReineBlanche = 0x0000000000000008UL;
            ReineNoire = 0x0800000000000000UL;
            RoiBlanc = 0x0000000000000010UL;
            RoiNoir = 0x1000000000000000UL;

            CoteBlanc = true;
            CaseEnPassant = -1;
            RoqueBlancCoteRoiPossible = true;
            RoqueBlancCoteDamePossible = true;
            RoqueNoirCoteRoiPossible = true;
            RoqueNoirCoteDamePossible = true;
            // Calcul de la clé Zobrist pour la position de départ
            CleActuelle = CalculerCleComplete();
        }

        public ulong GenererMouvementsLegaux()
        {   // Génère un bitboard de tous les mouvements légaux pour le côté qui joue.
            ulong mouvementsLegaux = 0UL;
            // ✅ On crée une liste locale car on doit fournir l'argument requis
            List<Mouvement> mouvements = [];
            GenererMouvementsLegauxCommun(mouvements);
            foreach (Mouvement mvt in mouvements)
            {
                mouvementsLegaux |= Bitboard.CaseVersBitboard(mvt.CaseArrivee);
            }
            return mouvementsLegaux;
        }
        public void InitialiserScoresMateriels()
        {
            ScoreMaterielMG = 0;
            ScoreMaterielEG = 0;
            PhaseActuelle = 0;

            for (int i = 0; i < 64; i++)
            {
                int pieceZobrist = ObtenirIndexPieceZobrist(i);
                if (pieceZobrist != -1)
                {
                    bool estBlanc = pieceZobrist < 6;
                    int indexType = pieceZobrist % 6;
                    /*
                    int vMG = EvalParams.ValeursMG[indexType];
                    int vEG = EvalParams.ValeursEG[indexType];
                    int vPh = EvalParams.ValeursPhase[indexType];
                    
                    if (estBlanc)
                    {
                        ScoreMaterielMG += vMG;
                        ScoreMaterielEG += vEG;
                    }
                    else
                    {
                        ScoreMaterielMG -= vMG;
                        ScoreMaterielEG -= vEG;
                    }
                    PhaseActuelle += vPh;
                    */
                }
            }
        }

        public TypePiece IdentifierTypePiece(int caseIndex)
        {   // Retourne le type de pièce sur la case donnée, ou "Vide" si aucune pièce. 
            ulong bit = 1UL << caseIndex;

            // --- OPTIMISATION : On vérifie d'abord si la case contient n'importe quelle pièce ---
            if ((ObtenirToutesLesPieces() & bit) == 0)
            {
                // Au lieu de lancer une exception tout de suite, on pourrait retourner un type "Vide"
                // Mais si ton moteur impose qu'il y ait une pièce, laisse l'exception.
                throw new Exception($"Aucune pièce trouvée sur la case {Bitboard.IndexVersCase(caseIndex)}");
            }

            // Si on arrive ici, on est CERTAIN qu'il y a une pièce.
            // On teste les bitboards par type, peu importe la couleur
            if (((PionsBlancs | PionsNoirs) & bit) != 0) return TypePiece.Pion;
            if (((CavaliersBlancs | CavaliersNoirs) & bit) != 0) return TypePiece.Cavalier;
            if (((FousBlancs | FousNoirs) & bit) != 0) return TypePiece.Fou;
            if (((ToursBlanches | ToursNoires) & bit) != 0) return TypePiece.Tour;
            if (((ReineBlanche | ReineNoire) & bit) != 0) return TypePiece.Reine;

            // Inutile de tester le Roi avec un "if" : si ce n'est rien d'autre, c'est forcément le Roi.
            return TypePiece.Roi;
        }
        public ulong ObtenirToutesLesPieces()
        {   // Retourne un bitboard de toutes les pièces sur l'échiquier.
            return PionsBlancs | PionsNoirs | CavaliersBlancs | CavaliersNoirs |
                   FousBlancs | FousNoirs | ToursBlanches | ToursNoires |
                   ReineBlanche | ReineNoire | RoiBlanc | RoiNoir;
        }
        public ulong ObtenirPiecesBlanches()
        {   // Retourne un bitboard des pièces blanches.
            return PionsBlancs | CavaliersBlancs | FousBlancs | ToursBlanches | ReineBlanche | RoiBlanc;
        }
        public ulong ObtenirPiecesNoires()
        {   // Retourne un bitboard des pièces noires.
            return PionsNoirs | CavaliersNoirs | FousNoirs | ToursNoires | ReineNoire | RoiNoir;
        }
        public ulong ObtenirBitboard(Case c) => 1UL << (int)c;          // Convertit une case en bitboard.

        public void ViderEchiquier()
        {   // Efface toutes les pièces et réinitialise les droits au roque et le côté qui joue
            PionsBlancs = PionsNoirs = 0;
            CavaliersBlancs = CavaliersNoirs = 0;
            FousBlancs = FousNoirs = 0;
            ToursBlanches = ToursNoires = 0;
            ReineBlanche = ReineNoire = 0;
            RoiBlanc = RoiNoir = 0;

            CoteBlanc = true;
            CaseEnPassant = -1;

            RoqueBlancCoteRoiPossible = false;
            RoqueBlancCoteDamePossible = false;
            RoqueNoirCoteRoiPossible = false;
            RoqueNoirCoteDamePossible = false;
        }
        public static int FromAlgebraic(string sq)
        {   // Convertit une notation algébrique (ex: "e4") en index de case (0-63). Par exemple, "a1" -> 0, "h8" -> 63.
            int file = sq[0] - 'a';
            int rank = sq[1] - '1';
            return rank * 8 + file;
        }

        public void JouerCoupNul()
        {   // 1. Gestion de la clé Zobrist pour le trait
            // Vu que le trait change, on applique le XOR avec ta constante
            CleActuelle ^= Zobrist.TraitBlanc;

            // 2. Gestion de la clé Zobrist pour l'en-passant
            // Si un droit de prise en passant existait, il expire ! 
            // On doit le retirer de la clé avant de mettre la variable à -1
            if (CaseEnPassant != -1)
            {
                int colonne = CaseEnPassant % 8;
                CleActuelle ^= Zobrist.ColonneEnPassant[colonne];
            }

            // 3. Changement d'état de l'échiquier
            CoteBlanc = !CoteBlanc;
            CaseEnPassant = -1;
        }

        public void AnnulerCoupNul(int ancienneCaseEnPassant)
        {
            // 1. On remet l'état de l'échiquier d'origine
            CoteBlanc = !CoteBlanc;
            CaseEnPassant = ancienneCaseEnPassant;

            // 2. On remet la clé Zobrist à l'identique (le XOR est réversible)
            CleActuelle ^= Zobrist.TraitBlanc;

            if (CaseEnPassant != -1)
            {
                int colonne = CaseEnPassant % 8;
                CleActuelle ^= Zobrist.ColonneEnPassant[colonne];
            }
        }

        // *************** Utilisé pour le debug ********
        public void Afficher()      
        {   // Affiche l'échiquier dans la console (pour le débogage).
            for (int ligne = 7; ligne >= 0; ligne--)
            {
                Console.Write($"{ligne + 1} ");
                for (int colonne = 0; colonne < 8; colonne++)
                {
                    int caseIndex = ligne * 8 + colonne;
                    ulong masque = 1UL << caseIndex;

                    if ((PionsBlancs & masque) != 0) Console.Write("♙ ");
                    else if ((PionsNoirs & masque) != 0) Console.Write("♟ ");
                    else if ((CavaliersBlancs & masque) != 0) Console.Write("♘ ");
                    else if ((CavaliersNoirs & masque) != 0) Console.Write("♞ ");
                    else if ((FousBlancs & masque) != 0) Console.Write("♗ ");
                    else if ((FousNoirs & masque) != 0) Console.Write("♝ ");
                    else if ((ToursBlanches & masque) != 0) Console.Write("♖ ");
                    else if ((ToursNoires & masque) != 0) Console.Write("♜ ");
                    else if ((ReineBlanche & masque) != 0) Console.Write("♕ ");
                    else if ((ReineNoire & masque) != 0) Console.Write("♛ ");
                    else if ((RoiBlanc & masque) != 0) Console.Write("♔ ");
                    else if ((RoiNoir & masque) != 0) Console.Write("♚ ");
                    else Console.Write(". ");
                }
                Console.WriteLine();
            }
            Console.WriteLine("  a b c d e f g h");
        }
        public void PlacerPiece(Case c, TypePiece type, bool côtéBlanc)
        {   // Convertit la case en bitboard et place la pièce correspondante (pour les tests)
            ulong bitboard = ObtenirBitboard(c);
            // Effacer les pièces existantes de ce type/côté (optionnel)
            if (côtéBlanc)
            {
                switch (type)
                {
                    case TypePiece.Pion: PionsBlancs |= bitboard; break;
                    case TypePiece.Cavalier: CavaliersBlancs |= bitboard; break;
                    case TypePiece.Fou: FousBlancs |= bitboard; break;
                    case TypePiece.Tour: ToursBlanches |= bitboard; break;
                    case TypePiece.Reine: ReineBlanche |= bitboard; break;
                    case TypePiece.Roi: RoiBlanc |= bitboard; break;
                }
            }
            else
            {
                switch (type)
                {
                    case TypePiece.Pion: PionsNoirs |= bitboard; break;
                    case TypePiece.Cavalier: CavaliersNoirs |= bitboard; break;
                    case TypePiece.Fou: FousNoirs |= bitboard; break;
                    case TypePiece.Tour: ToursNoires |= bitboard; break;
                    case TypePiece.Reine: ReineNoire |= bitboard; break;
                    case TypePiece.Roi: RoiNoir |= bitboard; break;
                }
            }
        }
        public void EffacerUnePiece(Case c)
        {   // Efface la pièce à la case donnée (pour les tests).
            ulong mask = ObtenirBitboard(c);
            // Blancs
            PionsBlancs &= ~mask;
            CavaliersBlancs &= ~mask;
            FousBlancs &= ~mask;
            ToursBlanches &= ~mask;
            ReineBlanche &= ~mask;
            RoiBlanc &= ~mask;
            // Noirs
            PionsNoirs &= ~mask;
            CavaliersNoirs &= ~mask;
            FousNoirs &= ~mask;
            ToursNoires &= ~mask;
            ReineNoire &= ~mask;
            RoiNoir &= ~mask;
        }
        public void EffacerPieces()
        {   /// Efface toutes les pièces (pour les tests).
            PionsBlancs = PionsNoirs = 0UL;
            CavaliersBlancs = CavaliersNoirs = 0UL;
            FousBlancs = FousNoirs = 0UL;
            ToursBlanches = ToursNoires = 0UL;
            ReineBlanche = ReineNoire = 0UL;
            RoiBlanc = RoiNoir = 0UL;
            // ✅ On réinitialise les droits au roque
            RoqueBlancCoteRoiPossible = false;
            RoqueBlancCoteDamePossible = false;
            RoqueNoirCoteRoiPossible = false;
            RoqueNoirCoteDamePossible = false;
            // Optionnel : On réinitialise aussi la case de prise en passant
            CaseEnPassant = -1;
        }
        // *************** Utilisé pour le debug ********

    }
}