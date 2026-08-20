// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe de la table de transposition.
// ├─ Structure "EntreeTT" représente une entrée dans la table de transposition
// ├─ Contructeur "TableTransposition" initialise la table de transposition avec une taille donnée
// ├─ "Vider" remet à zéro toutes les entrées de la table
// ├─ "CalculerIndexTable" calcule l'index dans la table à partir d'une clé
// ├─ "Stocker" stocke une entrée dans la table de transposition
// └─ "Recuperer" récupère une entrée de la table de transposition

using System;
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

        private readonly EntreeTT[] table;
        private readonly ulong masque;
        private readonly object verrou = new(); // Verrou pour sécuriser le multi-threading
        private ulong CalculerIndexTable(ulong cle) => cle & masque;
        public TableTransposition(int tailleMo)
        {
            int tailleEntree = System.Runtime.InteropServices.Marshal.SizeOf(typeof(EntreeTT));
            int nombreEntreesSouhaite = (tailleMo * 1024 * 1024) / tailleEntree;
            int nombreEntrees = (int)Math.Pow(2, Math.Floor(Math.Log(nombreEntreesSouhaite, 2)));
            table = new EntreeTT[nombreEntrees];
            masque = (ulong)(nombreEntrees - 1);
        }

        public void Vider()
        {
            lock (verrou)
            {
                Array.Clear(table, 0, table.Length);
            }
        }
        public void Stocker(ulong cle, int score, int profondeur, TypeBorne type, Echiquier.Mouvement meilleurCoup)
        {
            ulong index = CalculerIndexTable(cle);
            lock (verrou)
            {
                if (profondeur >= table[index].Profondeur || table[index].CleZobrist != cle || !table[index].Existe)
                {
                    table[index].CleZobrist = cle;
                    table[index].Score = score;
                    table[index].Profondeur = profondeur;
                    table[index].Type = type;
                    table[index].Existe = true;
                    table[index].CaseDepartMeilleur = meilleurCoup.CaseDepart;
                    table[index].CaseArriveeMeilleur = meilleurCoup.CaseArrivee;
                    table[index].PromotionMeilleur = meilleurCoup.Promotion;
                }
            }
        }
        public EntreeTT? Recuperer(ulong cle)
        {
            ulong index = CalculerIndexTable(cle);
            lock (verrou)
            {
                if (table[index].Existe && table[index].CleZobrist == cle)
                {
                    return table[index];
                }
            }
            return null;
        }
    }
}