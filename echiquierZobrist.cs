// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe (partie) de gestion de l'échiquier (gestion des clés Zobrist).
// ├─ Structure "EtatEchiquier" pour sauvegarder l'état de l'échiquier avant un coup
// ├─ "CalculerCleComplete" calcule la clé Zobrist complète à partir de l'état actuel de l'échiquier
// ├─ "ObtenirIndexPieceZobrist" retourne l'index Zobrist pour une pièce donnée sur une case donnée
// ├─ "VerifierCleZobrist" vérifie l'intégrité de la clé Zobrist actuelle
// ├─ "VerifierIntegriteZobrist" vérifie l'intégrité de la clé Zobrist actuelle (utilisé pour le débogage)
// Classe statique "Zobrist" pour gérer la table de hachage Zobrist.
// ├─ Contructeur "Zobrist" initialise la table Zobrist avec des nombres aléatoires
// └─ "GenererAleatoire" génère un nombre aléatoire de 64 bits pour la table Zobrist.

using System;
using System.Numerics;
using Athos64;

namespace Athos64
{
    public partial class Echiquier
    {
        public ulong CalculerCleComplete()
        {   // Calcule la clé Zobrist complète à partir de l'état actuel de l'échiquier (pour vérifier l'intégrité).
            ulong cle = 0;
            // 1. Les pièces
            for (int i = 0; i < 64; i++)
            {
                int typeIndex = ObtenirIndexPieceZobrist(i);
                if (typeIndex != -1)
                    cle ^= Zobrist.Pieces[typeIndex, i];
            }
            // 2. Le trait
            if (CoteBlanc) cle ^= Zobrist.TraitBlanc;
            // 3. Les droits au roque
            int indexRoque = 0;
            if (RoqueBlancCoteRoiPossible) indexRoque |= 1;
            if (RoqueBlancCoteDamePossible) indexRoque |= 2;
            if (RoqueNoirCoteRoiPossible) indexRoque |= 4;
            if (RoqueNoirCoteDamePossible) indexRoque |= 8;
            cle ^= Zobrist.DroitsRoque[indexRoque];
            // 4. L'en-passant (on utilise la colonne de la case cible)
            if (CaseEnPassant != -1)
            {
                int colonne = CaseEnPassant % 8;
                cle ^= Zobrist.ColonneEnPassant[colonne];
            }
            return cle;
        }
        private int ObtenirIndexPieceZobrist(int caseIndex)
        {   // Retourne l'index de la pièce pour la table Zobrist (0-11) ou -1 si aucune pièce sur cette case
            ulong bit = 1UL << caseIndex;
            if ((PionsBlancs & bit) != 0) return 0;
            if ((CavaliersBlancs & bit) != 0) return 1;
            if ((FousBlancs & bit) != 0) return 2;
            if ((ToursBlanches & bit) != 0) return 3;
            if ((ReineBlanche & bit) != 0) return 4;
            if ((RoiBlanc & bit) != 0) return 5;

            if ((PionsNoirs & bit) != 0) return 6;
            if ((CavaliersNoirs & bit) != 0) return 7;
            if ((FousNoirs & bit) != 0) return 8;
            if ((ToursNoires & bit) != 0) return 9;
            if ((ReineNoire & bit) != 0) return 10;
            if ((RoiNoir & bit) != 0) return 11;

            return -1;
        }
        public void VerifierCleZobrist()
        {
            // 1. On recalcule une clé totalement neuve à partir de l'état actuel des bitboards
            ulong cleRecalculee = CalculerCleComplete();

            // 2. Comparaison avec ta variable CleActuelle
            if (CleActuelle != cleRecalculee)
            {
                Console.WriteLine("\n!!! BUG ZOBRIST DETECTÉ !!!");
                Console.WriteLine($"Coup numéro : {NumeroDeCoup}");
                Console.WriteLine($"Clé Incrémentale (CleActuelle) : {CleActuelle:X}");
                Console.WriteLine($"Clé Recalculée (Correcte)    : {cleRecalculee:X}");

                // On affiche ce qui pourrait causer l'erreur
                Console.WriteLine($"Trait : {(CoteBlanc ? "Blancs" : "Noirs")}");
                Console.WriteLine($"Case En Passant actuelle : {CaseEnPassant}");

                // Bloquer ici pour inspecter si tu es en mode Debug
                throw new Exception("Dérive de la clé Zobrist !");
            }
        }

        public bool VerifierIntegriteZobrist()  // Utilisé pour Debug uniquement
        {   // Cette méthode est destinée à être utilisée après l'application d'un mouvement via JouerCoup.
            // On recalcule tout de zéro
            ulong cleTheorique = CalculerCleComplete();
            // On compare avec la clé actuelle que JouerCoup maintient
            return CleActuelle == cleTheorique;
        }

    }

    public static class Zobrist
    {   // Table de hachage Zobrist pour l'évaluation rapide des positions. Chaque entrée est un nombre aléatoire de 64 bits.
        // 12 types de pièces (6 blanches, 6 noires) x 64 cases
        public static readonly ulong[,] Pieces = new ulong[12, 64];
        public static readonly ulong TraitBlanc;
        // 16 combinaisons de roque possibles (binaire : 0000 à 1111)
        public static readonly ulong[] DroitsRoque = new ulong[16];
        // 8 colonnes possibles pour une prise en passant (+1 pour "aucune")
        public static readonly ulong[] ColonneEnPassant = new ulong[9];

        static Zobrist()
        {   // Initialisation de la table Zobrist avec des nombres aléatoires.
            // On utilise une graine fixe pour que les valeurs soient les mêmes à chaque exécution, ce qui facilite le débogage.
            Random rnd = new Random(42); // Graine fixe pour que la clé soit la même à chaque lancement
            for (int p = 0; p < 12; p++)
                for (int c = 0; c < 64; c++)
                    Pieces[p, c] = GenererAleatoire(rnd);
            TraitBlanc = GenererAleatoire(rnd);
            for (int i = 0; i < 16; i++)
                DroitsRoque[i] = GenererAleatoire(rnd);
            for (int i = 0; i < 9; i++)
                ColonneEnPassant[i] = GenererAleatoire(rnd);
        }

        private static ulong GenererAleatoire(Random rnd)
        {   // Génère un nombre aléatoire de 64 bits en combinant 8 octets aléatoires.
            byte[] buffer = new byte[8];
            rnd.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer, 0);
        }
    }
}


