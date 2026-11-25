// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe de la table de transposition.
// ├─ Structure "EntreeTT" représente une entrée lue dans la table de transposition
// ├─ Structure "Case" une case de la table : deux mots de 64 bits (données compactées, clé XOR données)
// ├─ Contructeur "TableTransposition" initialise la table de transposition avec une taille donnée
// ├─ "Vider" remet à zéro toutes les entrées de la table
// ├─ "CalculerIndexTable" calcule l'index dans la table à partir d'une clé
// ├─ "Stocker" stocke une entrée dans la table de transposition
// ├─ "Recuperer" récupère une entrée de la table de transposition
// └─ "Compacter" / "Decompacter" passage entre une entrée et son mot de 64 bits
//
// Sans verrou (2026-11-25) : le verrou prenait près de 20 % du temps de la recherche au profileur, même avec un seul thread.
// Chaque case garde la clé XOR les données (méthode « lockless » de Hyatt, utilisée par Crafty et Stockfish) :
// si un autre thread écrit la case pendant qu'on la lit, la clé recalculée ne correspond plus et l'entrée est ignorée.

using System;
using System.Diagnostics;
using Athos64;

namespace Athos64
{
    public class TableTransposition
    {
        public enum TypeBorne : byte
        {
            Exact = 0,           // Le score est exact (PV-Node)
            BorneInferieure = 1, // Le score est au moins égal à Beta (Fail-high)
            BorneSuperieure = 2  // Le score est au plus égal à Alpha (Fail-low)
        }

        public struct EntreeTT
        {
            public ulong CleZobrist;        // Signature complète pour éviter les collisions
            public int Score;               // Valeur de la position
            public int Profondeur;          // Profondeur de recherche lors du stockage
            public TypeBorne Type;          // Alpha, Beta ou Exact

            // Infos du coup
            public int CaseDepartMeilleur;
            public int CaseArriveeMeilleur;
            public char? PromotionMeilleur;
            public bool Existe;             // Flag d'existence
        }

        private struct Case
        {
            public ulong CleXorDonnees;     // Clé Zobrist ^ Donnees
            public ulong Donnees;           // Entrée compactée (0 = case vide)
        }

        // Disposition du mot de données :
        // ├─ bits  0-20 : score + 2^20 (21 bits, de -1 048 576 à 1 048 575 : couvre INFINI)
        // ├─ bits 21-36 : profondeur + 2^15 (16 bits)
        // ├─ bits 37-38 : type de borne + 1 (0 = case vide)
        // ├─ bits 39-44 : case de départ du meilleur coup ; bits 45-50 : case d'arrivée
        // └─ bit 51 : promotion présente ; bits 52-59 : caractère de la promotion
        private const int DECALAGE_SCORE = 1 << 20;
        private const int DECALAGE_PROFONDEUR = 1 << 15;

        private readonly Case[] table;
        private readonly ulong masque;
        private ulong CalculerIndexTable(ulong cle) => cle & masque;
        public TableTransposition(int tailleMo)
        {
            // Nombre d'entrées selon la taille réelle d'une case (16 octets) ; il était calculé avec la taille de l'ancienne
            // structure EntreeTT, ce qui n'utilisait qu'une partie de la mémoire (2026-11-25)
            int tailleEntree = System.Runtime.CompilerServices.Unsafe.SizeOf<Case>();
            int nombreEntreesSouhaite = (tailleMo * 1024 * 1024) / tailleEntree;
            int nombreEntrees = (int)Math.Pow(2, Math.Floor(Math.Log(nombreEntreesSouhaite, 2)));
            table = new Case[nombreEntrees];
            masque = (ulong)(nombreEntrees - 1);
        }

        public void Vider()
        {   // Appelé entre deux recherches, aucun thread ne lit la table à ce moment
            Array.Clear(table, 0, table.Length);
        }

        private static ulong Compacter(int score, int profondeur, TypeBorne type, Echiquier.Mouvement coup)
        {
            Debug.Assert(score >= -DECALAGE_SCORE && score < DECALAGE_SCORE);
            Debug.Assert(profondeur >= -DECALAGE_PROFONDEUR && profondeur < DECALAGE_PROFONDEUR);
            ulong donnees = (uint)(score + DECALAGE_SCORE)
                | ((ulong)(uint)(profondeur + DECALAGE_PROFONDEUR) << 21)
                | ((ulong)(uint)((int)type + 1) << 37)
                | ((ulong)(uint)(coup.CaseDepart & 63) << 39)
                | ((ulong)(uint)(coup.CaseArrivee & 63) << 45);
            if (coup.Promotion.HasValue)
                donnees |= (1UL << 51) | ((ulong)(coup.Promotion.Value & 0xFF) << 52);
            return donnees;
        }

        private static EntreeTT Decompacter(ulong cle, ulong donnees)
        {
            return new EntreeTT
            {
                CleZobrist = cle,
                Score = (int)(donnees & 0x1FFFFF) - DECALAGE_SCORE,
                Profondeur = (int)((donnees >> 21) & 0xFFFF) - DECALAGE_PROFONDEUR,
                Type = (TypeBorne)(((donnees >> 37) & 3) - 1),
                CaseDepartMeilleur = (int)((donnees >> 39) & 63),
                CaseArriveeMeilleur = (int)((donnees >> 45) & 63),
                PromotionMeilleur = ((donnees >> 51) & 1) != 0 ? (char)((donnees >> 52) & 0xFF) : null,
                Existe = true
            };
        }

        public void Stocker(ulong cle, int score, int profondeur, TypeBorne type, Echiquier.Mouvement meilleurCoup)
        {
            ulong index = CalculerIndexTable(cle);
            ulong anciennes = table[index].Donnees;
            bool existe = anciennes != 0;
            ulong ancienneCle = table[index].CleXorDonnees ^ anciennes;
            int ancienneProfondeur = (int)((anciennes >> 21) & 0xFFFF) - DECALAGE_PROFONDEUR;
            if (!existe || ancienneCle != cle || profondeur >= ancienneProfondeur)
            {
                ulong donnees = Compacter(score, profondeur, type, meilleurCoup);
                table[index].Donnees = donnees;
                table[index].CleXorDonnees = cle ^ donnees;
            }
        }

        public EntreeTT? Recuperer(ulong cle)
        {
            ulong index = CalculerIndexTable(cle);
            ulong donnees = table[index].Donnees;
            if (donnees != 0 && (table[index].CleXorDonnees ^ donnees) == cle)
            {
                return Decompacter(cle, donnees);
            }
            return null;
        }
    }
}
