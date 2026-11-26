// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Protocole UCI (partie) : commande "go" (lancement de la recherche), arrêt et ponderhit.
// ├─ "AllerAsynchrone" lit les paramètres de "go", règle le temps, lance la recherche et envoie le bestmove
// ├─ "CalculerTempsPourCeCoup" temps alloué à un coup selon le temps restant, l'incrément et movestogo
// ├─ "EnvoyerMeilleurCoup" envoie la ligne "bestmove" (avec le coup à deviner si la PV en a deux)
// ├─ "Stopper" arrête la recherche en cours
// └─ "Ponderhit" le coup attendu a été joué : la recherche infinie devient limitée dans le temps

using System;
using System.Collections.Generic;
using static Athos64.Echiquier;

namespace Athos64
{
    public sealed partial class ProtocoleUCI
    {
        // Profondeur maximale d'une recherche limitée par le temps (et d'un "go depth N")
        private const int PROFONDEUR_MAX_TEMPS = 64;

        private void AllerAsynchrone(List<string> jetons)
        {   // Traite la commande "go" sur le thread asynchrone
            int profondeur = ProfondeurDeRecherche;
            bool profondeurDonnee = false;      // "go depth N" reçu
            bool limiteDeTemps = false;         // wtime/btime, movetime ou infinite reçu : c'est le temps qui arrête la recherche
            var gestionTemps = new GestionTemps();
            int wtime = -1;
            int btime = -1;
            int winc = 0;
            int binc = 0;
            int movestogo = 0;

            for (int i = 1; i < jetons.Count; i++)
            {   // 1. On parcourt les jetons pour extraire les paramètres de la commande "go"
                string jeton = jetons[i].ToLower();

                if (jeton == "infinite")
                {   // Si le mode "infinite" est demandé, on démarre la recherche infinie
                    gestionTemps.DemarrerInfini();
                    limiteDeTemps = true;
                    continue;
                }
                if (i + 1 >= jetons.Count)  // On s'assure qu'il y a un argument après le jeton actuel
                    continue;
                switch (jeton)
                {
                    case "depth":
                        if (int.TryParse(jetons[++i], out int p))
                        {
                            profondeur = Math.Clamp(p, 1, PROFONDEUR_MAX_TEMPS);
                            profondeurDonnee = true;
                        }
                        break;
                    case "movetime":
                        if (int.TryParse(jetons[++i], out int t))
                        {
                            gestionTemps.Demarrer(t);
                            limiteDeTemps = true;
                        }
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
                int tempsPourCeCoup = CalculerTempsPourCeCoup(tempsRestant, increment, movestogo);

                // Le temps calculé est le temps visé : entre deux itérations, la recherche décide de continuer ou non.
                // La limite maximale (3 fois plus, au plus 30 % du temps restant) n'interrompt qu'une itération trop longue (2026-11-26)
                int tempsMaximal = Math.Min(3 * tempsPourCeCoup, Math.Max(tempsPourCeCoup, tempsRestant * 3 / 10));
                tempsMaximal = Math.Max(20, Math.Min(tempsMaximal, tempsRestant - 100));
                gestionTemps.Demarrer(tempsMaximal);
                gestionTemps.FixerTempsOptimal(tempsPourCeCoup);
                limiteDeTemps = true;
                _fluxUCI.EcrireDebug($"Temps restant={tempsRestant}  incr={increment}  visé={tempsPourCeCoup}  maximum={tempsMaximal}");
            }

            // Avec une limite de temps et sans "go depth", l'option Depth ne bride plus la recherche (2026-11-25) :
            // elle s'arrêtait à 12 demi-coups même s'il restait du temps (parties longues, finales)
            if (limiteDeTemps && !profondeurDonnee)
                profondeur = PROFONDEUR_MAX_TEMPS;

            _fluxUCI.EcrireDebug($"Lancement recherche profondeur: {profondeur}");

            // 3. Recherche avec récupération du tuple, puis affichage du bestmove final (seul endroit autorisé à le faire)
            var resultatRecherche = Recherche.Chercher(_echiquier, profondeur, gestionTemps, (info) => _fluxUCI.EcrireLigne(info));
            EnvoyerMeilleurCoup(resultatRecherche);
            GC.Collect(1, GCCollectionMode.Forced, false);
        }

        // Temps alloué à un coup (en ms) selon le temps restant, l'incrément et le nombre de coups restants (movestogo, 0 si inconnu)
        private static int CalculerTempsPourCeCoup(int tempsRestant, int increment, int movestogo)
        {
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
            return Math.Max(20, tempsPourCeCoup);
        }

        // Envoie la ligne "bestmove" : le 1er coup de la ligne PV, avec le 2e comme coup à deviner (ponder) s'il existe
        private void EnvoyerMeilleurCoup((Mouvement? MeilleurCoup, string LignePV) resultatRecherche)
        {
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
    }
}
