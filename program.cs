// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe principale "Main" du moteur d'échecs.
// ├─ "Main" est le point d'entrée du programme
// Classe de gestion du flux d'entrée et de sortie du moteur d'échecs.
// ├─ "GestionFlux" Constructeur de la classe GestionFlux
// ├─ "LireLigne" lit une ligne du flux d'entrée et la retourne sous forme de chaîne de caractères
// ├─ "EcrireLigne" écrit une ligne dans le flux de sortie
// ├─ "EcrireDebug" écrit un message de débogage dans la console si le mode debug est activé
// └─ "DisposeAsync" libère les ressources utilisées par la classe GestionFlux de manière asynchrone

using System;
using System.Globalization;
using System.Collections.Generic;
using System.Net.Quic;
using System.Runtime;
using System.Text;
using System.Diagnostics.Metrics;
using Athos64;

namespace Athos64
{
    internal static class Program
    {
        public static async Task Main()
        {
            // Force la console à utiliser UTF-8 SANS BOM (Byte Order Mark)
            Console.OutputEncoding = new UTF8Encoding(false);       // Permet de lire les caractères Unicode dans la console

            // Améliore les performances du ramasse-miettes (GarbageCollector) au prix d'une plus grande utilisation mémoire.
            // Le moteur ne devrait de toute façon pas allouer beaucoup de mémoire lors de la recherche
            // d'une position, car il fait référence à des objets préalloués.
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            // Met le GarbageCollector en mode low latency pour ne pas subir de pauses.
            // Organise le code pour minimiser les allocations(en réutilisant des buffers préalloués).
            // C’est cela qui permet au moteur d’échecs d’être fluide et rapide.

            // EvalParams.ChargerDepuisIni("eval.ini");
            // Test de l'utilisation de NNUE si activé dans les paramètres
            BrunoNNUE.Initialiser(ProtocoleUCI.BigNetworkFile, ProtocoleUCI.SmallNetworkFile);
            // On continue avec l'évaluation classique si NNUE n'est pas utilisé


            await using var fluxEntrée = Console.OpenStandardInput();
            await using var fluxSortie = Console.OpenStandardOutput();
            await using var entréeSortie = new GestionFlux(fluxEntrée, fluxSortie);
            using ProtocoleUCI fluxUCI = new ProtocoleUCI(entréeSortie);
            try
            {
                fluxUCI.Executer();
            }
            catch (Exception exception)
            {   // 1. On informe le protocole UCI (pour le log externe)
                fluxUCI.GérerException(exception);

                // 2. On affiche le détail complet dans la console pour VOUS, le développeur
                Console.Error.WriteLine("\n--- CRASH FATAL DÉTECTÉ ---");
                Console.Error.WriteLine($"Message: {exception.Message}");
                Console.Error.WriteLine($"StackTrace: {exception.StackTrace}");

                // 3. LA LIGNE MAGIQUE : Empêche la console de se fermer
                Console.Error.WriteLine("\nAppuyez sur une touche pour fermer...");
                Console.ReadKey();
            }
        }
    }
    public class GestionFlux : IAsyncDisposable
    {   // Classe pour gérer les flux d'entrée et de sortie du moteur d'échecs
        private readonly StreamReader _lectureFluxUCI;
        private readonly StreamWriter _écritureFluxUCI;
        public bool Debug { get; set; } = false;
        public bool Log { get; set; } = false;
        public GestionFlux(Stream fluxEntrée, Stream fluxSortie)
        {   // Constructeur de la classe GestionFlux
            _lectureFluxUCI = new StreamReader(fluxEntrée, Encoding.UTF8, leaveOpen: true);
            _écritureFluxUCI = new StreamWriter(fluxSortie, new UTF8Encoding(false), leaveOpen: true)
            {
                AutoFlush = true
            };
        }
        public string LireLigne()
        {   // Lit une ligne du flux d'entrée et la retourne sous forme de chaîne de caractères
            return _lectureFluxUCI.ReadLine() ?? string.Empty;      // Retourne une chaîne vide si null est rencontré
        }
        public void EcrireLigne(string texte = "")
        {   // Écriture d'une ligne dans le flux de sortie
            _écritureFluxUCI.WriteLine(texte);
            if (Log)
                Console.Error.WriteLine("[LOG] " + texte);
        }
        public void EcrireDebug(string texte)
        {   // Debug 
            if (Debug)
                Console.Error.WriteLine("[DEBUG] " + texte);
        }
        public async ValueTask DisposeAsync()
        {   // Libération des ressources  
            await _écritureFluxUCI.FlushAsync();
            _lectureFluxUCI.Dispose();
            _écritureFluxUCI.Dispose();
        }
    }
}
