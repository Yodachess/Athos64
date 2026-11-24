// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Contient les fonctions pour manipuler l'échiquier
// ├─ Constructeur statique "ProtocoleUCI" pour initialiser le protocole UCI
// ├─ _profondeur = x;  -> Profondeur de recherche par défaut
// └─ Classe "ProtocoleUCI"  
//                  ├─ "Dispose"  
//                  ├─ "Executer"
//                  ├─ "GérerException"
//                  ├─ "SurveillerFluxEntree"
//                  ├─ "DispatcherCommande"
//                  ├─ "DispatcherSurThreadPrincipal"
//                  ├─ "DispatcherSurThreadAsynchrone"
//                  ├─ "SurveillerFile"
//                  ├─ "UCI"
//                  ├─ "DefinirOption"
//                  ├─ "NouvellePartieUci"   -> GestionUCIPosition.cs
//                  ├─ "Positionner"   -> GestionUCIPosition.cs
//                  ├─ "AllerAsynchrone"   -> GestionUCIGo.cs
//                  ├─ "CreerMouvementDepuisUCI"   -> GestionUCIPosition.cs
//                  ├─ "ConvertirCoupUCI"   -> GestionUCIPosition.cs
//                  ├─ "Stopper"   -> GestionUCIGo.cs
//                  └─ "Quitter"
// └─ Classe "Jetons"  (pour séparer une commande UCI en "jetons")
//                  └─ "Parser"

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using static Athos64.Echiquier;

namespace Athos64
{
    public sealed partial class ProtocoleUCI : IDisposable
    {   // Classe pour gérer le protocole UCI (Universal Chess Interface) 
        public const long NoeudsIntervalInfo = 1_000_000;
        public const long NoeudsIntervalTemps = 1_000;
        public static int ProfondeurDeRecherche = 12;    // Profondeur de recherche par défaut
        public static int ProfondeurValeurMax = 68;     // Profondeur maximale autorisée
        public static int MultiPV = 1; // Valeur par défaut configurée dans le flux UCI
        public static int NombreThreads { get; private set; } = 4;  // Nombre de threads configure via UCI (4 par défaut)
        public static int NombreThreadsMax = Environment.ProcessorCount; // Nombre de threads maximum autorisé
        public static string NetworkFile = "nn-1a298aa575a0.nnue"; // Nom du fichier du réseau neuronal (Stockfish 19 : un seul réseau)

        public readonly GestionFlux _fluxUCI; 
        private readonly Queue<List<string>> _fileAsynchrone;
        private readonly AutoResetEvent _signalAsynchrone;
        private readonly object _verrouFile;
        private readonly Echiquier _echiquier;
        private readonly string[] _coupParDefautEtNumTour;

        private Thread _threadAsynchrone = null!;

        public ProtocoleUCI(GestionFlux messager)
        {   // Constructeur de la classe ProtocoleUCI
            _fluxUCI = messager;

            _fileAsynchrone = new Queue<List<string>>();
            _signalAsynchrone = new AutoResetEvent(false);
            _verrouFile = new object();

            _echiquier = new Echiquier();
            _echiquier.InitialiserPositionDeDepart();
            _coupParDefautEtNumTour = ["0", "1"];
        }

        public void Dispose()
        {   // Nettoyage des ressources
            _signalAsynchrone?.Dispose();
        }

        public void Executer()
        {   // Démarre l'exécution du protocole UCI
            _threadAsynchrone = new Thread(SurveillerFile) { Name = "UCI Asynchrone", IsBackground = true };
            _threadAsynchrone.Start();

            Thread.CurrentThread.Name = "UCI Synchrone";
            SurveillerFluxEntree();
        }

        public void GérerException(Exception exception)
        {   // Gère les exceptions en les enregistrant dans le flux UCI et en quittant le programme
            _fluxUCI.Log = true;
            var sb = new StringBuilder();
            var ex = exception;
            do
            {
                sb.AppendLine($"Message Exception = {ex.Message}");
                sb.AppendLine();
                sb.AppendLine($"Type Exception = {ex.GetType().FullName}.");
                sb.AppendLine();
                sb.AppendLine($"Pile d’appels = {ex.StackTrace}");
                sb.AppendLine();

                ex = ex.InnerException;
            } while (ex != null);
            _fluxUCI.EcrireLigne(sb.ToString());
            Console.Error.WriteLine("--- CRASH DÉTECTÉ ---");
            Console.Error.WriteLine(sb.ToString());
            if (!Console.IsInputRedirected)
            {   // Pause seulement dans une vraie console : avec une interface graphique l'entrée est
                // redirigée et ReadKey lèverait une exception qui masquerait l'erreur d'origine.
                Console.Error.WriteLine("Appuyez sur une touche pour quitter...");
                Console.ReadKey();
            }
            Quitter(-1);
        }

        private void SurveillerFluxEntree()
        {   // Lit les commandes UCI depuis le flux d'entrée et les traite
            try
            {
                string commande;
                do
                {   // prend en compte la ligne complète jusqu'au retour chariot
                    commande = _fluxUCI.LireLigne();
                    DispatcherCommande(commande);
                } while (commande != null);
            }
            catch (Exception ex)
            {
                GérerException(ex);
            }
        }

        private void DispatcherCommande(string commande)
        {   // Traite une commande UCI lue depuis le flux d'entrée
            if (commande == null) return;

            var jetons = Jetons.Parser(commande, ' ', '"');
            if (jetons.Count == 0) return;
            switch (jetons[0].ToLower())
            {   // On envoie certaines commandes sur le thread principal, d'autres sur le thread asynchrone
                case "go":
                    DispatcherSurThreadAsynchrone(jetons);
                    break;
                default:
                    DispatcherSurThreadPrincipal(jetons);
                    break;
            }
        }

        private void DispatcherSurThreadPrincipal(List<string> jetons)
        {   // Traite une commande UCI sur le thread principal
            var ecrireMessage = true;

            switch (jetons[0].ToLower())
            {   // On traite les commandes UCI sur le thread principal (sauf le go)
                case "uci":
                    Uci();
                    break;
                case "setoption":
                    DefinirOption(jetons);
                    break;
                case "position":
                    Positionner(jetons);
                    break;
                case "ucinewgame":
                    NouvellePartieUci();
                    break;
                case "isready":
                    _fluxUCI.EcrireLigne("readyok");
                    break;
                case "stop":
                    Stopper();
                    break;
                case "ponderhit":
                    Ponderhit();
                    break;
                case "perft":       // Compte le nombre de positions légales à partir de la position actuelle jusqu'à une profondeur donnée
                    _echiquier.TestPerft(jetons);   // ex : position intiale -> perft 1 = 20, perft 2 = 400, perft 3 = 8902, perft 4 = 197281
                    break;
                case "bench":
                    PerfBench(jetons);
                    break;
                case "eval":        // Évalue la position actuelle avec le réseau neuronal NNUE
                case "evalnnue":
                    CommandesDiagnostic.Eval(_echiquier);
                    break;
                case "evalnnuefen":
                    CommandesDiagnostic.EvalFen(jetons);
                    break;
                case "flip":
                    CommandesDiagnostic.Inverser(_echiquier);
                    break;
                case "testsee":
                    TestsSEE.Executer();
                    break;
                case "triple":
                    TestsRegles.Repetition();
                    break;
                case "50coups":
                    TestsRegles.CinquanteCoups(_echiquier);
                    break;
                case "d":
                    CommandesDiagnostic.AfficherEchiquier(_echiquier);
                    break;
                case "debug":
                    if (jetons.Count > 1)
                    {   // On modifie l'état si un argument est fourni
                        _fluxUCI.Debug = jetons[1].Equals("on", StringComparison.OrdinalIgnoreCase);
                    }   // On affiche l'état actuel dans tous les cas
                    _fluxUCI.EcrireLigne($"debug est actuellement : {(_fluxUCI.Debug ? "on" : "off")}");
                    break;
                case "help":
                case "/help":
                case "?":
                case "/?":
                    Aide.Afficher(_fluxUCI);
                    break;
                case "license":
                    Console.WriteLine("    ---       Athos64 is a chess engine for playing and analyzing       ---");
                    Console.WriteLine();
                    Console.WriteLine("Athos64 is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License ");
                    Console.WriteLine("as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.");
                    Console.WriteLine("For more information about Athos64: https://github.com/Yodachess/Athos64");
                    Console.WriteLine("See the LICENSE file distributed with Athos64 for the complete license terms.");
                    Console.WriteLine("Athos64 includes evaluation code derived from Stockfish, which is distributed under the GNU General Public License.");
                    Console.WriteLine("Athos64 comes with ABSOLUTELY NO WARRANTY, see the GNU General Public License for details.");
                    Console.WriteLine("");
                    break;
                case "quit":
                    Quitter(0);
                    break;
                default:
                    // _fluxUCI.EcrireLigne(jetons[0] + "    --- commande non supportée. ---");
                    _fluxUCI.EcrireLigne("Unknown command: " + jetons[0] + " --  Type help for more information.");
                    break;
            }
            if (ecrireMessage) _fluxUCI.EcrireLigne();
        }

        private void DispatcherSurThreadAsynchrone(List<string> jetons)
        {   //  Envoie une commande UCI à traiter sur le thread asynchrone
            lock (_verrouFile)
            {   // Ajoute la commande à la file d'attente asynchrone
                _fileAsynchrone.Enqueue(jetons);
                _signalAsynchrone.Set();
            }
        }

        private void SurveillerFile()
        {   // Surveille la file d'attente asynchrone pour traiter les commandes UCI
            try
            {
                do
                {
                    _signalAsynchrone.WaitOne();

                    List<string>? jetons = null;
                    lock (_verrouFile)
                    {
                        if (_fileAsynchrone.Count > 0) jetons = _fileAsynchrone.Dequeue();
                    }

                    if (jetons != null && jetons.Count > 0)
                    {
                        switch (jetons[0].ToLower())
                        {
                            case "go":                      // On passe les jetons à AllerAsynchrone pour qu'il 
                                AllerAsynchrone(jetons);    // puisse parser les arguments (movetime, infinite, etc.)
                                break;
                            default:
                                throw new Exception($"Impossible de traiter {jetons[0]} sur thread asynchrone.");
                        }
                        _fluxUCI.EcrireLigne();
                    }
                } while (true);
            }
            catch (Exception ex)
            {
                GérerException(ex);
            }
        }

        private void Uci()
        {   // Envoie les informations d'identification du moteur d'échecs au protocole UCI
            var version = "V 1.30 NNUE_SF19";
            if (Environment.Is64BitProcess)
                version = $"{version} x64";
            else
                version = $"{version} x86";
            _fluxUCI.EcrireLigne($"id name Athos64 {version}");
            _fluxUCI.EcrireLigne("id author Bruno Courtois");
            _fluxUCI.EcrireLigne($"{version} Copyright © 2026");
            _fluxUCI.EcrireLigne();
            _fluxUCI.EcrireLigne("option name UCI_EngineAbout type string default Athos64.");
            _fluxUCI.EcrireLigne($"option name Depth type spin default {ProfondeurDeRecherche} min 1 max 20");
            _fluxUCI.EcrireLigne($"option name Threads type spin default {NombreThreads} min 1 max {NombreThreadsMax}");
            // _fluxUCI.EcrireLigne("option name MultiPV type spin default 2 min 1 max 3");
            _fluxUCI.EcrireLigne("option name Ponder type check default true");
            _fluxUCI.EcrireLigne($"option name EvalFile type string default {NetworkFile}");
            _fluxUCI.EcrireLigne("uciok");
        }

        private void DefinirOption(List<string> jetons)
        {   // Vérification minimale : "setoption name [nom]"
            if (jetons.Count < 3)
            {
                _fluxUCI.EcrireLigne("info string Erreur: Syntaxe setoption invalide.");
                return;
            }
            if (!jetons[1].Equals("name", StringComparison.OrdinalIgnoreCase))
            {   // On s'assure que jetons[1] est bien "name" (norme UCI)
                _fluxUCI.EcrireLigne("info string Erreur: 'setoption' doit être suivi de 'name'.");
                return;
            }
            var nomOption = jetons[2];
            // On cherche "value" dans les jetons pour trouver la valeur
            var indexValue = jetons.FindIndex(j => j.Equals("value", StringComparison.OrdinalIgnoreCase));
            var valeurOption = (indexValue != -1 && indexValue + 1 < jetons.Count) ? jetons[indexValue + 1] : string.Empty;

            switch (nomOption.ToLower())
            {   // Gestion des options UCI disponibles avec ce moteur
                case "log":
                    _fluxUCI.Log = valeurOption.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
                case "depth":
                    if (int.TryParse(valeurOption, out int p))
                    {
                        ProfondeurDeRecherche = p;
                        _fluxUCI.EcrireLigne($"info string Profondeur de recherche = {ProfondeurDeRecherche}.");
                    }
                    break;
                case "threads":
                    if (int.TryParse(valeurOption, out int nbThreads) && nbThreads > 0)
                    {
                        NombreThreads = Math.Clamp(nbThreads, 1, 128);  // Bornage de sécurité (1 à 128)
                        _fluxUCI.EcrireLigne($"info string DEBUG THREADS: nombreThreads recu = {NombreThreads}");
                    }
                    break;
                /*      RALENTIT LE MOTEUR DE MANIÈRE SIGNIFICATIVE, DONC DÉSACTIVÉ POUR L'INSTANT
                case "multipv":
                    if (int.TryParse(valeurOption, out int mpv))
                    {   // On s'assure de respecter les bornes de l'option (min 1, max 3)
                        ProtocoleUCI.MultiPV = Math.Clamp(mpv, 1, 3);
                        _fluxUCI.EcrireLigne($"info string MultiPV mis sur {ProtocoleUCI.MultiPV}.");
                    }
                    break;
                */
                case "ponder":  // Option standard UCI reçue. Pas besoin de stockage complexe si 
                                // le comportement dépend directement des arguments du "go ponder".
                    _fluxUCI.EcrireLigne($"info string Option Ponder mise sur {valeurOption}.");
                    break;
                default:
                    _fluxUCI.EcrireLigne($"info string {nomOption} not supported.");
                    break;
            }
        }
		public static void PerfBench(List<string> commande)
		{   // Par défaut profondeur 6
			int profondeur = 6;
			// Si la liste contient plus d'un élément, le deuxième est notre profondeur (ex: "8")
			if (commande.Count > 1 && int.TryParse(commande[1], out int profPerso))
			{
				profondeur = profPerso;
			}
			Performance.Executer(profondeur);
		}
        private static void Quitter(int codeSortie) => Environment.Exit(codeSortie);
    }
    public static class Jetons
    {   // Classe utilitaire pour séparer une commande UCI en "jetons"
        public static List<string> Parser(string commande, char separateur, char guillemet)
        {   // Sépare une commande UCI en jetons en tenant compte des guillemets
            commande = commande.Trim();     // Nettoyage global pour éliminer les espaces au début et à la fin
            if (string.IsNullOrEmpty(commande)) return [];

            var jetons = new List<string>();
            var indiceDepart = 0;
            var dansCitation = false;

            for (var indice = 0; indice < commande.Length; indice++)
            {
                var caractere = commande[indice];
                if (indice == commande.Length - 1)
                {   // Cas du dernier caractère
                    var dernierIndice = indice + 1;
                    string jetonFinal = commande[indiceDepart..dernierIndice].TrimEnd(guillemet);
                    // On n'ajoute que si ce n'est pas vide (ex: espace traînant)
                    if (!string.IsNullOrWhiteSpace(jetonFinal))
                        jetons.Add(jetonFinal);
                    break;
                }

                if (caractere == separateur)
                {
                    if (dansCitation) continue;
                    // Ajouter un jeton intermédiaire
                    string jeton = commande[indiceDepart..indice].TrimEnd(guillemet);
                    // Sécurité : évite d'ajouter un jeton vide si l'utilisateur met 2 espaces
                    if (!string.IsNullOrWhiteSpace(jeton))
                        jetons.Add(jeton);
                    indiceDepart = indice + 1;
                }
                else if (caractere == guillemet)
                {
                    if (dansCitation)
                    {
                        dansCitation = false;
                    }
                    else
                    {   // On commence une citation : on saute le guillemet ouvrant
                        indiceDepart = indice + 1;
                        dansCitation = true;
                    }
                }
            }
            return jetons;
        }
    }
}
