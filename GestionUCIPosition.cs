// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Protocole UCI (partie) : nouvelle partie, commande "position" et conversion des coups.
// ├─ "NouvellePartieUci" vide la table de transposition et remet la position de départ
// ├─ "Positionner" traite "position [startpos | fen ...] [moves ...]"
// ├─ "CreerMouvementDepuisUCI" crée un mouvement à partir d'un coup en notation UCI (ex: e2e4, a7a8q)
// └─ "ConvertirMouvementEnUCI" convertit un mouvement en notation UCI

using System;
using System.Collections.Generic;
using System.Linq;
using static Athos64.Echiquier;

namespace Athos64
{
    public sealed partial class ProtocoleUCI
    {
        private void NouvellePartieUci()
        {
            try
            {
                    Recherche.ReinitialiserMoteur(Recherche.TT);
                    _echiquier.InitialiserPositionDeDepart();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"info string Erreur nouvelle partie: {ex.Message}");
            }
        }
        private void Positionner(List<string> jetons)
        {
            if (jetons.Count < 2)
            {   // Validation de base : la commande doit avoir au moins "position startpos" ou "position fen ..."
                _fluxUCI.EcrireLigne("info string [ERREUR] Syntaxe invalide. Usage: position [startpos | fen <fen>] [moves <coups>]");
                return;
            }
            var specifierCoups = false;
            var indexCoup = jetons.Count;
            for (var i = 1; i < jetons.Count; i++)
            {   // Recherche du mot-clé "moves"
                if (string.Equals(jetons[i], "moves", StringComparison.OrdinalIgnoreCase))
                {
                    specifierCoups = true;
                    indexCoup = i + 1;
                    break;
                }
            }
            string fen;     // 3. Détermination de la position initiale (FEN)
            if (jetons[1].Equals("startpos", StringComparison.OrdinalIgnoreCase))
            {
                fen = Echiquier.FenDepart;
            }
            else if (jetons[1].Equals("fen", StringComparison.OrdinalIgnoreCase))
            {   // On récupère tout ce qui est entre "fen" et "moves" (si présent)
                int finFen = specifierCoups ? jetons.IndexOf("moves") : jetons.Count;
                if (finFen <= 2)
                {
                    _fluxUCI.EcrireLigne("info string [ERREUR] FEN manquante après mot-clé 'fen'.");
                    return;
                }
                fen = string.Join(" ", jetons.GetRange(2, finFen - 2));
            }
            else
            {
                _fluxUCI.EcrireLigne($"info string [ERREUR] Argument inconnu : {jetons[1]}");
                return;
            }

            try
            {   // 4. Chargement de l'échiquier
                ChargementFen.ChargerFen(_echiquier, fen, false);
                _echiquier.CleActuelle = _echiquier.CalculerCleComplete();
            }
            catch (Exception ex)
            {
                _fluxUCI.EcrireLigne($"info string [ERREUR] Chargement FEN échoué : {ex.Message}");
                return;
            }

            // Jeu des coups (si "moves" trouvé)
            while (indexCoup < jetons.Count)
            {
                var coupUCI = jetons[indexCoup];
                try
                {
                    var coupVoulu = CreerMouvementDepuisUCI(coupUCI, _echiquier);
                    List<Mouvement> coupsLegaux = [];
                    _echiquier.GenererMouvementsLegauxCommun(coupsLegaux);

                    var coupTrouvé = coupsLegaux.FirstOrDefault(c =>
                        c.CaseDepart == coupVoulu.CaseDepart &&
                        c.CaseArrivee == coupVoulu.CaseArrivee &&
                        char.ToLower(c.Promotion ?? ' ') == char.ToLower(coupVoulu.Promotion ?? ' '));

                    // Si le coup n'est pas trouvé (Vérification par défaut de struct)
                    if (coupTrouvé.CaseDepart == 0 && coupTrouvé.CaseArrivee == 0)
                    {
                        _fluxUCI.EcrireLigne($"info string [ERREUR] Coup {coupUCI} illégal.");
                        break;
                    }
                    _echiquier.JouerCoup(coupTrouvé.CaseDepart, coupTrouvé.CaseArrivee, coupTrouvé.Promotion);
                }
                catch (Exception ex)
                {
                    _fluxUCI.EcrireLigne($"info string [ERREUR] Parsing coup {coupUCI} impossible : {ex.Message}");
                    break;
                }
                indexCoup++;
            }
            // _echiquier.Afficher(); // Utile pour le debug console
            // Je remplace par la commande d qui affiche l'échiquier dans la console
        }

        public static Mouvement CreerMouvementDepuisUCI(string uciCoup, Echiquier echiquier)
        {
            // 1. Conversion des cases via la classe Bitboard (uciCoup est par exemple "e2e4" ou "a7a8q")
            int de = Bitboard.CaseVersIndex(uciCoup.Substring(0, 2));
            int vers = Bitboard.CaseVersIndex(uciCoup.Substring(2, 2));

            // 2. Identifier la pièce qui bouge (utilise la méthode IdentifierTypePiece)
            Echiquier.TypePiece piece = echiquier.IdentifierTypePiece(de);

            // 3. Gérer la promotion (le 5ème caractère, s'il existe)
            char? promotion = uciCoup.Length == 5 ? uciCoup[4] : null;

            // 4. Identifier une éventuelle capture (On vérifie si la case d'arrivée est occupée)
            Echiquier.TypePiece? pieceCapturee = null;
            if ((echiquier.ObtenirToutesLesPieces() & Bitboard.CaseVersBitboard(vers)) != 0)
            {
                pieceCapturee = echiquier.IdentifierTypePiece(vers);
            }

            // 5. Détecter le roque (Si le roi se déplace de 2 cases horizontalement)
            bool estRoque = (piece == Echiquier.TypePiece.Roi) && Math.Abs(de % 8 - vers % 8) == 2;

            // 6. Détecter la prise en passant (Si c'est un pion qui change de colonne vers une case vide)
            bool estEP = (piece == Echiquier.TypePiece.Pion) && (de % 8 != vers % 8) && (pieceCapturee == null);

            // Retourne l'objet Mouvement avec toutes les infos
            return new Mouvement(de, vers, piece, pieceCapturee, promotion, estEP, estRoque);
        }
        public static string ConvertirMouvementEnUCI(Mouvement m)
        {
            string de = Bitboard.IndexVersCase(m.CaseDepart);
            string vers = Bitboard.IndexVersCase(m.CaseArrivee);
            string promo = m.Promotion.HasValue ? m.Promotion.Value.ToString().ToLower() : "";
            return de + vers + promo;
        }
    }
}
