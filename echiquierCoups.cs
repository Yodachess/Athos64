// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe (partie) de gestion de l'échiquier (gestion des coups).
// ├─ Structure "EtatEchiquier" pour sauvegarder l'état de l'échiquier avant un coup
// ├─ "SauvegarderEtat" sauvegarde l'état actuel de l'échiquier
// ├─ "RestaurerEtat" restaure l'état de l'échiquier à partir d'un état sauvegardé
// ├─ "AnnulerCoup" annule le dernier coup joué et restaure l'état précédent
// ├─ "JouerCoup" joue un coup et retourne l'état précédent pour permettre l'annulation
// ├─ "EstPositionInitiale" vérifie si l'échiquier est dans la position initiale
// ├─ "EstApresE4" vérifie si l'échiquier est après le coup e4 (pour l'ouverture)
// ├─ "EstApresD4" vérifie si l'échiquier est après le coup d4 (pour l'ouverture)
// ├─ "EstApresC4" vérifie si l'échiquier est après le coup c4 (pour l'ouverture)
// ├─ "EstApresCf3" vérifie si l'échiquier est après le coup Cf3 (pour l'ouverture)
// └─ "ObtenirIndexPromotionZobrist" retourne l'index Zobrist pour une promotion de pion

using System;
using System.Numerics;
using Athos64;

namespace Athos64
{
    public partial class Echiquier
    {
        public ulong[] HistoriqueCles = new ulong[1024];
        public int DemiCoupActuel = 0; 
        public struct EtatEchiquier
        {   // Structure pour sauvegarder l'état de l'échiquier avant un coup.
            // Bitboards (P=Pion, C=Cavalier, F=Fou, T=Tour, Q=Reine, R=Roi | B=Blanc, N=Noir)
            public ulong PB, PN, CB, CN, FB, FN, TB, TN, QB, QN, RB, RN;

            // État du jeu
            public bool CoteBlanc;
            public int CaseEnPassant;

            // Droits au roque (B=Blanc, N=Noir | K=CôtéRoi, Q=CôtéDame)
            public bool BK, BQ, NK, NQ;

            // Infos de mouvement et compteurs
            public bool EstPriseEnPassant;
            public int? CasePionPris;
            public ulong CleZobrist;
            public int RegleDes50Coups;
            public int NumeroDeCoup;
            public int DemiCoup;
            public int ScoreMG;
            public int ScoreEG;
            public int Phase;
        }

        public EtatEchiquier SauvegarderEtat()
        {
            return new EtatEchiquier
            {
                // Bitboards
                PB = PionsBlancs,
                PN = PionsNoirs,
                CB = CavaliersBlancs,
                CN = CavaliersNoirs,
                FB = FousBlancs,
                FN = FousNoirs,
                TB = ToursBlanches,
                TN = ToursNoires,
                QB = ReineBlanche,
                QN = ReineNoire,
                RB = RoiBlanc,
                RN = RoiNoir,

                // État
                CoteBlanc = CoteBlanc,
                CaseEnPassant = CaseEnPassant,

                // Roques
                BK = RoqueBlancCoteRoiPossible,
                BQ = RoqueBlancCoteDamePossible,
                NK = RoqueNoirCoteRoiPossible,
                NQ = RoqueNoirCoteDamePossible,

                // Méta-données
                CleZobrist = CleActuelle,
                RegleDes50Coups = this.RegleDes50Coups,
                NumeroDeCoup = this.NumeroDeCoup,
                DemiCoup = this.DemiCoupActuel,

                // Initialisés par défaut, modifiés par JouerCoup si besoin
                EstPriseEnPassant = false,
                CasePionPris = null,

                ScoreMG = this.ScoreMaterielMG,
                ScoreEG = this.ScoreMaterielEG,
                Phase = this.PhaseActuelle
            };
        }
        public void RestaurerEtat(EtatEchiquier etat)
        {
            // Restauration des Bitboards
            PionsBlancs = etat.PB; PionsNoirs = etat.PN;
            CavaliersBlancs = etat.CB; CavaliersNoirs = etat.CN;
            FousBlancs = etat.FB; FousNoirs = etat.FN;
            ToursBlanches = etat.TB; ToursNoires = etat.TN;
            ReineBlanche = etat.QB; ReineNoire = etat.QN;
            RoiBlanc = etat.RB; RoiNoir = etat.RN;

            // Restauration de l'état
            CoteBlanc = etat.CoteBlanc;
            CaseEnPassant = etat.CaseEnPassant;

            // Restauration des roques
            RoqueBlancCoteRoiPossible = etat.BK;
            RoqueBlancCoteDamePossible = etat.BQ;
            RoqueNoirCoteRoiPossible = etat.NK;
            RoqueNoirCoteDamePossible = etat.NQ;

            // Restauration des compteurs et de la clé
            RegleDes50Coups = etat.RegleDes50Coups;
            NumeroDeCoup = etat.NumeroDeCoup;
            this.DemiCoupActuel = etat.DemiCoup;
            CleActuelle = etat.CleZobrist;

            this.ScoreMaterielMG = etat.ScoreMG;
            this.ScoreMaterielEG = etat.ScoreEG;
            this.PhaseActuelle = etat.Phase;
        }
        public Echiquier Cloner()
        {
            var nouvelEchiquier = new Echiquier();
            nouvelEchiquier.RestaurerEtat(this.SauvegarderEtat());
            return nouvelEchiquier;
        }
        public void AnnulerCoup(EtatEchiquier etatPrecedent)
        {   // Annule le dernier coup joué et restaure l'état précédent.
            RestaurerEtat(etatPrecedent);
            // On restaure simplement la clé telle qu'elle était avant le coup
            this.CleActuelle = etatPrecedent.CleZobrist;
        }

        // Joue un coup et retourne l'état précédent pour permettre l'annulation.
        public EtatEchiquier JouerCoup(int caseDepart, int caseArrivee, char? promotion = null)
        {
            // =========================
            // 0. SAUVEGARDE DE L'ÉTAT
            // =========================
            EtatEchiquier etatPrecedent = SauvegarderEtat();
            etatPrecedent.CleZobrist = CleActuelle; // On stocke la clé AVANT les modifs

            // ======================================================================
            // 💾 INJECTION DE L'HISTORIQUE : ON RETIENT LA POSITION AVANT LE COUP
            // ======================================================================
            HistoriqueCles[DemiCoupActuel] = CleActuelle;
            DemiCoupActuel++;
            // ======================================================================

            ulong departBit = Bitboard.CaseVersBitboard(caseDepart);
            ulong arriveeBit = Bitboard.CaseVersBitboard(caseArrivee);
            bool blanc = CoteBlanc;

            // Identification de la pièce qui bouge et de la victime
            int typeAttaquant = ObtenirIndexPieceZobrist(caseDepart);
            int typeVictime = ObtenirIndexPieceZobrist(caseArrivee);

            // Snapshots pour la logique existante
            ulong pBlanc0 = PionsBlancs; ulong pNoir0 = PionsNoirs;
            ulong rBlanc0 = RoiBlanc; ulong rNoir0 = RoiNoir;
            ulong tBlanc0 = ToursBlanches; ulong tNoir0 = ToursNoires;

            bool estUnPion = blanc ? (pBlanc0 & departBit) != 0 : (pNoir0 & departBit) != 0;
            bool estUnRoi = blanc ? (rBlanc0 & departBit) != 0 : (rNoir0 & departBit) != 0;
            bool estUneTour = blanc ? (tBlanc0 & departBit) != 0 : (tNoir0 & departBit) != 0;

            // --- MISE À JOUR MATÉRIEL (CAPTURE CLASSIQUE) ---
            if (typeVictime != -1)
            {
                // On récupère le type de pièce (0-5 pour Blanc, 6-11 pour Noir)
                // On utilise (typeVictime % 6) pour avoir l'index universel (0=Pion, 1=Cavalier...)
                int indexPiece = typeVictime % 6;
                int vMG = EvalParams.ValeursMG[indexPiece];
                int vEG = EvalParams.ValeursEG[indexPiece];
                int vPh = EvalParams.ValeursPhase[indexPiece];

                if (blanc) // Le blanc capture une pièce noire
                {
                    ScoreMaterielMG += vMG;
                    ScoreMaterielEG += vEG;
                }
                else // Le noir capture une pièce blanche
                {
                    ScoreMaterielMG -= vMG;
                    ScoreMaterielEG -= vEG;
                }
                PhaseActuelle -= vPh;
            }
            // =========================
            // 1. GESTION ZOBRIST : AVANT LE MOUVEMENT
            // =========================

            // Sécurité : si on ne trouve pas la pièce, on ne peut pas mettre à jour la clé
            if (typeAttaquant == -1) throw new Exception($"Erreur : Aucune pièce à la case de départ {caseDepart}");

            // 1. Sortir la pièce de départ de la clé
            CleActuelle ^= Zobrist.Pieces[typeAttaquant, caseDepart];
            // 2. Si capture classique, sortir la victime de la clé
            // Note : En-passant est géré plus bas car la victime n'est pas sur caseArrivee
            if (typeVictime != -1)
                CleActuelle ^= Zobrist.Pieces[typeVictime, caseArrivee];
            // 3. Sortir l'ancienne case en passant (si elle existait)
            if (etatPrecedent.CaseEnPassant != -1)
                CleActuelle ^= Zobrist.ColonneEnPassant[etatPrecedent.CaseEnPassant % 8];
            // 4. Sortir les anciens droits au roque (systématiquement)
            int indexRoqueAncien = (RoqueBlancCoteRoiPossible ? 1 : 0) |
                                   (RoqueBlancCoteDamePossible ? 2 : 0) |
                                   (RoqueNoirCoteRoiPossible ? 4 : 0) |
                                   (RoqueNoirCoteDamePossible ? 8 : 0);

            CleActuelle ^= Zobrist.DroitsRoque[indexRoqueAncien];

            // ==========================
            // MISE À JOUR RÈGLE 50 COUPS
            // ==========================
            // Si c'est un mouvement de pion ou une capture (classique ou en passant)
            if (estUnPion || typeVictime != -1)
            {
                RegleDes50Coups = 0;
            }
            else
            {
                RegleDes50Coups++;
            }

            // ==========================================
            // 2. GESTION DE LA CASE EN PASSANT (Logique)
            // ==========================================
            CaseEnPassant = -1;

            // ==========================================
            // 3. TRAITEMENT DES CAPTURES (Classiques)
            // ==========================================
            if (typeVictime != -1)
            {
                if (blanc)
                {
                    PionsNoirs &= ~arriveeBit; CavaliersNoirs &= ~arriveeBit; FousNoirs &= ~arriveeBit;
                    ToursNoires &= ~arriveeBit; ReineNoire &= ~arriveeBit;
                    if (caseArrivee == 63) RoqueNoirCoteRoiPossible = false;
                    if (caseArrivee == 56) RoqueNoirCoteDamePossible = false;
                }
                else
                {
                    PionsBlancs &= ~arriveeBit; CavaliersBlancs &= ~arriveeBit; FousBlancs &= ~arriveeBit;
                    ToursBlanches &= ~arriveeBit; ReineBlanche &= ~arriveeBit;
                    if (caseArrivee == 7) RoqueBlancCoteRoiPossible = false;
                    if (caseArrivee == 0) RoqueBlancCoteDamePossible = false;
                }
            }

            // ==========================
            // 4. DÉPLACEMENT DE LA PIÈCE
            // ==========================
            if (blanc)
            {
                PionsBlancs &= ~departBit; CavaliersBlancs &= ~departBit; FousBlancs &= ~departBit;
                ToursBlanches &= ~departBit; ReineBlanche &= ~departBit; RoiBlanc &= ~departBit;
            }
            else
            {
                PionsNoirs &= ~departBit; CavaliersNoirs &= ~departBit; FousNoirs &= ~departBit;
                ToursNoires &= ~departBit; ReineNoire &= ~departBit; RoiNoir &= ~departBit;
            }

            if (estUnPion)
            {   // Prise en passant
                if (caseArrivee == etatPrecedent.CaseEnPassant)
                {
                    int casePionCapture = blanc ? caseArrivee - 8 : caseArrivee + 8;
                    int typePionCapture = blanc ? 6 : 0; // Pion noir si blanc joue, sinon pion blanc

                    // NOUVEAU : Matériel En Passant
                    int vMG_EP = EvalParams.ValeursMG[0];
                    int vEG_EP = EvalParams.ValeursEG[0];
                    int vPh_EP = EvalParams.ValeursPhase[0];
                    if (blanc) { ScoreMaterielMG += vMG_EP; ScoreMaterielEG += vEG_EP; }
                    else { ScoreMaterielMG -= vMG_EP; ScoreMaterielEG -= vEG_EP; }
                    PhaseActuelle -= vPh_EP;

                    // Mise à jour Zobrist pour le pion mangé en passant
                    CleActuelle ^= Zobrist.Pieces[typePionCapture, casePionCapture];

                    ulong masqueCapture = Bitboard.CaseVersBitboard(casePionCapture);
                    if (blanc) PionsNoirs &= ~masqueCapture; else PionsBlancs &= ~masqueCapture;

                    etatPrecedent.EstPriseEnPassant = true;
                    etatPrecedent.CasePionPris = casePionCapture;
                }
                // Double pas
                if (Math.Abs(caseArrivee - caseDepart) == 16)
                {
                    CaseEnPassant = (caseDepart + caseArrivee) / 2;
                    // Ajouter la nouvelle case en passant à la clé
                    CleActuelle ^= Zobrist.ColonneEnPassant[CaseEnPassant % 8];
                }
                else
                {
                    CaseEnPassant = -1;
                }
                // Promotion
                if (promotion.HasValue)
                {
                    int indexPromuZobrist = ObtenirIndexPromotionZobrist(promotion.Value, blanc);
                    int indexPiecePromue = indexPromuZobrist % 6;
                    CleActuelle ^= Zobrist.Pieces[indexPromuZobrist, caseArrivee];

                    // NOUVEAU : Matériel Promotion (Différence Pion vs Pièce)
                    int diffMG = EvalParams.ValeursMG[indexPiecePromue] - EvalParams.ValeursMG[0];
                    int diffEG = EvalParams.ValeursEG[indexPiecePromue] - EvalParams.ValeursEG[0];
                    if (blanc) { ScoreMaterielMG += diffMG; ScoreMaterielEG += diffEG; }
                    else { ScoreMaterielMG -= diffMG; ScoreMaterielEG -= diffEG; }

                    if (blanc)
                    {
                        switch (promotion.Value)
                        {
                            case 'q': ReineBlanche |= arriveeBit; break;
                            case 'r': ToursBlanches |= arriveeBit; break;
                            case 'b': FousBlancs |= arriveeBit; break;
                            case 'n': CavaliersBlancs |= arriveeBit; break;
                        }
                    }
                    else
                    {
                        switch (promotion.Value)
                        {
                            case 'q': ReineNoire |= arriveeBit; break;
                            case 'r': ToursNoires |= arriveeBit; break;
                            case 'b': FousNoirs |= arriveeBit; break;
                            case 'n': CavaliersNoirs |= arriveeBit; break;
                        }
                    }
                }
                else
                {
                    CleActuelle ^= Zobrist.Pieces[typeAttaquant, caseArrivee];
                    if (blanc) PionsBlancs |= arriveeBit; else PionsNoirs |= arriveeBit;
                }
            }
            else if (estUnRoi)
            {
                CleActuelle ^= Zobrist.Pieces[typeAttaquant, caseArrivee];
                if (blanc) { RoiBlanc |= arriveeBit; RoqueBlancCoteRoiPossible = false; RoqueBlancCoteDamePossible = false; }
                else { RoiNoir |= arriveeBit; RoqueNoirCoteRoiPossible = false; RoqueNoirCoteDamePossible = false; }

                // Roque (déplacement de la tour)
                if (Math.Abs(caseArrivee - caseDepart) == 2)
                {
                    int cDepTour, cArrTour, typeTour;
                    if (caseArrivee == 6) { cDepTour = 7; cArrTour = 5; typeTour = 3; ToursBlanches &= ~Bitboard.CaseVersBitboard(7); ToursBlanches |= Bitboard.CaseVersBitboard(5); }
                    else if (caseArrivee == 2) { cDepTour = 0; cArrTour = 3; typeTour = 3; ToursBlanches &= ~Bitboard.CaseVersBitboard(0); ToursBlanches |= Bitboard.CaseVersBitboard(3); }
                    else if (caseArrivee == 62) { cDepTour = 63; cArrTour = 61; typeTour = 9; ToursNoires &= ~Bitboard.CaseVersBitboard(63); ToursNoires |= Bitboard.CaseVersBitboard(61); }
                    else { cDepTour = 56; cArrTour = 59; typeTour = 9; ToursNoires &= ~Bitboard.CaseVersBitboard(56); ToursNoires |= Bitboard.CaseVersBitboard(59); }

                    // Mise à jour Zobrist pour la tour qui bouge pendant le roque
                    CleActuelle ^= Zobrist.Pieces[typeTour, cDepTour];
                    CleActuelle ^= Zobrist.Pieces[typeTour, cArrTour];
                }
            }
            else // Cas des Cavaliers, Fous, Tours, Reines
            {
                // 1. Mettre à jour la clé Zobrist pour la case d'arrivée
                CleActuelle ^= Zobrist.Pieces[typeAttaquant, caseArrivee];

                // 2. Mettre à jour le bitboard correspondant
                if (blanc)
                {
                    if (typeAttaquant == 1) CavaliersBlancs |= arriveeBit;
                    else if (typeAttaquant == 2) FousBlancs |= arriveeBit;
                    else if (typeAttaquant == 3)
                    {
                        ToursBlanches |= arriveeBit;
                        if (caseDepart == 7) RoqueBlancCoteRoiPossible = false;
                        if (caseDepart == 0) RoqueBlancCoteDamePossible = false;
                    }
                    else if (typeAttaquant == 4) ReineBlanche |= arriveeBit;
                }
                else
                {
                    if (typeAttaquant == 7) CavaliersNoirs |= arriveeBit;
                    else if (typeAttaquant == 8) FousNoirs |= arriveeBit;
                    else if (typeAttaquant == 9)
                    {
                        ToursNoires |= arriveeBit;
                        if (caseDepart == 63) RoqueNoirCoteRoiPossible = false;
                        if (caseDepart == 56) RoqueNoirCoteDamePossible = false;
                    }
                    else if (typeAttaquant == 10) ReineNoire |= arriveeBit;
                }
            }
            
            // =========================
            // 5. FINALISATION ZOBRIST
            // =========================

            // On calcule le NOUVEL index (après les modifs de JouerCoup)
            int indexRoqueApres = (RoqueBlancCoteRoiPossible ? 1 : 0) | (RoqueBlancCoteDamePossible ? 2 : 0) |
                                    (RoqueNoirCoteRoiPossible ? 4 : 0) | (RoqueNoirCoteDamePossible ? 8 : 0);

            // On XOR le nouvel index (qu'il soit différent ou identique, ça le remet dans la clé)
            CleActuelle ^= Zobrist.DroitsRoque[indexRoqueApres];

            // Inverser le trait (UNE SEULE FOIS)
            CleActuelle ^= Zobrist.TraitBlanc;

            if (!blanc)
                NumeroDeCoup++;

            CoteBlanc = !CoteBlanc;

            return etatPrecedent;
        }
        public bool EstPositionInitiale()
        {
            return
                PionsBlancs == 0x000000000000FF00UL &&
                CavaliersBlancs == 0x0000000000000042UL &&
                FousBlancs == 0x0000000000000024UL &&
                ToursBlanches == 0x0000000000000081UL &&
                ReineBlanche == 0x0000000000000008UL &&
                RoiBlanc == 0x0000000000000010UL &&
                PionsNoirs == 0x00FF000000000000UL &&
                CavaliersNoirs == 0x4200000000000000UL &&
                FousNoirs == 0x2400000000000000UL &&
                ToursNoires == 0x8100000000000000UL &&
                ReineNoire == 0x0800000000000000UL &&
                RoiNoir == 0x1000000000000000UL &&
                NumeroDeCoup == 1 &&
                RegleDes50Coups == 0 &&
                RoqueBlancCoteRoiPossible && RoqueBlancCoteDamePossible &&
                RoqueNoirCoteRoiPossible && RoqueNoirCoteDamePossible &&
                CaseEnPassant == -1;
        }
        public bool EstApresE4()
        {
            if (CoteBlanc) return false;
            // Toutes les pièces sauf les pions blancs doivent être identiques à la position initiale
            if (CavaliersBlancs != 0x0000000000000042UL ||
                FousBlancs != 0x0000000000000024UL ||
                ToursBlanches != 0x0000000000000081UL ||
                ReineBlanche != 0x0000000000000008UL ||
                RoiBlanc != 0x0000000000000010UL ||
                PionsNoirs != 0x00FF000000000000UL ||
                CavaliersNoirs != 0x4200000000000000UL ||
                FousNoirs != 0x2400000000000000UL ||
                ToursNoires != 0x8100000000000000UL ||
                ReineNoire != 0x0800000000000000UL ||
                RoiNoir != 0x1000000000000000UL)
                return false;
            // Les pions blancs doivent être ceux du départ sauf e2 remplacé par e4
            ulong pionsInitiaux = 0x000000000000FF00UL;
            ulong e2 = 1UL << 12;
            ulong e4 = 1UL << 28;
            return PionsBlancs == ((pionsInitiaux & ~e2) | e4);
        }

        public bool EstApresD4()
        {
            if (CoteBlanc) return false;
            // Toutes les pièces sauf les pions blancs doivent être identiques à la position initiale
            if (CavaliersBlancs != 0x0000000000000042UL ||
                FousBlancs != 0x0000000000000024UL ||
                ToursBlanches != 0x0000000000000081UL ||
                ReineBlanche != 0x0000000000000008UL ||
                RoiBlanc != 0x0000000000000010UL ||
                PionsNoirs != 0x00FF000000000000UL ||
                CavaliersNoirs != 0x4200000000000000UL ||
                FousNoirs != 0x2400000000000000UL ||
                ToursNoires != 0x8100000000000000UL ||
                ReineNoire != 0x0800000000000000UL ||
                RoiNoir != 0x1000000000000000UL)
                return false;
            // Les pions blancs doivent être ceux du départ sauf d2 remplacé par d4
            ulong pionsInitiaux = 0x000000000000FF00UL;
            ulong d2 = 1UL << 11;
            ulong d4 = 1UL << 27;
            return PionsBlancs == ((pionsInitiaux & ~d2) | d4);
        }
        public bool EstApresC4()
        {
            if (CoteBlanc) return false;

            if (CavaliersBlancs != 0x0000000000000042UL ||
                FousBlancs != 0x0000000000000024UL ||
                ToursBlanches != 0x0000000000000081UL ||
                ReineBlanche != 0x0000000000000008UL ||
                RoiBlanc != 0x0000000000000010UL ||
                PionsNoirs != 0x00FF000000000000UL ||
                CavaliersNoirs != 0x4200000000000000UL ||
                FousNoirs != 0x2400000000000000UL ||
                ToursNoires != 0x8100000000000000UL ||
                ReineNoire != 0x0800000000000000UL ||
                RoiNoir != 0x1000000000000000UL)
                return false;

            ulong pionsInitiaux = 0x000000000000FF00UL;
            ulong c2 = 1UL << 10;
            ulong c4 = 1UL << 26;

            return PionsBlancs == ((pionsInitiaux & ~c2) | c4);
        }
        public bool EstApresCf3()
        {
            if (CoteBlanc) return false;

            if (PionsBlancs != 0x000000000000FF00UL ||
                FousBlancs != 0x0000000000000024UL ||
                ToursBlanches != 0x0000000000000081UL ||
                ReineBlanche != 0x0000000000000008UL ||
                RoiBlanc != 0x0000000000000010UL ||
                PionsNoirs != 0x00FF000000000000UL ||
                CavaliersNoirs != 0x4200000000000000UL ||
                FousNoirs != 0x2400000000000000UL ||
                ToursNoires != 0x8100000000000000UL ||
                ReineNoire != 0x0800000000000000UL ||
                RoiNoir != 0x1000000000000000UL)
                return false;

            // g1 -> f3
            return CavaliersBlancs == ((0x0000000000000042UL & ~(1UL << 6)) | (1UL << 21));
        }
        private int ObtenirIndexPromotionZobrist(char promo, bool blanc)
        {   // Retourne l'index Zobrist pour la pièce promue, en fonction de la couleur.
            return promo switch
            {
                'n' => blanc ? 1 : 7,
                'b' => blanc ? 2 : 8,
                'r' => blanc ? 3 : 9,
                'q' => blanc ? 4 : 10,
                _ => 0
            };
        }
    }
}