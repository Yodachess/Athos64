// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe (partie) de gestion de l'échiquier (gestion des mouvements).
// ├─ Structure "Mouvement" représente un mouvement sur l'échiquier avec toutes les informations nécessaires.
// ├─ "GenererCapturesLegales" génère tous les mouvements de capture légaux
// ├─ "GenererCapturesPourBitboard" génère tous les mouvements de capture pour un bitboard donné
// ├─ "AjouterCaptures" ajoute des mouvements de capture à une liste donnée
// ├─ "GenererMouvementsLegauxCommun" génère tous les mouvements légaux pour le côté actif
// │  ├─ "GenererMouvementsBlancs" génère les mouvements légaux des pièces blanches
// │  └─ "GenererMouvementsNoirs" génère les mouvements légaux des pièces noires
// ├─ "RoiEnEchec" vérifie si le roi du côté actif est en échec
// ├─ "EstPat" vérifie si le roi du côté actif est pat  (inutilisé ??)
// ├─ "CasesAttaqueesPar" retourne un bitboard des cases attaquées par le côté donné
// ├─ "RoiEnEchecApresCoup" vérifie si le roi du côté actif serait en échec après un coup donné
// ├─ "AppliquerPromotion" applique une promotion de pion si nécessaire
// ├─ "RoiEstAttaque" vérifie si le roi du côté actif est attaqué par une pièce ennemie
// ├─ "ObtenirPieceCapturee" retourne le type de pièce capturée sur une case donnée
// ├─ Clouages : voir echiquierClouages.cs ("ObtenirPiecesClouees", "DetecterClouageDirection", "EstPieceGlissanteValide")
// └─ Perft et notation des coups : voir echiquierPerft.cs

using Athos64;
using System;
using System.Numerics;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Athos64
{
    public partial class Echiquier
    {
        public struct Mouvement(int caseDepart, int caseArrivee, Echiquier.TypePiece piece, 
            Echiquier.TypePiece? pieceCapturee = null, char? promotion = null, bool estPriseEnPassant = false, bool estRoque = false)
        {   // Représente un mouvement sur l'échiquier, avec toutes les informations nécessaires pour la validation et la simulation.
            public int CaseDepart { get; } = caseDepart;
            public int CaseArrivee { get; } = caseArrivee;
            public TypePiece Piece { get; } = piece;
            public TypePiece? PieceCapturee { get; } = pieceCapturee;
            public char? Promotion { get; } = promotion;
            public bool EstPriseEnPassant { get; } = estPriseEnPassant;
            public bool EstRoque { get; } = estRoque;
            public int ScoreTri = 0; // Champ pour le tri des mouvements (ex: dans l'ordonnancement des coups)
        }

        public void GenererCapturesLegales(List<Mouvement> captures, bool inclureCoupsCalmesSiEchec = true)
        {   // On vide la liste du pool avant de commencer
            captures.Clear();

            bool enEchec = RoiEnEchec(CoteBlanc);
            if (enEchec && inclureCoupsCalmesSiEchec)
            {   // Si on est en échec, on doit générer tous les coups légaux pour se protéger
                GenererMouvementsLegauxCommun(captures); // La méthode qui génère TOUT
                return;
            }

            // Génère tous les mouvements de capture légaux (y compris les promotions avec capture et les prises en passant).
            ulong occupations = ObtenirToutesLesPieces();
            ulong amies = CoteBlanc ? ObtenirPiecesBlanches() : ObtenirPiecesNoires();
            ulong ennemies = CoteBlanc ? ObtenirPiecesNoires() : ObtenirPiecesBlanches();

            if (CoteBlanc)
            {   // --- Pions Blancs (Captures uniquement) ---
                ulong pions = PionsBlancs;
                while (pions != 0)
                {
                    int de = BitOperations.TrailingZeroCount(pions);
                    ulong masqueDe = 1UL << de;
                    // 1. Captures diagonales classiques
                    // On utilise une méthode spécifique aux attaques pour ne pas calculer les poussées
                    ulong attaques = Pion.ObtenirAttaquesBlanches(masqueDe) & ennemies;
                    AjouterCaptures(captures, de, attaques, TypePiece.Pion, ennemies);
                    // 2. Prise en passant
                    if (CaseEnPassant != -1)
                    {
                        ulong ep = Pion.ObtenirPrisesEnPassantBlancs(masqueDe, CaseEnPassant);
                        if (ep != 0 && !RoiEnEchecApresCoup(de, CaseEnPassant, true, null, true))
                        {
                            captures.Add(new Mouvement(de, CaseEnPassant, TypePiece.Pion, TypePiece.Pion, null, true));
                        }
                    }
                    pions &= ~masqueDe;
                }

                // --- Autres pièces Blanches ---
                GenererCapturesPourBitboard(captures, CavaliersBlancs, TypePiece.Cavalier, de => Cavalier.ObtenirMouvements(1UL << de, amies) & ennemies, ennemies);
                GenererCapturesPourBitboard(captures, FousBlancs, TypePiece.Fou, de => Fou.ObtenirMouvements(1UL << de, occupations, amies) & ennemies, ennemies);
                GenererCapturesPourBitboard(captures, ToursBlanches, TypePiece.Tour, de => Tour.ObtenirMouvements(1UL << de, occupations, amies) & ennemies, ennemies);
                GenererCapturesPourBitboard(captures, ReineBlanche, TypePiece.Reine, de => Reine.ObtenirMouvements(1UL << de, occupations, amies) & ennemies, ennemies);
                GenererCapturesPourBitboard(captures, RoiBlanc, TypePiece.Roi, de => Roi.ObtenirMouvements(1UL << de, amies) & ennemies, ennemies);
            }
            else
            {   // On analyse le coté Noir.
                // --- Pions Noirs (Captures uniquement) ---
                ulong pions = PionsNoirs;
                while (pions != 0)
                {
                    int de = BitOperations.TrailingZeroCount(pions);
                    ulong masqueDe = 1UL << de;
                    // 1. Captures diagonales classiques
                    ulong attaques = Pion.ObtenirAttaquesNoires(masqueDe) & ennemies;
                    AjouterCaptures(captures, de, attaques, TypePiece.Pion, ennemies);
                    // 2. Prise en passant
                    if (CaseEnPassant != -1)
                    {
                        ulong ep = Pion.ObtenirPrisesEnPassantNoirs(masqueDe, CaseEnPassant);
                        if (ep != 0 && !RoiEnEchecApresCoup(de, CaseEnPassant, false, null, true))
                        {
                            captures.Add(new Mouvement(de, CaseEnPassant, TypePiece.Pion, TypePiece.Pion, null, true));
                        }
                    }
                    pions &= ~masqueDe;
                }

                // --- Autres pièces Noires ---
                GenererCapturesPourBitboard(captures, CavaliersNoirs, TypePiece.Cavalier, de => Cavalier.ObtenirMouvements(1UL << de, amies) & ennemies, ennemies);
                GenererCapturesPourBitboard(captures, FousNoirs, TypePiece.Fou, de => Fou.ObtenirMouvements(1UL << de, occupations, amies) & ennemies, ennemies);
                GenererCapturesPourBitboard(captures, ToursNoires, TypePiece.Tour, de => Tour.ObtenirMouvements(1UL << de, occupations, amies) & ennemies, ennemies);
                GenererCapturesPourBitboard(captures, ReineNoire, TypePiece.Reine, de => Reine.ObtenirMouvements(1UL << de, occupations, amies) & ennemies, ennemies);
                GenererCapturesPourBitboard(captures, RoiNoir, TypePiece.Roi, de => Roi.ObtenirMouvements(1UL << de, amies) & ennemies, ennemies);
            }
        }
        private void GenererCapturesPourBitboard(List<Mouvement> liste, ulong bitboardPieces, TypePiece type, Func<int, ulong> generateurCibles, ulong ennemies)
        {
            while (bitboardPieces != 0)
            {
                int de = BitOperations.TrailingZeroCount(bitboardPieces);
                ulong ciblesAttaquees = generateurCibles(de);
                // Sécurité : uniquement les pièces ennemies
                ciblesAttaquees &= ennemies;
                AjouterCaptures(liste, de, ciblesAttaquees, type, ennemies);
                bitboardPieces &= ~(1UL << de);
            }
        }
        private void AjouterCaptures(List<Mouvement> liste, int de, ulong cibles, TypePiece type, ulong ennemies)
        {   // Ajoute à la liste les mouvements de capture légaux pour une pièce donnée
            // partant de 'de' vers les cases indiquées dans 'cibles'.
            while (cibles != 0)
            {
                int a = BitOperations.TrailingZeroCount(cibles);
                // On vérifie la légalité du coup (ne laisse pas le roi en échec)
                if (!RoiEnEchecApresCoup(de, a, CoteBlanc))
                {
                    TypePiece? capturee = ObtenirPieceCapturee(a, CoteBlanc);
                    // Cas particulier : Promotion avec capture
                    bool estLignePromotion = (CoteBlanc && a >= 56) || (!CoteBlanc && a <= 7);
                    if (type == TypePiece.Pion && estLignePromotion)
                    {   // En Quiescence Search, on ne teste souvent que la promotion Reine ('q')
                        // pour limiter l'explosion combinatoire.
                        liste.Add(new Mouvement(de, a, type, capturee, 'q'));
                    }
                    else
                    {
                        liste.Add(new Mouvement(de, a, type, capturee));
                    }
                }
                cibles &= ~(1UL << a);
            }
        }

        public void GenererMouvementsLegauxCommun(List<Mouvement> mouvements)
        {   // Génère tous les mouvements légaux. Retourne la liste de mouvements (caseDepart, caseArrivee, promotion).

            mouvements.Clear(); // On vide la liste
            ulong occupations = ObtenirToutesLesPieces();
            ulong occupationsAmies = CoteBlanc ? ObtenirPiecesBlanches() : ObtenirPiecesNoires();
            ulong occupationsEnnemies = CoteBlanc ? ObtenirPiecesNoires() : ObtenirPiecesBlanches();

            if (CoteBlanc)
                GenererMouvementsBlancs(mouvements, occupations, occupationsAmies, occupationsEnnemies);
            else
                GenererMouvementsNoirs(mouvements, occupations, occupationsAmies, occupationsEnnemies);
        }

        // Mouvements légaux des pièces blanches (pions, cavaliers, fous, tours, reine, roi avec roques)
        private void GenererMouvementsBlancs(List<Mouvement> mouvements, ulong occupations, ulong occupationsAmies, ulong occupationsEnnemies)
        {
            // --- Pions Blancs ---
            ulong pions = PionsBlancs;
            while (pions != 0)
            {
                int caseDepart = BitOperations.TrailingZeroCount(pions);
                ulong masqueDepart = Bitboard.CaseVersBitboard(caseDepart);

                // Mouvements normaux et captures
                ulong mouvementsPiece = Pion.ObtenirMouvementsBlancs(masqueDepart, occupations, occupationsEnnemies);
                while (mouvementsPiece != 0)
                {
                    int caseArrivee = BitOperations.TrailingZeroCount(mouvementsPiece);
                    ulong masqueArrivee = Bitboard.CaseVersBitboard(caseArrivee);
                    if (!RoiEnEchecApresCoup(caseDepart, caseArrivee, CoteBlanc))
                    {
                        bool estCapture = (masqueArrivee & occupationsEnnemies) != 0;
                        TypePiece? capture = estCapture ? ObtenirPieceCapturee(caseArrivee, true) : null;

                        // 1. Détecter la vraie pièce capturée (important pour la simulation d'échec)
                        bool estCapture2 = (Bitboard.CaseVersBitboard(caseArrivee) & occupationsEnnemies) != 0;
                        TypePiece? capture2 = estCapture2 ? ObtenirPieceCapturee(caseArrivee, true) : null;

                        // 2. Gérer la promotion
                        if ((Bitboard.CaseVersBitboard(caseArrivee) & Bitboard.LignePromotionBlancs) != 0)
                        {
                            char[] typesPromotion = { 'q', 'r', 'b', 'n' };
                            foreach (char type in typesPromotion)
                            {   // On vérifie la légalité pour CHAQUE type de promotion
                                if (!RoiEnEchecApresCoup(caseDepart, caseArrivee, CoteBlanc, type))
                                {
                                    mouvements.Add(new Mouvement(caseDepart, caseArrivee, TypePiece.Pion, capture2, type));
                                }
                            }
                        }

                        // 3. Gérer le mouvement normal (non-promotion)
                        else
                        {
                            if (!RoiEnEchecApresCoup(caseDepart, caseArrivee, CoteBlanc))
                            {
                                mouvements.Add(new Mouvement(caseDepart, caseArrivee, TypePiece.Pion, capture2));
                            }
                        }
                    }
                    mouvementsPiece &= ~Bitboard.CaseVersBitboard(caseArrivee);
                }

                // Prises en passant
                if (CaseEnPassant != -1)
                {
                    ulong prisesEnPassant = Pion.ObtenirPrisesEnPassantBlancs(masqueDepart, CaseEnPassant);
                    while (prisesEnPassant != 0)
                    {
                        int caseArrivee = BitOperations.TrailingZeroCount(prisesEnPassant);
                        if (!RoiEnEchecApresCoup(caseDepart, caseArrivee, CoteBlanc, null, true))
                        {
                            mouvements.Add(new Mouvement(
                                caseDepart,
                                caseArrivee,
                                TypePiece.Pion,
                                TypePiece.Pion,     // capture forcée
                                null,
                                estPriseEnPassant: true
                            ));
                        }
                        prisesEnPassant &= ~Bitboard.CaseVersBitboard(caseArrivee);
                    }
                }
                pions &= ~masqueDepart;
            }

            // --- Cavaliers Blancs ---
            ulong cavaliers = CavaliersBlancs;
            while (cavaliers != 0)
            {
                int caseDepart = BitOperations.TrailingZeroCount(cavaliers);
                ulong masqueDepart = Bitboard.CaseVersBitboard(caseDepart);
                ulong mouvementsPiece = Cavalier.ObtenirMouvements(masqueDepart, occupationsAmies);
                while (mouvementsPiece != 0)
                {
                    int caseArrivee = BitOperations.TrailingZeroCount(mouvementsPiece);
                    ulong masqueArrivee = Bitboard.CaseVersBitboard(caseArrivee);
                    // On détecte le VRAI type de la pièce capturée
                    bool estCapture = (masqueArrivee & occupationsEnnemies) != 0;
                    TypePiece? capture = estCapture ? ObtenirPieceCapturee(caseArrivee, true) : null;
                    if (!RoiEnEchecApresCoup(caseDepart, caseArrivee, CoteBlanc))
                    {
                        mouvements.Add(new Mouvement(caseDepart, caseArrivee, TypePiece.Cavalier, capture));
                    }
                    mouvementsPiece &= ~masqueArrivee;
                }
                cavaliers &= ~masqueDepart;
            }

            // --- Fous Blancs ---
            ulong fous = FousBlancs;
            while (fous != 0)
            {
                int caseDepart = BitOperations.TrailingZeroCount(fous);
                ulong masqueDepart = Bitboard.CaseVersBitboard(caseDepart);
                ulong mouvementsPiece = Fou.ObtenirMouvements(masqueDepart, occupations, occupationsAmies);
                while (mouvementsPiece != 0)
                {
                    int caseArrivee = BitOperations.TrailingZeroCount(mouvementsPiece);
                    ulong masqueArrivee = Bitboard.CaseVersBitboard(caseArrivee);
                    bool estCapture = (masqueArrivee & occupationsEnnemies) != 0;
                    TypePiece? capture = estCapture ? ObtenirPieceCapturee(caseArrivee, true) : null;
                    if (!RoiEnEchecApresCoup(caseDepart, caseArrivee, CoteBlanc))
                    {
                        mouvements.Add(new Mouvement(caseDepart, caseArrivee, TypePiece.Fou, capture));
                    }
                   mouvementsPiece &= ~masqueArrivee;
                }
                fous &= ~masqueDepart;
            }

            // --- Tours Blanches ---
            ulong tours = ToursBlanches;
            while (tours != 0)
            {
                int caseDepart = BitOperations.TrailingZeroCount(tours);
                ulong masqueDepart = Bitboard.CaseVersBitboard(caseDepart);
                ulong mouvementsPiece = Tour.ObtenirMouvements(masqueDepart, occupations, occupationsAmies);
                while (mouvementsPiece != 0)
                {
                    int caseArrivee = BitOperations.TrailingZeroCount(mouvementsPiece);
                    ulong masqueArrivee = Bitboard.CaseVersBitboard(caseArrivee);
                    bool estCapture = (masqueArrivee & occupationsEnnemies) != 0;
                    TypePiece? capture = estCapture ? ObtenirPieceCapturee(caseArrivee, true) : null;
                    if (!RoiEnEchecApresCoup(caseDepart, caseArrivee, CoteBlanc))
                    {   // On enregistre la Tour avec sa cible réelle
                        mouvements.Add(new Mouvement(caseDepart, caseArrivee, TypePiece.Tour, capture));
                    }
                    mouvementsPiece &= ~masqueArrivee;
                }
                tours &= ~masqueDepart;
            }

            // --- Reine Blanche ---
            ulong reine = ReineBlanche;
            while (reine != 0)
            {
                int caseDepart = BitOperations.TrailingZeroCount(reine);
                ulong masqueDepart = Bitboard.CaseVersBitboard(caseDepart);
                ulong mouvementsPiece = Reine.ObtenirMouvements(masqueDepart, occupations, occupationsAmies);
                while (mouvementsPiece != 0)
                {
                    int caseArrivee = BitOperations.TrailingZeroCount(mouvementsPiece);
                    ulong masqueArrivee = Bitboard.CaseVersBitboard(caseArrivee);
                    bool estCapture = (masqueArrivee & occupationsEnnemies) != 0;
                    TypePiece? capture = estCapture ? ObtenirPieceCapturee(caseArrivee, true) : null;
                    if (!RoiEnEchecApresCoup(caseDepart, caseArrivee, CoteBlanc))
                    {
                        mouvements.Add(new Mouvement(caseDepart, caseArrivee, TypePiece.Reine, capture));
                    }
                    mouvementsPiece &= ~masqueArrivee;
                }
                reine &= ~masqueDepart;
            }

            // --- Roi Blanc (inclut le roque) ---
            ulong roi = RoiBlanc;
            if (roi != 0)
            {
                int caseDepart = BitOperations.TrailingZeroCount(roi);
                ulong masqueDepart = Bitboard.CaseVersBitboard(caseDepart);
                ulong casesAttaquees = CasesAttaqueesPar(false); // Attaques des noirs
                // Mouvements normaux du roi
                ulong mouvementsPiece = Roi.ObtenirMouvements(masqueDepart, occupationsAmies);
                // Filtrage des cases attaquées
                mouvementsPiece &= ~casesAttaquees;
                while (mouvementsPiece != 0)
                {
                    int caseArrivee = BitOperations.TrailingZeroCount(mouvementsPiece);
                    ulong masqueArrivee = Bitboard.CaseVersBitboard(caseArrivee);
                    bool estCapture = (masqueArrivee & occupationsEnnemies) != 0;
                    TypePiece? capture = estCapture ? ObtenirPieceCapturee(caseArrivee, true) : null; // true car ennemi noir
                    if (!RoiEnEchecApresCoup(caseDepart, caseArrivee, CoteBlanc))
                    {
                        mouvements.Add(new Mouvement(caseDepart, caseArrivee, TypePiece.Roi, capture));
                    }
                    mouvementsPiece &= ~masqueArrivee;
                }

                // 2. ROQUE (Version sécurisée)
                // Le roque est interdit si le Roi est en échec
                if ((masqueDepart & casesAttaquees) == 0)
                {   // Petit Roque (O-O)
                    if (RoqueBlancCoteRoiPossible)
                    {   // Vérifier cases vides entre Roi et Tour (f1, g1)
                        // ET vérifier que f1 n'est pas attaquée
                        bool f1Vide = (occupations & (1UL << 5)) == 0;
                        bool g1Vide = (occupations & (1UL << 6)) == 0;
                        bool f1PasAttaquee = (casesAttaquees & (1UL << 5)) == 0;
                        bool g1PasAttaquee = (casesAttaquees & (1UL << 6)) == 0;
                        if (f1Vide && g1Vide && f1PasAttaquee && g1PasAttaquee)
                        {
                            mouvements.Add(new Mouvement(caseDepart, 6, TypePiece.Roi, null, null, false, estRoque: true));
                        }
                    }
                    // Grand Roque (O-O-O)
                    if (RoqueBlancCoteDamePossible)
                    {   // Vérifier cases vides (d1, c1, b1) 
                        // ET vérifier que d1 et c1 ne sont pas attaquées
                        bool d1Vide = (occupations & (1UL << 3)) == 0;
                        bool c1Vide = (occupations & (1UL << 2)) == 0;
                        bool b1Vide = (occupations & (1UL << 1)) == 0;
                        bool d1PasAttaquee = (casesAttaquees & (1UL << 3)) == 0;
                        bool c1PasAttaquee = (casesAttaquees & (1UL << 2)) == 0;
                        if (d1Vide && c1Vide && b1Vide && d1PasAttaquee && c1PasAttaquee)
                        {
                            mouvements.Add(new Mouvement(caseDepart, 2, TypePiece.Roi, null, null, false, estRoque: true));
                        }
                    }
                }
            }
        }

        // Mouvements légaux des pièces noires (pions, cavaliers, fous, tours, reine, roi avec roques)
        private void GenererMouvementsNoirs(List<Mouvement> mouvements, ulong occupations, ulong occupationsAmies, ulong occupationsEnnemies)
        {
            // --- Pions Noirs ---
            ulong pions = PionsNoirs;
            while (pions != 0)
            {
                int caseDepart = BitOperations.TrailingZeroCount(pions);
                ulong masqueDepart = Bitboard.CaseVersBitboard(caseDepart);

                // 1. Mouvements normaux et captures
                ulong mouvementsPiece = Pion.ObtenirMouvementsNoirs(masqueDepart, occupations, occupationsEnnemies);
                while (mouvementsPiece != 0)
                {
                    int caseArrivee = BitOperations.TrailingZeroCount(mouvementsPiece);
                    ulong masqueArrivee = Bitboard.CaseVersBitboard(caseArrivee);
                    // Détection de la vraie pièce capturée pour la simulation
                    bool estCapture = (masqueArrivee & occupationsEnnemies) != 0;
                    TypePiece? capture = estCapture ? ObtenirPieceCapturee(caseArrivee, false) : null; // false car l'ennemi est Blanc
                    // Cas de la Promotion
                    if ((masqueArrivee & Bitboard.LignePromotionNoirs) != 0)
                    {
                        char[] typesPromotion = { 'q', 'r', 'b', 'n' };
                        foreach (char type in typesPromotion)
                        {
                            if (!RoiEnEchecApresCoup(caseDepart, caseArrivee, false, type))
                            {
                                mouvements.Add(new Mouvement(caseDepart, caseArrivee, TypePiece.Pion, capture, type));
                            }
                        }
                    }
                    else
                    {   // Mouvement normal
                        if (!RoiEnEchecApresCoup(caseDepart, caseArrivee, false))
                        {
                            mouvements.Add(new Mouvement(caseDepart, caseArrivee, TypePiece.Pion, capture));
                        }
                    }
                    mouvementsPiece &= ~masqueArrivee;
                }

                // 2. Prises en passant
                if (CaseEnPassant != -1)
                {
                    ulong prisesEnPassant = Pion.ObtenirPrisesEnPassantNoirs(masqueDepart, CaseEnPassant);
                    while (prisesEnPassant != 0)
                    {
                        int caseArrivee = BitOperations.TrailingZeroCount(prisesEnPassant);
                        if (!RoiEnEchecApresCoup(caseDepart, caseArrivee, false, null, true))
                        {
                            mouvements.Add(new Mouvement(
                                caseDepart,
                                caseArrivee,
                                TypePiece.Pion,
                                TypePiece.Pion, // Toujours un pion en EP
                                null,
                                estPriseEnPassant: true
                            ));
                        }
                        prisesEnPassant &= ~Bitboard.CaseVersBitboard(caseArrivee);
                    }
                }
                pions &= ~masqueDepart;
            }

            // --- Cavaliers Noirs ---
            ulong cavaliers = CavaliersNoirs;
            while (cavaliers != 0)
            {
                int caseDepart = BitOperations.TrailingZeroCount(cavaliers);
                ulong masqueDepart = Bitboard.CaseVersBitboard(caseDepart);
                ulong mouvementsPiece = Cavalier.ObtenirMouvements(masqueDepart, occupationsAmies);
                while (mouvementsPiece != 0)
                {
                    int caseArrivee = BitOperations.TrailingZeroCount(mouvementsPiece);
                    ulong masqueArrivee = Bitboard.CaseVersBitboard(caseArrivee);
                    if (!RoiEnEchecApresCoup(caseDepart, caseArrivee, false))
                    {
                        TypePiece piece = TypePiece.Cavalier;
                        TypePiece? capture = (masqueArrivee & occupationsEnnemies) != 0
                            ? ObtenirPieceCapturee(caseArrivee, false) : null;
                        mouvements.Add(new Mouvement(caseDepart, caseArrivee, piece, capture));
                    }
                    mouvementsPiece &= ~masqueArrivee;
                }
                cavaliers &= ~masqueDepart;
            }

            // --- Fous Noirs ---
            ulong fous = FousNoirs;
            while (fous != 0)
            {
                int caseDepart = BitOperations.TrailingZeroCount(fous);
                ulong masqueDepart = Bitboard.CaseVersBitboard(caseDepart);
                ulong mouvementsPiece = Fou.ObtenirMouvements(masqueDepart, occupations, occupationsAmies);
                while (mouvementsPiece != 0)
                {
                    int caseArrivee = BitOperations.TrailingZeroCount(mouvementsPiece);
                    ulong masqueArrivee = Bitboard.CaseVersBitboard(caseArrivee);
                    if (!RoiEnEchecApresCoup(caseDepart, caseArrivee, false))
                    {
                        TypePiece piece = TypePiece.Fou;
                        TypePiece? capture = (masqueArrivee & occupationsEnnemies) != 0
                            ? ObtenirPieceCapturee(caseArrivee, false) : null;
                        mouvements.Add(new Mouvement(caseDepart, caseArrivee, piece, capture));
                    }
                    mouvementsPiece &= ~masqueArrivee;
                }
                fous &= ~masqueDepart;
            }

            // --- Tours Noires ---
            ulong tours = ToursNoires;
            while (tours != 0)
            {
                int caseDepart = BitOperations.TrailingZeroCount(tours);
                ulong masqueDepart = Bitboard.CaseVersBitboard(caseDepart);
                ulong mouvementsPiece = Tour.ObtenirMouvements(masqueDepart, occupations, occupationsAmies);
                while (mouvementsPiece != 0)
                {
                    int caseArrivee = BitOperations.TrailingZeroCount(mouvementsPiece);
                    ulong masqueArrivee = Bitboard.CaseVersBitboard(caseArrivee);
                    if (!RoiEnEchecApresCoup(caseDepart, caseArrivee, false))
                    {
                        TypePiece piece = TypePiece.Tour;
                        TypePiece? capture = (masqueArrivee & occupationsEnnemies) != 0
                            ? ObtenirPieceCapturee(caseArrivee, false) : null;
                        mouvements.Add(new Mouvement(caseDepart, caseArrivee, piece, capture));
                    }
                    mouvementsPiece &= ~masqueArrivee;
                }
                tours &= ~masqueDepart;
            }

            // --- Reine Noire ---
            ulong reine = ReineNoire;
            while (reine != 0)
            {
                int caseDepart = BitOperations.TrailingZeroCount(reine);
                ulong masqueDepart = Bitboard.CaseVersBitboard(caseDepart);
                ulong mouvementsPiece = Reine.ObtenirMouvements(masqueDepart, occupations, occupationsAmies);
                while (mouvementsPiece != 0)
                {
                    int caseArrivee = BitOperations.TrailingZeroCount(mouvementsPiece);
                    ulong masqueArrivee = Bitboard.CaseVersBitboard(caseArrivee);
                    if (!RoiEnEchecApresCoup(caseDepart, caseArrivee, false))
                    {
                        TypePiece piece = TypePiece.Reine;
                        TypePiece? capture = (masqueArrivee & occupationsEnnemies) != 0
                            ? ObtenirPieceCapturee(caseArrivee, false) : null;
                        mouvements.Add(new Mouvement(caseDepart, caseArrivee, piece, capture));
                    }
                    mouvementsPiece &= ~masqueArrivee;
                }
                reine &= ~masqueDepart;
            }

            // --- Roi Noir (inclut le roque) ---
            ulong roi = RoiNoir;
            if (roi != 0)
            {
                int caseDepart = BitOperations.TrailingZeroCount(roi);
                ulong masqueDepart = Bitboard.CaseVersBitboard(caseDepart);
                ulong casesAttaquees = CasesAttaqueesPar(true); // Attaques des Blancs
                ulong mouvementsPiece = Roi.ObtenirMouvements(masqueDepart, occupationsAmies);
                mouvementsPiece &= ~casesAttaquees;
                while (mouvementsPiece != 0)
                {
                    int caseArrivee = BitOperations.TrailingZeroCount(mouvementsPiece);
                    ulong masqueArrivee = 1UL << caseArrivee;
                    if (!RoiEnEchecApresCoup(caseDepart, caseArrivee, false))
                    {
                        TypePiece piece = TypePiece.Roi;
                        TypePiece? capture = (masqueArrivee & occupationsEnnemies) != 0
                            ? ObtenirPieceCapturee(caseArrivee, false) : null;
                        mouvements.Add(new Mouvement(caseDepart, caseArrivee, piece, capture));
                    }
                    mouvementsPiece &= ~masqueArrivee;
                }

                // 2. ROQUE NOIR
                // Le roque est strictement interdit si le Roi est actuellement en échec
                if ((masqueDepart & casesAttaquees) == 0)
                {   // Petit Roque Noir (O-O) - Cases f8(61) et g8(62)
                    if (RoqueNoirCoteRoiPossible)
                    {
                        bool f8Vide = (occupations & (1UL << 61)) == 0;
                        bool g8Vide = (occupations & (1UL << 62)) == 0;
                        bool f8PasAttaquee = (casesAttaquees & (1UL << 61)) == 0;
                        bool g8PasAttaquee = (casesAttaquees & (1UL << 62)) == 0;
                        if (f8Vide && g8Vide && f8PasAttaquee && g8PasAttaquee)
                        {
                            mouvements.Add(new Mouvement(caseDepart, 62, TypePiece.Roi, null, null, false, estRoque: true));
                        }
                    }
                    // Grand Roque Noir (O-O-O) - Cases d8(59), c8(58), b8(57)
                    if (RoqueNoirCoteDamePossible)
                    {
                        bool d8Vide = (occupations & (1UL << 59)) == 0;
                        bool c8Vide = (occupations & (1UL << 58)) == 0;
                        bool b8Vide = (occupations & (1UL << 57)) == 0;
                        bool d8PasAttaquee = (casesAttaquees & (1UL << 59)) == 0;
                        bool c8PasAttaquee = (casesAttaquees & (1UL << 58)) == 0;
                        // Note : b8 doit être vide, mais n'a pas besoin d'être à l'abri des attaques
                        if (d8Vide && c8Vide && b8Vide && d8PasAttaquee && c8PasAttaquee)
                        {
                            mouvements.Add(new Mouvement(caseDepart, 58, TypePiece.Roi, null, null, false, estRoque: true));
                        }
                    }
                }
            }
        }

        public bool RoiEnEchec(bool roiBlanc)
        {   // Vérifie si le roi est en échec.
            ulong roi = roiBlanc ? RoiBlanc : RoiNoir;
            if (roi == 0) return false;
            ulong attaquesAdverses = CasesAttaqueesPar(!roiBlanc);
            return (attaquesAdverses & roi) != 0;
        }
        public bool EstPat()
        {   // Vérifie si la position est un pat (pas de coup légal et roi pas en échec).
            if (RoiEnEchec(CoteBlanc))      // Le joueur courant ne doit PAS être en échec
                return false;
            return GenererMouvementsLegaux() == 0;
        }

        public ulong CasesAttaqueesPar(bool parBlanc)
        {   // Retourne un bitboard des cases attaquées par les pièces du camp spécifié.
            ulong attaques = 0UL;
            ulong occupations = ObtenirToutesLesPieces();
            if (parBlanc)
            {
                attaques |= ((PionsBlancs << 7) & ~Bitboard.ColonneH);
                attaques |= ((PionsBlancs << 9) & ~Bitboard.ColonneA);
                attaques |= Cavalier.ObtenirAttaques(CavaliersBlancs);
                attaques |= Fou.ObtenirAttaques(FousBlancs, occupations);
                attaques |= Tour.ObtenirAttaques(ToursBlanches, occupations);
                attaques |= Reine.ObtenirAttaques(ReineBlanche, occupations);
                attaques |= Roi.ObtenirAttaques(RoiBlanc);
            }
            else
            {
                attaques |= ((PionsNoirs >> 9) & ~Bitboard.ColonneH);
                attaques |= ((PionsNoirs >> 7) & ~Bitboard.ColonneA);
                attaques |= Cavalier.ObtenirAttaques(CavaliersNoirs);
                attaques |= Fou.ObtenirAttaques(FousNoirs, occupations);
                attaques |= Tour.ObtenirAttaques(ToursNoires, occupations);
                attaques |= Reine.ObtenirAttaques(ReineNoire, occupations);
                attaques |= Roi.ObtenirAttaques(RoiNoir);
            }
            return attaques;
        }

        public bool RoiEnEchecApresCoup(int caseDepart, int caseArrivee, bool blancJoue, char? promotion = null, bool estPriseEnPassant = false)
        {   // Simule le coup et vérifie si le roi du camp qui vient de jouer est en échec après ce coup.
            ulong fromMask = Bitboard.CaseVersBitboard(caseDepart);
            ulong toMask = Bitboard.CaseVersBitboard(caseArrivee);

            // Copies locales des Bitboards
            ulong pBlanc = PionsBlancs; ulong pNoir = PionsNoirs;
            ulong cBlanc = CavaliersBlancs; ulong cNoir = CavaliersNoirs;
            ulong fBlanc = FousBlancs; ulong fNoir = FousNoirs;
            ulong tBlanc = ToursBlanches; ulong tNoir = ToursNoires;
            ulong qBlanc = ReineBlanche; ulong qNoir = ReineNoire;
            ulong rBlanc = RoiBlanc; ulong rNoir = RoiNoir;

            // --- 1. GESTION DE LA CAPTURE ---
            if (estPriseEnPassant)
            {   // En passant : on retire le pion qui est DERRIÈRE la case d'arrivée
                if (blancJoue) pNoir &= ~(toMask >> 8);
                else pBlanc &= ~(toMask << 8);
            }
            else
            {   // Capture normale : on nettoie la case d'arrivée
                pBlanc &= ~toMask; pNoir &= ~toMask;
                cBlanc &= ~toMask; cNoir &= ~toMask;
                fBlanc &= ~toMask; fNoir &= ~toMask;
                tBlanc &= ~toMask; tNoir &= ~toMask;
                qBlanc &= ~toMask; qNoir &= ~toMask;
            }

            // --- 2. DÉPLACEMENT ET PROMOTION ---
            if (blancJoue)
            {
                if ((pBlanc & fromMask) != 0)
                {   // On retire la pièce de la case de départ
                    pBlanc &= ~fromMask;
                    if (promotion == null) pBlanc |= toMask;
                    else AppliquerPromotion(promotion.Value, true, ref qBlanc, ref tBlanc, ref fBlanc, ref cBlanc, toMask);
                }
                else if ((cBlanc & fromMask) != 0) { cBlanc &= ~fromMask; cBlanc |= toMask; }
                else if ((fBlanc & fromMask) != 0) { fBlanc &= ~fromMask; fBlanc |= toMask; }
                else if ((tBlanc & fromMask) != 0) { tBlanc &= ~fromMask; tBlanc |= toMask; }
                else if ((qBlanc & fromMask) != 0) { qBlanc &= ~fromMask; qBlanc |= toMask; }
                else if ((rBlanc & fromMask) != 0) { rBlanc &= ~fromMask; rBlanc |= toMask; }

                return RoiEstAttaque(rBlanc, false, pBlanc, pNoir, cBlanc, cNoir, fBlanc, fNoir, tBlanc, tNoir, qBlanc, qNoir, rBlanc, rNoir);
            }
            else
            {
                if ((pNoir & fromMask) != 0)
                {
                    pNoir &= ~fromMask;
                    if (promotion == null) pNoir |= toMask;
                    else AppliquerPromotion(promotion.Value, false, ref qNoir, ref tNoir, ref fNoir, ref cNoir, toMask);
                }
                else if ((cNoir & fromMask) != 0) { cNoir &= ~fromMask; cNoir |= toMask; }
                else if ((fNoir & fromMask) != 0) { fNoir &= ~fromMask; fNoir |= toMask; }
                else if ((tNoir & fromMask) != 0) { tNoir &= ~fromMask; tNoir |= toMask; }
                else if ((qNoir & fromMask) != 0) { qNoir &= ~fromMask; qNoir |= toMask; }
                else if ((rNoir & fromMask) != 0) { rNoir &= ~fromMask; rNoir |= toMask; }

                return RoiEstAttaque(rNoir, true, pBlanc, pNoir, cBlanc, cNoir, fBlanc, fNoir, tBlanc, tNoir, qBlanc, qNoir, rBlanc, rNoir);
            }
        }

        private void AppliquerPromotion(char type, bool blanc, ref ulong q, ref ulong t, ref ulong f, ref ulong c, ulong mask)
        {   // Retire le pion et place la pièce promue
            switch (type)
            {
                case 'q': q |= mask; break;
                case 'r': t |= mask; break;
                case 'b': f |= mask; break;
                case 'n': c |= mask; break;
            }
        }
        private bool RoiEstAttaque(
            ulong roi, bool roiNoir, ulong pBlanc, ulong pNoir, ulong cBlanc, ulong cNoir, ulong fBlanc, ulong fNoir,
                                        ulong tBlanc, ulong tNoir, ulong qBlanc, ulong qNoir, ulong rBlanc, ulong rNoir)
        {
            ulong occup = pBlanc | pNoir | cBlanc | cNoir | fBlanc | fNoir | tBlanc | tNoir | qBlanc | qNoir | rBlanc | rNoir;

            ulong attaques =
                // pions
                (roiNoir ? Pion.ObtenirAttaquesBlanches(pBlanc) : Pion.ObtenirAttaquesNoires(pNoir))
                // cavaliers
                | Cavalier.ObtenirAttaques(roiNoir ? cBlanc : cNoir)
                // fous
                | Fou.ObtenirAttaques(roiNoir ? fBlanc : fNoir, occup)
                // tours
                | Tour.ObtenirAttaques(roiNoir ? tBlanc : tNoir, occup)
                // reines
                | Reine.ObtenirAttaques(roiNoir ? qBlanc : qNoir, occup)
                // Et enfin, le roi adverse
                | Roi.ObtenirAttaques(roiNoir ? rBlanc : rNoir);

            return (attaques & roi) != 0;
        }
        private TypePiece? ObtenirPieceCapturee(int caseArrivee, bool ennemiEstNoir)
        {   // Retourne le type de la pièce capturée sur la case d'arrivée, ou null s'il n'y a pas de capture.
            ulong m = Bitboard.CaseVersBitboard(caseArrivee);
            if (ennemiEstNoir)
            {
                if ((PionsNoirs & m) != 0) return TypePiece.Pion;
                if ((CavaliersNoirs & m) != 0) return TypePiece.Cavalier;
                if ((FousNoirs & m) != 0) return TypePiece.Fou;
                if ((ToursNoires & m) != 0) return TypePiece.Tour;
                if ((ReineNoire & m) != 0) return TypePiece.Reine;
                if ((RoiNoir & m) != 0) return TypePiece.Roi;
            }
            else
            {
                if ((PionsBlancs & m) != 0) return TypePiece.Pion;
                if ((CavaliersBlancs & m) != 0) return TypePiece.Cavalier;
                if ((FousBlancs & m) != 0) return TypePiece.Fou;
                if ((ToursBlanches & m) != 0) return TypePiece.Tour;
                if ((ReineBlanche & m) != 0) return TypePiece.Reine;
                if ((RoiBlanc & m) != 0) return TypePiece.Roi;
            }
            return null;
        }
    }
}
    
