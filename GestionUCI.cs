// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Contient les focntions pour manipuler l'échiquier
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
//                  ├─ "NouvellePartieUci"
//                  ├─ "Positionner"
//                  ├─ "AllerAsynchrone"
//                  ├─ "CreerMouvementDepuisUCI"
//                  ├─ "ConvertirCoupUCI"
//                  ├─ "Stopper"
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
    public sealed class ProtocoleUCI : IDisposable
    {   // Classe pour gérer le protocole UCI (Universal Chess Interface) 
        public const long NoeudsIntervalInfo = 1_000_000;
        public const long NoeudsIntervalTemps = 1_000;
        public static int ProfondeurDeRecherche = 12;    // Profondeur de recherche par défaut
        public static int ProfondeurValeurMax = 68;     // Profondeur maximale autorisée
        public static int MultiPV = 1; // Valeur par défaut configurée dans le flux UCI
        public static int NombreThreads { get; private set; } = 4;  // Nombre de threads configure via UCI (4 par défaut)
        public static int NombreThreadsMax = Environment.ProcessorCount; // Nombre de threads maximum autorisé
        public static string BigNetworkFile = "nn-c288c895ea92.nnue"; // Nom du fichier du réseau neuronal principal
        public static string SmallNetworkFile = "nn-37f18f62d772.nnue"; // Nom du fichier du réseau neuronal secondaire

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
            Console.Error.WriteLine("Appuyez sur une touche pour quitter...");
            Console.ReadKey(); // Le programme attend ici, permettant de lire l'erreur.
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
                case "eval":
                    int scoreClassique = BrunoNNUE.EvaluerNNUE(_echiquier);
                    int valeurNNUE = BrunoNNUE.Evaluer(_echiquier);
                    int scoreNNUE = BrunoNNUE.ConvertirEnCpStockfish(valeurNNUE, _echiquier);

                    Console.WriteLine($"info string Classique : {scoreClassique} cp");
                    Console.WriteLine($"info string NNUE      : {scoreNNUE} cp");
                    break;
                case "evalnnue":    // Évalue la position actuelle de l'échiquier en utilisant le réseau neuronal NNUE
                    if (!BrunoNNUE.Initialiser(ProtocoleUCI.BigNetworkFile, ProtocoleUCI.SmallNetworkFile))
                    {
                        Console.WriteLine("info string NNUE : réseaux indisponibles");
                        break;
                    }

                    int valeurNNUEBoard = BrunoNNUE.Evaluer(_echiquier);
                    int scoreNNUEBoard = BrunoNNUE.ConvertirEnCpStockfish(valeurNNUEBoard, _echiquier);

                    Console.WriteLine($"info string Evaluation NNUE : {scoreNNUEBoard} cp");
                    break;
                case "evalnnuefen":
                    if (!BrunoNNUE.Initialiser(ProtocoleUCI.BigNetworkFile, ProtocoleUCI.SmallNetworkFile))
                    {
                        Console.WriteLine("info string NNUE : réseaux indisponibles");
                        break;
                    }
                    if (jetons.Count < 2) 
                    {
                        Console.WriteLine("info string NNUE : FEN manquante");
                        break; 
                    }
                    string fenNNUE = string.Join(" ", jetons.Skip(1)); 
                    int scoreNNUEFen = BrunoNNUE.NNUE_EvalFEN(fenNNUE);
                    Console.WriteLine($"info string Evaluation NNUE à partir du FEN: {scoreNNUEFen} cp");
                    break;
                case "flip":
                    _echiquier.CoteBlanc = !_echiquier.CoteBlanc;
                    Console.WriteLine($"info string Trait inversé. Au tour des {(_echiquier.CoteBlanc ? "Blancs" : "Noirs")}.");
                    break;
                case "testsee":
                    Performance.ExecuterSuiteTestsSEE();
                    break;
                case "triple":
                    TesterRepetitionConsole();
                    break;
                case "50coups":
                    Tester50CoupsConsole(_echiquier);
                    break;
                case "d":
                    Console.WriteLine($"\nEchiquier actuel :" +
                                    $"\n------------------");
                    _echiquier.Afficher(); // Utile pour le debug console
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
        private void AllerAsynchrone(List<string> jetons)
        {   // Traite la commande "go" sur le thread asynchrone
            int profondeur = ProfondeurDeRecherche;
            var gestionTemps = new GestionTemps();
            bool estModePonder = false;
            int wtime = -1;
            int btime = -1;
            int winc = 0;
            int binc = 0;
            int movestogo = 0;

            for (var i = 1; i < jetons.Count; i++)
            {   // 1. On cherche d'abord si le flag "ponder" est présent dans la commande
                if (jetons[i].Equals("ponder", StringComparison.OrdinalIgnoreCase))
                {
                    estModePonder = true;
                    break;
                }
            }
            for (int i = 1; i < jetons.Count; i++)
            {   // 2. On parcourt les jetons pour extraire les paramètres de la commande "go"
                string jeton = jetons[i].ToLower();

                if (jeton == "infinite")
                {   // Si le mode "infinite" est demandé, on démarre la recherche infinie
                    gestionTemps.DemarrerInfini();
                    continue;
                }
                if (i + 1 >= jetons.Count)  // On s'assure qu'il y a un argument après le jeton actuel
                    continue;
                switch (jeton)
                {
                    case "depth":
                        if (int.TryParse(jetons[++i], out int p))
                            profondeur = p;
                        break;
                    case "movetime":
                        if (int.TryParse(jetons[++i], out int t))
                            gestionTemps.Demarrer(t);
                        break;
                    case "wtime":
                        int.TryParse(jetons[++i], out wtime);
                        break;
                    case "btime":
                        int.TryParse(jetons[++i], out btime);
                        break;
                    case "winc":
                        int.TryParse(jetons[++i], out winc);
                        break;
                    case "binc":
                        int.TryParse(jetons[++i], out binc);
                        break;
                    case "movestogo":
                        int.TryParse(jetons[++i], out movestogo);
                        break;
                }
            }

            // Si aucun movetime n'a été donné mais qu'on dispose du temps restant
            if (wtime >= 0 && btime >= 0)
            {
                int tempsRestant = _echiquier.CoteBlanc ? wtime : btime;
                int increment = _echiquier.CoteBlanc ? winc : binc;

                int tempsPourCeCoup;

                if (movestogo > 0)
                {   // Contrôle du type "40 coups en 2 heures"
                    tempsPourCeCoup = tempsRestant / movestogo;
                }
                else
                {   // Contrôle Fischer
                    if (tempsRestant > 300000)          // > 5 min
                        tempsPourCeCoup = tempsRestant / 20;
                    else if (tempsRestant > 60000)      // > 1 min
                        tempsPourCeCoup = tempsRestant / 25;
                    else if (tempsRestant > 10000)      // > 10 s
                        tempsPourCeCoup = tempsRestant / 30;
                    else
                        tempsPourCeCoup = tempsRestant / 40;
                }

                // On profite presque entièrement de l'incrément
                tempsPourCeCoup += increment * 8 / 10;

                // Toujours conserver un peu de temps
                tempsPourCeCoup = Math.Min(tempsPourCeCoup, tempsRestant - 100);

                // Minimum
                tempsPourCeCoup = Math.Max(20, tempsPourCeCoup);

                gestionTemps.Demarrer(tempsPourCeCoup);
                _fluxUCI.EcrireDebug($"Temps restant={tempsRestant}  incr={increment}  alloué={tempsPourCeCoup}");
            }

            _fluxUCI.EcrireDebug($"Lancement recherche profondeur: {profondeur}");

            // 3. Recherche avec récupération du tuple
            // Affichage du bestmove final (seul endroit autorisé à le faire)
            var resultatRecherche = Recherche.Chercher(_echiquier, profondeur, gestionTemps, (info) => _fluxUCI.EcrireLigne(info));
            string pvTexte = resultatRecherche.LignePV ?? "";
            string[] coupsPV = pvTexte.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (coupsPV.Length >= 2)
            {
                _fluxUCI.EcrireLigne($"bestmove {coupsPV[0]} ponder {coupsPV[1]}");
            }
            else if (coupsPV.Length == 1)
            {
                _fluxUCI.EcrireLigne($"bestmove {coupsPV[0]}");
            }
            else if (resultatRecherche.MeilleurCoup.HasValue)
            {
                string coupTxt = _echiquier.TraduireEnNotationAlgebraique(resultatRecherche.MeilleurCoup.Value);
                _fluxUCI.EcrireLigne($"bestmove {coupTxt}");
            }
            else
            {
                _fluxUCI.EcrireLigne("bestmove (none)");
            }
            GC.Collect(1, GCCollectionMode.Forced, false);
        }
        private void Uci()
        {   // Envoie les informations d'identification du moteur d'échecs au protocole UCI
            var version = "V 1.25 NNUE_SF18";
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
            _fluxUCI.EcrireLigne($"option name EvalFile type string default {BigNetworkFile}");
            _fluxUCI.EcrireLigne($"option name EvalFileSmall type string default {SmallNetworkFile}");
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
		public static void TesterRepetitionConsole()
        {   // Teste la détection de la répétition triple dans une séquence de coups
            Echiquier e = new();
            ChargementFen.ChargerFen(e, Echiquier.FenDepart, true);  // DemiCoupActuel passe à 0
            // --- SÉQUENCE DE COUPS (Allers-retours de Cavaliers) ---
            e.JouerCoup(6, 21);     // 1. Nf3 (g1 -> f3)
            e.JouerCoup(62, 45);    // 1... Nf6 (g8 -> f6)
            e.JouerCoup(21, 6);     // 2. Ng1 (f3 -> g1) - Retour Blancs (2ème apparition position initiale)
            e.JouerCoup(45, 62);    // 2... Ng8 (f6 -> g8) - Retour Noirs
            e.JouerCoup(6, 21);     // 3. Nf3 (g1 -> f3)
            e.JouerCoup(62, 45);    // 3... Nf6 (g8 -> f6)
            // 4. Ng1 (f3 -> g1) - Ce coup va provoquer la TRIPLE répétition au pli suivant
            var etatPrecedent = e.JouerCoup(21, 6);
            // On appelle Negamax en simulant un nœud enfant (distanceRacine = 1)
            // On s'attend à ce qu'il voie la répétition et coupe immédiatement à 0
            int score = Recherche.Negamax(e, profondeur: 2, distanceRacine: 1, alpha: -10000, beta: 10000);
            e.AnnulerCoup(etatPrecedent);
            if (score == 0)
            {   // Affichage du verdict dans la console
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" [OK] Test Répétition : Negamax a bien renvoyé 0 (Nulle).");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($" [ÉCHEC] Test Répétition : Negamax a renvoyé {score} au lieu de 0.");
            }
            Console.ResetColor();
            Console.WriteLine("Appuyez sur ENTRÉE pour continuer...");
            Console.ReadLine();
        }
        public static void Tester50CoupsConsole(Echiquier e)
        {   // Teste la règle des 50 coups dans une séquence de coups
            Console.WriteLine("\n--- TEST UNITAIRE : RÈGLE DES 50 COUPS ---");
            // On charge un FEN où le compteur (le 5ème champ "99") dit qu'on est à 99 demi-coups sans capture ni pion
            ChargementFen.ChargerFen(e, "8/8/8/8/4k3/8/8/4K3 w - - 99 75", true);
            Console.WriteLine($"[DEBUG] Compteur après FEN = {e.RegleDes50Coups}"); // Doit afficher 99
            // Roi blanc bouge : e1 (4) -> f1 (5). Ce coup fait passer le compteur à 100 !
            var etatPrecedent = e.JouerCoup(4, 5);
            // On appelle Negamax au pli enfant (distanceRacine = 1)
            int score = Recherche.Negamax(e, 2, 1, -10000, 10000);
            // On nettoie le coup de test
            e.AnnulerCoup(etatPrecedent);
            if (score == 0)     // Résultat
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" [OK] Test 50 Coups : Le compteur à 100 (50 coups complets) renvoie bien 0.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($" [ÉCHEC] Test 50 Coups : Score de {score} au lieu de 0.");
            }
            Console.ResetColor();
            Console.WriteLine("Appuyez sur ENTRÉE pour continuer...");
            Console.ReadLine();
        }

        private  static void Stopper()
        {   // On force l'exception via l'instance de GestionTemps en cours
            Recherche.GestionTempsEnCours?.DemanderArret();
        }
        private static void Ponderhit()
        {   // On signale au gestionnaire de temps que le coup attendu a été joué.
            // Cela transforme la recherche infinie (pondering) en recherche limitée dans le temps.
            Recherche.GestionTempsEnCours?.SignalerPonderhit();
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
