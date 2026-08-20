// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe de gestion du temps.
// ├─ "DemanderArret" demande l'arrêt de la recherche
// ├─ "Demarrer" démarre le chronomètre avec un temps limité
// ├─ "DemarrerInfini" démarre le chronomètre en mode infini
// ├─ "DemarrerPonder" démarre le chronomètre en mode ponder
// ├─ "SignalerPonderhit" signale un ponderhit
// ├─ "ForcerArret" force l'arrêt de la recherche
// └─ "DoitArreter" vérifie si la recherche doit s'arrêter

using System;
using System.Numerics;
using Athos64;

namespace Athos64
{
    public class ArretRechercheException : Exception { }
    public class GestionTemps
    {
        private int _tempsAlloueInitial = 0; // On stocke le temps alloué au départ pour le ponderhit
        public long LimiteFin { get; private set; }
        public bool EstInfini { get; private set; }
        public bool DemandeArret { get; set; } = false;

        public void DemanderArret()
        {
            DemandeArret = true; // Signal envoyé au thread de recherche
        }
        public GestionTemps()
        {   // On met une limite très lointaine pour ne pas s'arrêter par défaut
            LimiteFin = long.MaxValue;
            EstInfini = false;
        }
        public void Demarrer(int ms)
        {
            _tempsAlloueInitial = ms;
            LimiteFin = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ms;
            EstInfini = false;
        }
        public void DemarrerInfini()
        {
            EstInfini = true;
        }
        public void DemarrerPonder(int ms)
        {   // Pendant le ponder, on stocke le temps théorique alloué,
            // mais on force la recherche en mode infini pour l'instant.
            _tempsAlloueInitial = ms;
            EstInfini = true;
        }
        public void SignalerPonderhit()
        {
            if (EstInfini)
            {   // Rebascule en recherche normale limitée par le temps
                EstInfini = false;
                // On applique le temps de calcul à partir de maintenant (moment du ponderhit)
                LimiteFin = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _tempsAlloueInitial;
            }
        }
        public void ForcerArret()
        {
            throw new ArretRechercheException();
        }
        public bool DoitArreter()
        {   // 1. Si on est en mode infini, on ignore la limite de temps
            if (!EstInfini && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= LimiteFin)
                throw new ArretRechercheException();
            // 2. Soit on a reçu une commande "stop" (le drapeau)
            if (DemandeArret)
                throw new ArretRechercheException();

            return false;
        }
    }
}