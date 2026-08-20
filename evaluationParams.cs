// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe des poids de la fonction d'évaluation.
// ├─ "ChargerDepuisIni" charge les paramètres depuis un fichier ini
// ├─ "SauvegarderVersIni" sauvegarde les paramètres dans un fichier ini (pour les tests)
// └─ Liste des paramètres par défaut si aucun fichier ini n'est trouvé.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Athos64
{
    public static class EvalParams
    {
        public static void ChargerDepuisIni(string chemin)
        {
            if (!File.Exists(chemin))
            {
                Console.WriteLine("[INFO] Aucun fichier eval.ini trouvé. Utilisation des paramètres internes par défaut.");
                return;
            }
            string premiereLigne = File.ReadLines(chemin).FirstOrDefault();
            if (premiereLigne != null)
            {   // Affiche la première ligne pour confirmer le chargement du bon fichier
                Console.WriteLine($"[INFO] Chargement de : {Path.GetFileName(chemin)} (Début: {premiereLigne})");
            }
            var type = typeof(EvalParams);
            foreach (var ligne in File.ReadAllLines(chemin))
            {
                string l = ligne.Trim();
                if (string.IsNullOrEmpty(l) || l.StartsWith(";") || l.StartsWith("[")) continue;

                var parties = l.Split('=');
                if (parties.Length != 2) continue;

                string nomChamp = parties[0].Trim();
                string valeurStr = parties[1].Trim();

                FieldInfo champ = type.GetField(nomChamp, BindingFlags.Public | BindingFlags.Static);

                if (champ != null && !champ.IsLiteral)
                {   // Ajout de la condition !champ.IsLiteral pour éviter l'erreur de constante
                    try
                    {
                        if (champ.FieldType == typeof(int))
                        {
                            champ.SetValue(null, int.Parse(valeurStr));
                        }
                        else if (champ.FieldType == typeof(double))
                        {
                            champ.SetValue(null, double.Parse(valeurStr, CultureInfo.InvariantCulture));
                        }
                        else if (champ.FieldType == typeof(int[]))
                        {
                            int[] tableau = [.. valeurStr.Split(',').Select(int.Parse)];
                            champ.SetValue(null, tableau);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERREUR] Impossible de charger le champ {nomChamp} : {ex.Message}");
                    }
                }
            }
            Console.WriteLine("[INFO] Tous les paramètres ont été chargés et appliqués.\n");
        }

        public static void SauvegarderVersIni(string chemin)
        {
            var type = typeof(EvalParams);
            using (StreamWriter sw = new StreamWriter(chemin))
            {
                foreach (var champ in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    object valeur = champ.GetValue(null);

                    if (valeur is int[] tab)
                    {
                        sw.WriteLine($"{champ.Name}={string.Join(",", tab)}");
                    }
                    else if (valeur is double d)
                    {
                        sw.WriteLine($"{champ.Name}={d.ToString(CultureInfo.InvariantCulture)}");
                    }
                    else
                    {
                        sw.WriteLine($"{champ.Name}={valeur}");
                    }
                }
            }
            // Console.WriteLine("[INFO] Paramètres sauvegardés automatiquement dans le fichier ini.");
        }

        // --- PARAMETRES UTILISÉS POUR L'ÉVALUATION SI PAS DE FICHIER INI ---

        // VALEURS MATÉRIELLES
        public static int[] ValeursMG = { 100, 320, 340, 500, 990, 0 };
        public static int[] ValeursEG = { 120, 300, 320, 550, 900, 0 };
        public static int[] ValeursPhase = { 1, 1, 1, 2, 3, 0 };
        public static int[] MalusDangerRoi = { 0, 0, 5, 25, 60, 120, 250, 450, 900 };

        // FACTEUR GLOBAL
        public static double FacteurPositionnel = 0.45;

        // BONUS / MALUS PIONS
        public static int MalusPionDouble = 15;
        public static int MalusPionTriple = 35;
        public static int MalusPionIsole = 15;
        // Pénalités spécifiques pour les pions doublés
        public static int MalusPionDoubleRelie = 15;        // Moins grave car ils se soutiennent/couvrent
        public static int MalusPionDoubleIsole = 45;        // Très vulnérable
        public static int MalusPionDoubleExpose = 25;       // Sur colonne sans pion adverse (cible facile pour les tours)
        public static int MalusPionDoubleBloqueFou = 30;    // Gêne la diagonale du fou ami
        public static int BonusSoutienPion = 10;     

        // Îlots
        public static int MalusIlotMG = 12;
        public static int MalusIlotEG = 25;

        // PIONS PASSÉS
        public static int[] BonusPionPasseMG = { 0, 10, 15, 25, 40, 80, 150, 0 };
        public static int[] BonusPionPasseEG = { 0, 15, 25, 45, 80, 150, 300, 0 };
        public static int MalusPionPasseBloque = 20;
        public static int BonusPionPasseProtege = 15;
        public static int[] BonusCarrePionPasse = { 5, 8, 12, 18, 25, 35, 0, 0 };
        public static int[] MalusCarrePionPasse = { 5, 10, 20, 35, 55, 90, 0, 0 };

        // MOBILITÉ
        public static int MobiliteCavalierMG = 4;
        public static int MobiliteCavalierEG = 2;
        public static int MobiliteFouMG = 3;
        public static int MobiliteFouEG = 2;
        public static int MobiliteTourMG = 2;
        public static int MobiliteTourEG = 3;
        public static int MobiliteDameMG = 1;
        public static int MobiliteDameEG = 2;

        // DÉVELOPPEMENT
        public static int MalusCavalierCaseDepart = 15;
        public static int MalusFouDameCaseDepart = 15;
        public static int MalusFouRoiCaseDepart = 25;
        public static int MalusEvalFouBloqueur = 60;
        public static int BonusPionCentreDeveloppement = 20;

        // CENTRE
        public static int BonusControleCentre = 25;

        // Centre fermé
        public static int BonusCavalierCentreFerme = 8;  // Non utilisé pour l'instant
        public static int MalusFouCentreFerme = 6;       // Non utilisé pour l'instant

        // FOU
        public static int BonusPaireFousCentreOuvert = 20;  // Non utilisé pour l'instant
        public static int MalusFouMauvaiseCouleur = 8;      // Mauvais Fou
        public static int BonusPaireFous = 40;              // PAIRE DE FOUS

        // DAME PRÉCOCE
        public static int MalusDamePrecoce = 40;

        // TOURS
        public static int BonusTourColonneOuverte = 25;
        public static int BonusTourColonneSemiOuverte = 15;
        public static int BonusTourDerrierePionPasse = 10;
        public static int BonusTourDerrierePionPasseAdverse = 15;

        // ROI

        public static int BonusRoiRoque = 60;
        public static int BonusRoiPretRoquer = 15;
        public static int MalusRoiSansRoque = 60;

        // Bouclier de pions
        public static int MalusBouclierFaible = 20;
        public static int MalusBouclierSansPionG = 70;
        public static int MalusBouclierTresFaible = 85;
        public static int MalusRoiExposed = 120;

        // AVANT-POSTES
        public static int BonusAvantPosteCavalier = 20;
    }
}