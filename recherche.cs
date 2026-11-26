// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

// Classe de recherche des meilleurs mouvements.
// ├─ Structure "Mouvement" représente un mouvement sur l'échiquier avec toutes les informations nécessaires.
// ├─ "InitierPool" initialise un pool de listes de mouvements pour éviter les allocations répétées.
// ├─ "Chercher" recherche du meilleur mouvement à partir d'une position donnée, avec gestion du temps et affichage des informations UCI.
// ├─ "Negamax" implémente l'algorithme de recherche principal avec Negamax, Alpha-Beta, Null Move Pruning, LMR, etc.
// ├─ "FiltrerScoreTT_Entree" filtre le score de la table de transposition pour les entrées de Mat.
// ├─ "FiltrerScoreTT_Sortie" filtre le score de la table de transposition pour les sorties de Mat.
// ├─ "RechercheQuiescence" implémente la recherche de quiescence pour évaluer les positions calmes.
// ├─ "ChoisirMeilleurMouvement" choisit le meilleur mouvement parmi une liste de mouvements en fonction de leur score de tri.
// ├─ "ObtenirPremierMouvement" retourne le premier mouvement de la ligne PV à partir de l'échiquier actuel.
// ├─ "ObtenirLignePV" retourne la ligne de variation principale (PV) à partir de l'échiquier actuel et de la profondeur donnée.
// ├─ "AppliquerScalingStockfish" applique un scaling du score pour le rendre compatible avec l'évaluation de Stockfish.
// ├─ "CalculerScoreMouvement" calcule le score d'un mouvement pour le tri, en tenant compte des captures, promotions, coups TT, coups Killer et historique.
// └─ "ObtenirValeurPiece" retourne la valeur d'une pièce pour l'évaluation statique 

using Athos64;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Collections.Concurrent;
using static Athos64.Echiquier;
using static Athos64.TableTransposition;
using static Athos64.ProtocoleUCI;
using static Athos64.GestionTemps;

namespace Athos64
{
    public sealed class ContexteRecherche
    {   // Contexte de recherche pour stocker les informations spécifiques à chaque recherche
        public long NombreDeNoeuds { get; set; } = 0;
        public long NoeudsQuiescence { get; set; } = 0;
        public IntPtr Nnue { get; set; } = IntPtr.Zero;     // Contexte NNUE natif (évaluation incrémentale) de ce thread ; IntPtr.Zero = aucun
        public int ProfondeurMaxAtteinte { get; set; } = 0;
        // Profondeur de référence des limites d'extension et de quiescence : la profondeur demandée au "go", au plus 20
        // (fixée par Chercher ; 20 est la valeur validée en match, une recherche au temps demande jusqu'à 64)
        public int ProfondeurReglee { get; set; } = ProtocoleUCI.ProfondeurDeRecherche;

        // Rendre les stats TT locales au contexte
        public long InterrogationsTT { get; set; } = 0;
        public long CoupuresTT { get; set; } = 0;
        public long UtilisationsCoupTT { get; set; } = 0;
        public long CoupuresTTQuiescence { get; set; } = 0;
        public long UtilisationsTTQuiescence { get; set; } = 0;
        public int[] LongueurPV { get; } = new int[128];
        public Mouvement[,] TablePV { get; } = new Mouvement[128, 128];
        public Mouvement[,] KillerMoves { get; } = new Mouvement[128, 2];    // 2 "Killer Moves" par niv. de distance à la racine (max 128 plies)
        public int[,] TableHistorique { get; } = new int[128, 128];   // Tableau contenant l'historique des coups [CaseDepart, CaseArrivee]
        public List<Mouvement>[] PoolListes { get; } = InitierPool();

        private static List<Mouvement>[] InitierPool()
        {
            var pool = new List<Mouvement>[256];
            for (int i = 0; i < 256; i++)
            {
                pool[i] = new List<Mouvement>(128);
            }
            return pool;
        }
    }
    public static class Recherche
    {   // Classe statique pour la recherche des meilleurs mouvements

        private const int INFINI = 1000000;
        // On définit une limite pour savoir si c'est un score de MAT
        private const int SCORE_MAT = 30000;
        internal static Mouvement MeilleurMouvementTrouve;
        internal static GestionTemps? GestionTempsEnCours;
        internal static int limiteProfondeurQuiescence = 16; // Limite de profondeur pour la Quiescence (en termes de distance à la racine)

        // Optimism NNUE, comme Stockfish : une valeur par couleur.
        public static int OptimismBlanc { get; private set; }
        public static int OptimismNoir { get; private set; }

        // La table doit être accessible par la recherche (à initialiser avant Chercher)
        private const int TAILLE_TABLE_MO = 128;    // Taille de la table en Mo
        internal static readonly TableTransposition TT = new(TAILLE_TABLE_MO);   // 128 Mo pour la table de transposition

        // Matrice de réduction LMR précalculée [Profondeur, IndexCoup]
        private static readonly int[,] TableLMR = InitialiserTableLMR();

        // Référence vers le contexte de la dernière recherche effectuée
        public static ContexteRecherche? DernierContexte { get; private set; }

        // Propriété relais pour lire le nombre de nœuds sans casser l'accès depuis GestionUCI
        public static long NombreDeNoeuds => DernierContexte?.NombreDeNoeuds ?? 0;


        public static (Mouvement? MeilleurCoup, string LignePV) Chercher(Echiquier echiquier, int profondeurMax, 
                                                                            GestionTemps gestionTemps, Action<string> envoyerInfo)
        {
            Mouvement? meilleurMouvementGlobal = null;
            string lignePVGlobal = "";
            string lignePV = "";

            // 1. La TT n'est plus vidée à chaque coup (2026-11-25) : la position après le coup adverse a déjà été cherchée
            //    au coup précédent. Elle n'est vidée qu'à "ucinewgame" (NouvellePartieUci).
            MeilleurMouvementTrouve = default;  // Repli de ValiderPV : ne doit jamais venir d'une position précédente
            // 2. Initialisation de la gestion du temps juste avant d'attaquer la recherche
            Recherche.GestionTempsEnCours = gestionTemps;
            gestionTemps.DemandeArret = false;

            // Variété pour le 1er coup : un coup d'ouverture choisi au hasard parmi les plus populaires
            Mouvement? coupForcePremierTour = ChoisirCoupVarie(echiquier);
            // --- Si un coup est forcé, on court-circuite immédiatement ---
            if (coupForcePremierTour.HasValue) 
            {
                meilleurMouvementGlobal = coupForcePremierTour.Value;
                lignePV = ProtocoleUCI.ConvertirMouvementEnUCI(coupForcePremierTour.Value);
                // On informe l'interface immédiatement à la profondeur 1
                envoyerInfo?.Invoke($"info depth 1 seldepth 1 score cp 25 nodes 0 pv {lignePV}");
                // On quitte la fonction, la TT reste parfaitement vierge et intacte !
                return (meilleurMouvementGlobal, lignePV);
            }
            // ======================================================================
            //  Fin de mise en place de variété pour le 1er coup
            // ======================================================================

            // --- INITIALISATION DU CONTEXTE PRINCIPAL ---
            int profondeurReglee = Math.Min(profondeurMax, 20);
            var ctxPrincipal = new ContexteRecherche { ProfondeurReglee = profondeurReglee };
            DernierContexte = ctxPrincipal; // Permet à GestionUCI de lire les statistiques
            ctxPrincipal.Nnue = BrunoNNUE.CreerContexte();
            if (!BrunoNNUE.DefinirPosition(ctxPrincipal.Nnue, echiquier))
            {   // Pas de contexte utilisable : évaluation complète à chaque nœud
                BrunoNNUE.DetruireContexte(ctxPrincipal.Nnue);
                ctxPrincipal.Nnue = IntPtr.Zero;
            }

            // --- INITIALISATION DE LA FILE DE TRAVAIL PARTAGÉE ---
            // On remplit la file avec les profondeurs à traiter pour les threads aides (ex: de p=2 jusqu'à profondeurMax + 2)
            var fileDeProfondeurs = new System.Collections.Concurrent.ConcurrentQueue<int>();
            for (int p = 2; p <= profondeurMax + 2; p++)
            {
                fileDeProfondeurs.Enqueue(p);
            }

            // Threads aides (Lazy SMP) : chacun a son échiquier, son contexte de recherche et son contexte NNUE
            var contextesAides = new System.Collections.Concurrent.ConcurrentBag<ContexteRecherche>();
            // Les erreurs des threads aides sont notées ici puis envoyées par le thread principal (un seul thread écrit sur la sortie UCI)
            var erreursAides = new System.Collections.Concurrent.ConcurrentQueue<string>();
            Task[] tachesAides = LancerThreadsAides(echiquier, fileDeProfondeurs, contextesAides, gestionTemps, erreursAides, profondeurReglee);
            
            // --- 3. THREAD PRINCIPAL (Thread 0) ---
            DateTime heureDebut = DateTime.Now; // Nécessaire pour le calcul des Noeuds Par Seconde (NPS)
            // ======================================================================
            // ---      Approfondissement itératif de la profondeur 1 à p        ----
            // ======================================================================
            try
            {
                int score = 0;

                // Moyenne récursive des scores des recherches successives,
                // selon le principe de Stockfish RootMove::averageScore.
                int averageScore = 0;
                bool averageScoreInitialise = false;

                // Stabilité du meilleur coup d'une itération à l'autre (gestion du temps)
                Mouvement? coupIterationPrecedente = null;
                int iterationsStables = 0;

                for (int p = 1; p <= profondeurMax; p++)
                {
                    ctxPrincipal.NoeudsQuiescence = 0;
                    ctxPrincipal.ProfondeurMaxAtteinte = 0;

                    // Recherche complète puis, à partir de la profondeur 5, recherche avec fenêtre d'aspiration
                    score = RechercherAProfondeur(echiquier, p, ctxPrincipal, ref meilleurMouvementGlobal, ref lignePVGlobal, score);

                    // ------------------------------------------------------------------
                    // Mise à jour de averageScore comme dans Stockfish RootMove
                    // ------------------------------------------------------------------
                    if (!averageScoreInitialise)
                    {
                        averageScore = score;
                        averageScoreInitialise = true;
                    }
                    else
                    {
                        averageScore = (score + averageScore) / 2;
                    }
                    MettreAJourOptimism(echiquier, averageScore);   // Optimism selon Stockfish 19

                    // --- Affichage de la ligne d'informations uci info depth ... ---
                    envoyerInfo?.Invoke(ConstruireLigneInfo(echiquier, p, score, lignePVGlobal, ctxPrincipal, contextesAides, heureDebut));

                    // --- Gestion du temps entre deux itérations (2026-11-26) ---
                    // Meilleur coup stable : on s'arrête plus tôt ; il vient de changer : on prend plus de temps
                    if (meilleurMouvementGlobal.HasValue)
                    {
                        bool memeCoup = coupIterationPrecedente.HasValue && MemesCases(meilleurMouvementGlobal.Value, coupIterationPrecedente.Value);
                        iterationsStables = memeCoup ? iterationsStables + 1 : 0;
                        coupIterationPrecedente = meilleurMouvementGlobal;
                    }
                    double fraction = iterationsStables >= 3 ? 0.4 : iterationsStables >= 1 ? 0.6 : 0.9;
                    if (p >= 4 && gestionTemps.NePasCommencerIteration(fraction))
                        break;
                }
            }

            catch (ArretRechercheException)
            {   // Interruption immédiate par le temps : 
                // On ne fait rien, on va simplement retourner les dernières valeurs valides
            }

            finally
            {   // 1. Demande l'arrêt immédiat de tous les threads
                if (gestionTemps != null)
                {
                    gestionTemps.DemandeArret = true;
                }
                // 2. Attend IMPÉRATIVEMENT que tous les threads aides soient arrêtés
                Task.WaitAll(tachesAides);
                // Signale à l'interface les erreurs survenues dans les threads aides (la recherche a continué sans eux)
                while (erreursAides.TryDequeue(out string? erreurAide))
                    envoyerInfo?.Invoke(erreurAide);
                // 3. Libère le contexte NNUE natif du thread principal
                BrunoNNUE.DetruireContexte(ctxPrincipal.Nnue);
                ctxPrincipal.Nnue = IntPtr.Zero;
            }

            if (meilleurMouvementGlobal.HasValue)
            {   // Met à jour la variable statique uniquement à la fin par le thread principal
                MeilleurMouvementTrouve = meilleurMouvementGlobal.Value;
            }

            return (meilleurMouvementGlobal, lignePVGlobal);
        }
        // Variété pour la première partie : depuis la position initiale (blancs) ou après 1.e4, d4, c4, Cf3 (noirs),
        // retourne un coup d'ouverture populaire choisi au hasard ; null sinon.
        private static Mouvement? ChoisirCoupVarie(Echiquier echiquier)
        {
            Mouvement? coupForcePremierTour = null;
            if (echiquier.CoteBlanc)
            {       // Variété côté blanc
                if (echiquier.EstPositionInitiale())
                {   // Si on est au tout début de la partie, on force un coup d'ouverture parmi les plus populaires.
                    List<Mouvement> coupsLegaux = [];
                    echiquier.GenererMouvementsLegauxCommun(coupsLegaux);
                    List<string> choixSouhaites = ["e2e4", "d2d4", "c2c4", "g1f3"];
                    List<Mouvement> optionsValides = [.. coupsLegaux.Where(m => choixSouhaites.Contains(ProtocoleUCI.ConvertirMouvementEnUCI(m)))];
                    if (optionsValides.Count == 0) optionsValides = coupsLegaux;
                    if (optionsValides.Count > 0)
                    {   // Si on a trouvé des coups valides, on en choisit un au hasard
                        Random rand = new();
                        coupForcePremierTour = optionsValides[rand.Next(optionsValides.Count)];
                    }
                }
            }
            else if (!echiquier.CoteBlanc)
            {       // Variété côté noir après le premier coup blanc
                List<Mouvement> coupsLegauxNoirs = [];
                echiquier.GenererMouvementsLegauxCommun(coupsLegauxNoirs);

                List<Mouvement> optionsPropres = [];
                if (echiquier.EstApresE4())
                {   // Après 1.e4
                    optionsPropres = [.. coupsLegauxNoirs.Where(m =>
                        (m.CaseDepart == 50 && m.CaseArrivee == 34) ||  // c7c5
                        (m.CaseDepart == 52 && m.CaseArrivee == 44) ||  // e7e6
                        (m.CaseDepart == 52 && m.CaseArrivee == 36))];  // e7e5
                }
                else if (echiquier.EstApresD4() || echiquier.EstApresC4() || echiquier.EstApresCf3())
                {   // Après 1.d4, c4 ou Cf3
                    optionsPropres = [.. coupsLegauxNoirs.Where(m =>
                        (m.CaseDepart == 52 && m.CaseArrivee == 44) ||  // e7e6
                        (m.CaseDepart == 51 && m.CaseArrivee == 35) ||  // d7d5
                        (m.CaseDepart == 62 && m.CaseArrivee == 45))];  // g8f6
                }
                if (optionsPropres.Count > 0)
                {   // Si on a trouvé des coups valides, on en choisit un au hasard
                    Random rand = new();
                    coupForcePremierTour = optionsPropres[rand.Next(optionsPropres.Count)];
                }
            }
            return coupForcePremierTour;
        }

        // Lance les threads aides (Lazy SMP divergent). Chacun cherche, sur sa copie de l'échiquier, les profondeurs
        // qu'il prend dans la file partagée. Il s'arrête quand gestionTemps.DemandeArret passe à true.
        private static Task[] LancerThreadsAides(Echiquier echiquier, System.Collections.Concurrent.ConcurrentQueue<int> fileDeProfondeurs,
                                                 System.Collections.Concurrent.ConcurrentBag<ContexteRecherche> contextesAides,
                                                 GestionTemps gestionTemps,
                                                 System.Collections.Concurrent.ConcurrentQueue<string> erreursAides,
                                                 int profondeurReglee)
        {
            Task[] tachesAides = new Task[ProtocoleUCI.NombreThreads - 1];
            for (int t = 1; t < NombreThreads; t++)
            {
                // Copie faite ici, par le thread principal, avant son premier coup : si le thread d'aide démarrait tard
                // (machine chargée), il copierait un échiquier que le thread principal est en train de modifier.
                var echiquierAide = echiquier.Cloner();
                tachesAides[t - 1] = Task.Run(() =>
                {
                    var ctxAide = new ContexteRecherche { ProfondeurReglee = profondeurReglee };
                    contextesAides.Add(ctxAide);
                    ctxAide.Nnue = BrunoNNUE.CreerContexte();
                    if (!BrunoNNUE.DefinirPosition(ctxAide.Nnue, echiquierAide))
                    {
                        BrunoNNUE.DetruireContexte(ctxAide.Nnue);
                        ctxAide.Nnue = IntPtr.Zero;
                    }

                    try
                    {
                        // Les threads aides piochent dynamiquement chaque profondeur non attribuée
                        while (fileDeProfondeurs.TryDequeue(out int p))
                        {
                            if (gestionTemps.DemandeArret) break;

                            // Recherche avec fenêtre ouverte
                            Negamax(echiquierAide, p, 0, -INFINI, INFINI, ctxAide);
                        }
                    }
                    catch (ArretRechercheException) { /* Arrêt propre */ }
                    catch (Exception exception)
                    {   // Une erreur dans un thread d'aide ne doit pas arrêter la recherche principale : on la note
                        // (sur une seule ligne) pour que le thread principal l'envoie en "info string"
                        string message = $"{exception.GetType().Name} : {exception.Message}".Replace('\r', ' ').Replace('\n', ' ');
                        erreursAides.Enqueue($"info string Erreur dans un thread d'aide : {message}");
                    }
                    finally
                    {
                        BrunoNNUE.DetruireContexte(ctxAide.Nnue);
                        ctxAide.Nnue = IntPtr.Zero;
                    }
                });
            }
            return tachesAides;
        }

        // Recherche à la profondeur p à la racine : complète jusqu'à la profondeur 4, puis avec fenêtre d'aspiration.
        // Retourne le score ; le meilleur coup et la ligne PV sont mis à jour par ValiderPV.
        private static int RechercherAProfondeur(Echiquier echiquier, int p, ContexteRecherche ctxPrincipal,
                                                 ref Mouvement? meilleurMouvementGlobal, ref string lignePVGlobal, int scorePrecedent)
        {
            int score;
            if (p <= 4 || Math.Abs(scorePrecedent) >= SCORE_MAT - 100)
            {   // Recherche complète : premières profondeurs, ou score de mat (la fenêtre n'aurait pas de sens)
                score = Negamax(echiquier, p, 0, -INFINI, INFINI, ctxPrincipal);
                ValiderPV(echiquier, p, ctxPrincipal, ref meilleurMouvementGlobal, ref lignePVGlobal);
            }
            else
            {   // ---------------- Fenêtre d'aspiration ----------------
                // Centrée sur le score de l'itération précédente (2026-11-25 : avant, une recherche complète à la même
                // profondeur précédait la fenêtre, qui n'économisait donc rien)
                score = scorePrecedent;

                const int MargeInitiale = 50;
                int marge = MargeInitiale;

                int alphaFenetre = Math.Max(score - marge, -INFINI);
                int betaFenetre = Math.Min(score + marge, INFINI);

                while (true)    // Se termine toujours : au-delà de 1600 cp de marge, la fenêtre est entièrement ouverte
                {
                    int scoreFenetre = Negamax(echiquier, p, 0, alphaFenetre, betaFenetre, ctxPrincipal);

                    // MAT : on refait immédiatement une recherche complète pour obtenir une PV complète et fiable.
                    if (Math.Abs(scoreFenetre) >= SCORE_MAT - 100)
                    {
                        score = Negamax(echiquier, p, 0, -INFINI, INFINI, ctxPrincipal);
                        ValiderPV(echiquier, p, ctxPrincipal, ref meilleurMouvementGlobal, ref lignePVGlobal);
                        break;
                    }

                    if (scoreFenetre > alphaFenetre && scoreFenetre < betaFenetre)
                    {   // Score dans la fenêtre
                        score = scoreFenetre;
                        ValiderPV(echiquier, p, ctxPrincipal, ref meilleurMouvementGlobal, ref lignePVGlobal);
                        break;
                    }

                    // FAIL-LOW / FAIL-HIGH
                    score = scoreFenetre;
                    marge *= 2;

                    if (scoreFenetre <= alphaFenetre)
                    {   // FAIL-LOW
                        alphaFenetre = Math.Max(scoreFenetre - marge, -INFINI);
                    }
                    else
                    {   // FAIL-HIGH
                        betaFenetre = Math.Min(scoreFenetre + marge, INFINI);
                    }
                    if (marge > 1600)
                    {
                        alphaFenetre = -INFINI;
                        betaFenetre = INFINI;
                    }

                    if (alphaFenetre == -INFINI && betaFenetre == INFINI)
                    {   // Fenêtre complètement ouverte
                        score = Negamax(echiquier, p, 0, -INFINI, INFINI, ctxPrincipal);
                        ValiderPV(echiquier, p, ctxPrincipal, ref meilleurMouvementGlobal, ref lignePVGlobal);
                        break;
                    }
                }
                // -------------Fin Fenêtre d'aspiration ----------------
            }
            return score;
        }

        // Validation et reconstruction de la PV après chaque recherche : récupère le meilleur coup trouvé
        // et complète la PV si nécessaire à partir de la table de transposition.
        private static void ValiderPV(Echiquier echiquier, int p, ContexteRecherche ctxPrincipal,
                                      ref Mouvement? meilleurMouvementGlobal, ref string lignePVGlobal)
        {
            Mouvement premierCoup = ctxPrincipal.TablePV[0, 0];
            if (premierCoup.CaseDepart == premierCoup.CaseArrivee)
                premierCoup = MeilleurMouvementTrouve;
            if (premierCoup.CaseDepart == premierCoup.CaseArrivee)
                return;

            meilleurMouvementGlobal = premierCoup;

            if (ctxPrincipal.LongueurPV[0] > 1)
            {   // Si la PV existe déjà, on ne touche à rien
                lignePVGlobal = ObtenirLignePV(echiquier, p, ctxPrincipal);
                return;
            }
            // PV réduite à un seul coup : on essaie de la prolonger avec le TT
            ctxPrincipal.TablePV[0, 0] = premierCoup;
            ctxPrincipal.LongueurPV[0] = 1;

            // Copie isolée pour ne pas altérer l'échiquier principal
            // ni subir de conflits pendant que les threads assistants travaillent
            var echiquierPV = echiquier.Cloner();
            Mouvement coup = premierCoup;

            for (int i = 1; i < p; i++)
            {
                echiquierPV.JouerCoup(coup.CaseDepart, coup.CaseArrivee, coup.Promotion);
                EntreeTT? entree = TT.Recuperer(echiquierPV.CleActuelle);

                if (!entree.HasValue)
                    break;
                if (entree.Value.CaseDepartMeilleur == entree.Value.CaseArriveeMeilleur)
                    break;

                var mouvements = ctxPrincipal.PoolListes[i];
                mouvements.Clear();
                echiquierPV.GenererMouvementsLegauxCommun(mouvements);

                bool trouve = false;
                foreach (var m in mouvements)
                {
                    if (m.CaseDepart == entree.Value.CaseDepartMeilleur &&
                        m.CaseArrivee == entree.Value.CaseArriveeMeilleur &&
                        m.Promotion == entree.Value.PromotionMeilleur)
                    {
                        coup = m;
                        ctxPrincipal.TablePV[0, i] = m;
                        ctxPrincipal.LongueurPV[0] = i + 1;
                        trouve = true;
                        break;
                    }
                }
                mouvements.Clear();

                if (!trouve)
                    break;
            }
            lignePVGlobal = ObtenirLignePV(echiquier, p, ctxPrincipal);
        }

        // Optimism de Stockfish 19 à partir de la moyenne des scores : Stockfish utilise optimism[WHITE] et optimism[BLACK]
        private static void MettreAJourOptimism(Echiquier echiquier, int averageScore)
        {
            int optimism = 114 * averageScore /
                           (Math.Abs(averageScore) + 85);
            if (echiquier.CoteBlanc)
            {
                OptimismBlanc = optimism;
                OptimismNoir = -optimism;
            }
            else
            {
                OptimismNoir = optimism;
                OptimismBlanc = -optimism;
            }
        }

        // Ligne "info depth ..." envoyée à l'interface après chaque profondeur
        private static string ConstruireLigneInfo(Echiquier echiquier, int p, int score, string lignePVGlobal, ContexteRecherche ctxPrincipal,
                                                  System.Collections.Concurrent.ConcurrentBag<ContexteRecherche> contextesAides, DateTime heureDebut)
        {
            TimeSpan duree = DateTime.Now - heureDebut;
            double secondes = duree.TotalSeconds;

            // En multi-thread (Lazy SMP), le total de nœuds est la somme du thread principal + des threads aides
            long noeudsTotaux = ctxPrincipal.NombreDeNoeuds;
            foreach (var ctxAide in contextesAides)
            {
                noeudsTotaux += ctxAide.NombreDeNoeuds;
            }

            // NPS : Basé sur le cumul de tous les nœuds
            long nps = secondes > 0.001 ? (long)(noeudsTotaux / secondes) : 0;
            long tempsMs = (long)duree.TotalMilliseconds;

            // Nœuds propres au Negamax (Recherche principale)
            double hitRate = ctxPrincipal.InterrogationsTT > 0 ? (double)ctxPrincipal.CoupuresTT / ctxPrincipal.InterrogationsTT * 100 : 0;
            double qHitRate = ctxPrincipal.NoeudsQuiescence > 0 ? (double)ctxPrincipal.CoupuresTTQuiescence / ctxPrincipal.NoeudsQuiescence * 100 : 0;

            string scoreString;
            // Seuil pour détecter un score de mat (basé dynamiquement sur INFINI)
            int limiteMat = INFINI - 1000;

            if (Math.Abs(score) > limiteMat)
            {   // Calcul de la distance du mat en nombre de COUPS (1 coup = 2 demi-coups/plies)
                // On ajoute 1 pour arrondir correctement la division par 2
                // On extrait dynamiquement le nombre de plies depuis la constante INFINI
                int distancePlies = INFINI - Math.Abs(score);
                int distanceCoups = (distancePlies + 1) / 2;

                // Sécurité pour empêcher l'affichage technique d'un "mate 0" à la racine
                if (distanceCoups == 0) distanceCoups = 1;

                int signe = (score > 0) ? 1 : -1;
                scoreString = $"mate {signe * distanceCoups}";
            }
            else
            {
                int scoreFinal = AppliquerScalingStockfish(echiquier, score);
                scoreString = $"cp {scoreFinal}";
            }

            return $"info depth {p} seldepth {ctxPrincipal.ProfondeurMaxAtteinte} score {scoreString} " +
                   $"nodes {noeudsTotaux} nps {nps} time {tempsMs} " +
                   $"tthit {hitRate:F1}% qhit {qHitRate:F1}% pv {lignePVGlobal}";
        }

        public static int Negamax(Echiquier e, int profondeur, int distanceRacine, int alpha, int beta, ContexteRecherche? ctx = null)
        {   // Algorithme de recherche principal avec Negamax, Alpha-Beta, Null Move Pruning, LMR, etc.
            
            ctx ??= new ContexteRecherche();    // Si aucun contexte n'est fourni, on en crée un
            ctx.NombreDeNoeuds++;
            // Vérification périodique de l'arrêt demandé (tous les 64 nœuds : l'évaluation NNUE est lente,
            // 2048 nœuds pouvaient durer plus de 100 ms)
            VerifierArretDemande(ctx);
            // Garde-fou pour éviter tout dépassement de tableau en profondeur extrême
            if (distanceRacine >= 127)
            {
                return BrunoNNUE.EvaluerNNUE(e, ctx.Nnue);
            }
            int alphaOrigine = alpha;
            int betaOrigine = beta;
            ctx.LongueurPV[distanceRacine] = distanceRacine;

            // ======================================================================
            // --- Détection de la triple répétition et de la règle des 50 coups ----
            // ======================================================================

            // Règle des 50 coups et triple répétition
            if (PartieNulle(e, distanceRacine))
                return 0;

            // Sécurité temporelle (pour go movetime xxx) : lève ArretRechercheException si le temps est écoulé
            VerifierTemps(ctx);

            // ====================================================
            // --- 1. Consultation de la table de transposition ---
            // ====================================================
            if (SonderTable(ctx, e, profondeur, distanceRacine, ref alpha, ref beta, out Echiquier.Mouvement coupTT, out int scoreTT))
                return scoreTT;

            if (profondeur <= 0) return RechercheQuiescence(ctx, e, alpha, beta, distanceRacine);

            // Évaluation statique de la position actuelle pour les élagages sélectifs (inutile en échec : aucun élagage ne s'y applique)
            bool enEchec = e.RoiEnEchec(e.CoteBlanc);
            bool noeudPV = betaOrigine - alphaOrigine > 1;
            int evalStatique = enEchec ? -INFINI : BrunoNNUE.EvaluerNNUE(e, ctx.Nnue);

            // ==================================================================
            // --- 2. Reverse Futility Pruning (élagage par l'éval statique) ---
            // ==================================================================
            // Près des feuilles, hors variante principale : si l'éval dépasse beta d'une marge par demi-coup restant,
            // un coup calme suffira presque sûrement à garder l'avantage : on coupe sans chercher (2026-11-25)
            if (!noeudPV && !enEchec && profondeur <= 6 && Math.Abs(beta) < 20000
                && evalStatique - 80 * profondeur >= beta)
                return evalStatique;

            // =======================================================
            // ---  3. Null Move Pruning (Élagage par le coup nul) ---
            // =======================================================
            // Seulement si l'éval statique est déjà au-dessus de beta : sinon passer son tour ne coupera presque jamais (2026-11-25)
            if (!enEchec && evalStatique >= beta && CoupNulDonneCoupure(ctx, e, profondeur, distanceRacine, beta))
                return beta;
            // ---------------------------------------------------

            // ✅ Utilisation du Pool au lieu de l'allocation d'une nouvelle liste
            var listeMouvements = ctx.PoolListes[distanceRacine];
            e.GenererMouvementsLegauxCommun(listeMouvements);

            if (listeMouvements.Count == 0)
            {
                if (e.RoiEnEchec(e.CoteBlanc)) return -INFINI + distanceRacine;
                return 0;
            }

            // ========================================================================
            // --- 3. Calcul des scores de tri pour l'ordonnancement des mouvements ---    
            // ========================================================================
            CalculerScoresTri(ctx, e, listeMouvements, coupTT, distanceRacine);

            Mouvement meilleurMouvementLocal = default;
            int meilleurScoreLocal = -INFINI;

            // ==========================================================
            // --- 4. Boucle de recherche avec choix du meilleur coup ---
            // ==========================================================
            bool subitEchec = enEchec;

            for (int i = 0; i < listeMouvements.Count; i++)
            {   // On amène le meilleur coup parmi ceux restants à l'index i
                ChoisirMeilleurMouvement(listeMouvements, i);
                var mouvement = listeMouvements[i];
                // Futility Pruning : on ignore un coup calme près des feuilles si la position est déjà trop mauvaise
                if (ElagageFutilite(mouvement, i, profondeur, alpha, evalStatique, subitEchec))
                    continue;

                int score = JouerEtChercher(ctx, e, mouvement, i, profondeur, distanceRacine, alpha, beta, subitEchec);

                if (score > meilleurScoreLocal)
                {
                    meilleurScoreLocal = score;
                    meilleurMouvementLocal = mouvement; // <-- IL COMMENCE ICI (On stocke le "moins pire" ou le meilleur coup absolu)
                }
                if (score > alpha)
                {
                    alpha = score;
                    // if (distanceRacine == 0) MeilleurMouvementTrouve = mouvement;

                    if (distanceRacine < 127)
                        MettreAJourPV(ctx, mouvement, distanceRacine);
                }
                // C'est ici que la coupure se produit !
                if (alpha >= beta)
                {   // C'est ce coup qui provoque la coupure
                    meilleurMouvementLocal = mouvement;
                    EnregistrerCoupureCalme(ctx, mouvement, profondeur, distanceRacine);
                    break;
                }
            }

            // =======================================================
            // --- 5. Stockage dans la table de transposition      ---
            // =======================================================
            StockerResultatTT(e, profondeur, distanceRacine, alphaOrigine, betaOrigine, meilleurScoreLocal, meilleurMouvementLocal);
            // SÉCURITÉ : On nettoie la liste avant de quitter le nœud pour que le pool reste propre
            listeMouvements.Clear();
            return meilleurScoreLocal;
        }
        // Score de tri de chaque coup (coup de la table de transposition, captures, tueurs, historique...)
        private static void CalculerScoresTri(ContexteRecherche ctx, Echiquier e, List<Echiquier.Mouvement> listeMouvements,
                                              Echiquier.Mouvement coupTT, int distanceRacine)
        {
            for (int i = 0; i < listeMouvements.Count; i++)
            {
                var m = listeMouvements[i];
                m.ScoreTri = CalculerScoreMouvement(ctx, m, e, coupTT, distanceRacine);
                listeMouvements[i] = m;
            }
        }

        // Futility Pruning sécurisé : retourne true si le coup calme peut être ignoré
        private static bool ElagageFutilite(Echiquier.Mouvement mouvement, int numeroCoup, int profondeur, int alpha,
                                            int evalStatique, bool subitEchec)
        {
            bool estCoupCalme = !mouvement.PieceCapturee.HasValue && !mouvement.Promotion.HasValue;

            // 1. Limité strictement aux feuilles proches (profondeur 1 et 2)
            // 2. On n'élague jamais si le Roi est en échec ou si on est proche d'un score de mat
            // 3. On réserve l'élagage aux coups calmes à partir du 2ᵉ coup
            if (!subitEchec
                && estCoupCalme
                && profondeur <= 2
                && numeroCoup > 0
                && Math.Abs(alpha) < 20000)
            {
                // Marge de sécurité suffisante (~200 cp à d1, ~400 cp à d2)
                int margeFP = 200 * profondeur;
                return evalStatique + margeFP <= alpha;
            }
            return false;
        }

        // Joue le coup (échiquier et contexte NNUE), le cherche à la profondeur voulue, puis l'annule. Retourne son score.
        // numeroCoup est sa position dans la liste triée : le premier est cherché en pleine fenêtre,
        // les suivants en fenêtre nulle (avec réduction LMR pour les coups calmes tardifs).
        private static int JouerEtChercher(ContexteRecherche ctx, Echiquier e, Echiquier.Mouvement mouvement, int numeroCoup,
                                           int profondeur, int distanceRacine, int alpha, int beta, bool subitEchec)
        {
            bool nnueJoue = BrunoNNUE.JouerCoup(ctx.Nnue, mouvement.CaseDepart, mouvement.CaseArrivee, mouvement.Promotion);
            var etatPrecedent = e.JouerCoup(mouvement.CaseDepart, mouvement.CaseArrivee, mouvement.Promotion);

            // Une fois le coup joué, le trait a changé : on vérifie si le Roi du camp adverse
            // (celui qui vient de jouer) met l'autre en échec.
            bool donneEchec = e.RoiEnEchec(e.CoteBlanc);

            // Extension sur échec, sauf si on a déjà trop creusé par rapport à la profondeur demandée
            int extension = (donneEchec && distanceRacine < ctx.ProfondeurReglee * 2) ? 1 : 0;
            int score;

            // LMR (Late Move Reduction) pour les coups moins prometteurs.
            // On ne réduit PAS si :
            // 1. On est trop près des feuilles (profondeur < 3)    2. Le coup est parmi les premiers de la liste (numeroCoup < 4)
            // 3. C'est une capture, une promotion, un coup tueur ou un coup du Roi    4. Le Roi est en échec ou le coup donne échec
            /*
            CONDITION LMR : On ne réduit que les coups CALMES (pas de captures, promotions, échecs, ou roi en danger)
            Au départ, mon LMR réduisait trop de coups, trop tôt et trop fortement.
            Concrètement :      • il s'appliquait dès le 3ᵉ coup (i > 2) ;
                                • à profondeur 8, il réduisait déjà de 3 demi-coups, ce qui est très agressif.
            Résultat : certaines variantes intéressantes étaient explorées de façon trop superficielle.
            À profondeur 8, le moteur passait à côté du meilleur coup et en choisissait même un mauvais.
            J'ai donc rendu le LMR plus prudent :
                • il ne s'applique qu'à partir du 6ᵉ coup (i >= 6) ;
                • la réduction est limitée à 1 ou 2 demi-coups à profondeur 8.
            Le moteur explore donc davantage les coups prometteurs, retrouve les bons coups... mais en contrepartie il visite plus de nœuds et devient plus lent. C'est le compromis classique du réglage du LMR.
            (Réglage actuel : à partir du 5ᵉ coup, numeroCoup >= 4 ; voir la condition ci-dessous.)
            */
            // Coup tueur reconnu par ses cases, comme pour le tri (2026-11-25) : Equals comparait aussi ScoreTri,
            // et le tueur garde le score de tri qu'il avait lors de sa coupure : il n'était reconnu que par hasard
            bool estKiller = (distanceRacine < 64) &&
                             (MemesCases(mouvement, ctx.KillerMoves[distanceRacine, 0]) || MemesCases(mouvement, ctx.KillerMoves[distanceRacine, 1]));

            if (profondeur >= 3
                && numeroCoup >= 4       // Laisser les 4 premiers coups (0, 1, 2, 3) à pleine profondeur
                && !mouvement.PieceCapturee.HasValue
                && !mouvement.Promotion.HasValue
                && !subitEchec
                && !donneEchec
                && !estKiller
                && mouvement.Piece != Echiquier.TypePiece.Roi)
            {
                int reduction = CalculerReductionLMR(ctx, mouvement, profondeur, numeroCoup);

                // 1. Recherche réduite à fenêtre nulle
                score = -Negamax(e, profondeur - 1 - reduction, distanceRacine + 1, -alpha - 1, -alpha, ctx);

                // 2. Re-recherche complète si la réduction a échoué (le coup est meilleur que prévu)
                if (score > alpha)
                {
                    score = -Negamax(e, profondeur - 1 + extension, distanceRacine + 1, -beta, -alpha, ctx);
                }
            }
            else
            {       // Recherche normale classique (sans réduction)
                if (numeroCoup == 0)
                {   // Premier coup : recherche normale pleine fenêtre
                    score = -Negamax(e, profondeur - 1 + extension, distanceRacine + 1, -beta, -alpha, ctx);
                }
                else
                {   // Coup suivant : recherche à fenêtre nulle
                    score = -Negamax(e, profondeur - 1 + extension, distanceRacine + 1, -alpha - 1, -alpha, ctx);
                    if (score > alpha && score < beta)
                    {   // Si le coup semble meilleur, refaire une recherche complète
                        score = -Negamax(e, profondeur - 1 + extension, distanceRacine + 1, -beta, -alpha, ctx);
                    }
                }
            }

            if (nnueJoue) BrunoNNUE.Annuler(ctx.Nnue);
            e.AnnulerCoup(etatPrecedent);
            return score;
        }

        // Même coup d'après ses cases (et sa promotion) ; un tueur vide (cases égales) ne correspond à rien
        private static bool MemesCases(Echiquier.Mouvement m, Echiquier.Mouvement tueur)
            => tueur.CaseDepart != tueur.CaseArrivee && m.CaseDepart == tueur.CaseDepart
            && m.CaseArrivee == tueur.CaseArrivee && m.Promotion == tueur.Promotion;

        // Tous les 64 nœuds, lève ArretRechercheException si l'arrêt a été demandé
        private static void VerifierArretDemande(ContexteRecherche ctx)
        {
            if ((ctx.NombreDeNoeuds & 63) == 0 && GestionTempsEnCours?.DemandeArret == true)
                throw new ArretRechercheException();
        }

        // Tous les 64 nœuds, lève ArretRechercheException si le temps alloué est écoulé
        private static void VerifierTemps(ContexteRecherche ctx)
        {
            if ((ctx.NombreDeNoeuds & 63) == 0 && Recherche.GestionTempsEnCours != null)
                Recherche.GestionTempsEnCours.DoitArreter();
        }

        // Règle des 50 coups ou triple répétition : la position vaut 0
        private static bool PartieNulle(Echiquier e, int distanceRacine)
        {
            if (e.RegleDes50Coups >= 100)
                return true;

            if (distanceRacine > 0)
            {
                ulong cleActuelle = e.CleActuelle;
                // On s'arrête au dernier coup irréversible, et on remonte l'historique de 2 en 2 (même joueur)
                int limiteArriere = Math.Max(0, e.DemiCoupActuel - e.RegleDes50Coups);
                for (int i = e.DemiCoupActuel - 2; i >= limiteArriere; i -= 2)
                {
                    if (e.HistoriqueCles[i] == cleActuelle)
                        return true;
                }
            }
            return false;
        }

        // Consulte la table de transposition. Retourne true si elle permet de couper tout de suite
        // (le score à renvoyer est alors dans scoreRetour). Sinon alpha et beta ont pu être resserrés
        // et coupTT contient le coup de la table (à essayer en premier), ou rien.
        private static bool SonderTable(ContexteRecherche ctx, Echiquier e, int profondeur, int distanceRacine,
                                        ref int alpha, ref int beta, out Echiquier.Mouvement coupTT, out int scoreRetour)
        {
            coupTT = default;
            scoreRetour = 0;
            EntreeTT? entree = TT.Recuperer(e.CleActuelle);
            if (!entree.HasValue)
                return false;

            // On a une entrée pour la position actuelle, mais est-elle exploitable à ce stade de la recherche ?
            ctx.InterrogationsTT++;

            // Profondeur > 0 (strict) pour interdire les coups de Quiescence (-1 ou 0)
            if (entree.Value.Profondeur > 0 && entree.Value.CaseDepartMeilleur != entree.Value.CaseArriveeMeilleur)
            {
                coupTT = new Echiquier.Mouvement(
                    entree.Value.CaseDepartMeilleur,
                    entree.Value.CaseArriveeMeilleur,
                    Echiquier.TypePiece.Pion,
                    null,
                    entree.Value.PromotionMeilleur
                );
            }

            // Récupération et ajustement du score pour les transpositions de Mat
            int scoreTT = FiltrerScoreTT_Sortie(entree.Value.Score, distanceRacine);

            // Jamais de coupure à la racine (2026-11-25) : la table est gardée d'un coup à l'autre, une coupure ici terminerait
            // l'itération sans chercher ni remplir la PV. Le coup de la table sert alors seulement au tri.
            if (entree.Value.Profondeur >= profondeur && distanceRacine > 0)
            {
                if (entree.Value.Type == TypeBorne.Exact)
                {   // Valeur exacte : coupure immédiate
                    ctx.UtilisationsCoupTT++;
                    ctx.CoupuresTT++;
                    if (distanceRacine == 0 && coupTT.CaseDepart != coupTT.CaseArrivee)
                    {
                        ctx.TablePV[0, 0] = coupTT;
                        ctx.LongueurPV[0] = 1;
                    }
                    scoreRetour = scoreTT;
                    return true;
                }
                else if (entree.Value.Type == TypeBorne.BorneInferieure)
                {   // Score minimum connu : si c'est déjà >= beta, coupure beta directe
                    if (scoreTT >= beta)
                    {
                        ctx.UtilisationsCoupTT++;
                        ctx.CoupuresTT++;
                        scoreRetour = scoreTT;
                        return true;
                    }
                    alpha = Math.Max(alpha, scoreTT);
                }
                else if (entree.Value.Type == TypeBorne.BorneSuperieure)
                {   // Score maximum connu : si c'est déjà <= alpha, coupure alpha directe
                    if (scoreTT <= alpha)
                    {
                        ctx.UtilisationsCoupTT++;
                        ctx.CoupuresTT++;
                        scoreRetour = scoreTT;
                        return true;
                    }
                    beta = Math.Min(beta, scoreTT);
                }
                if (alpha >= beta)
                {   // Effondrement de la fenêtre alpha-bêta
                    ctx.UtilisationsCoupTT++;
                    ctx.CoupuresTT++;
                    scoreRetour = alpha;
                    return true;
                }
            }
            return false;
        }

        // Null Move Pruning : retourne true si passer son tour laisse la position assez forte pour couper (le nœud renvoie alors beta)
        private static bool CoupNulDonneCoupure(ContexteRecherche ctx, Echiquier e, int profondeur, int distanceRacine, int beta)
        {
            int Réduction = 2 + profondeur / 4;
            Réduction = Math.Min(Réduction, 4);

            // SÉCURITÉ ZUGZWANG : le camp actif doit avoir des pièces (mineures ou majeures) en plus de son Roi et ses pions.
            // S'il ne lui reste que des pions, on désactive le Null Move pour éviter de rater un Zugzwang.
            ulong piecesDuCamp = e.CoteBlanc
                ? (e.ReineBlanche | e.ToursBlanches | e.FousBlancs | e.CavaliersBlancs)
                : (e.ReineNoire | e.ToursNoires | e.FousNoirs | e.CavaliersNoirs);

            bool roiActifEnEchec = e.RoiEnEchec(e.CoteBlanc);

            if (profondeur >= 3 && !roiActifEnEchec && piecesDuCamp != 0UL)
            {   // On sauvegarde la case en-passant actuelle avant de l'annuler
                int enPassantSauvegarde = e.CaseEnPassant;

                e.JouerCoupNul();
                bool nnueNul = BrunoNNUE.JouerCoupNul(ctx.Nnue);
                // Recherche réduite avec une fenêtre nulle (Null Window Search)
                int scoreNull = -Negamax(e, profondeur - 1 - Réduction, distanceRacine + 1, -beta, -beta + 1, ctx);
                if (nnueNul) BrunoNNUE.Annuler(ctx.Nnue);
                e.AnnulerCoupNul(enPassantSauvegarde);

                // Coupure Beta, sauf si le score indique un mat (très proche de l'infini) : on évite les faux positifs
                if (scoreNull >= beta && scoreNull < INFINI - 10000)
                    return true;
            }
            return false;
        }

        // Réduction LMR d'un coup calme : base de la table, moins un si l'historique est bon, entre 1 et profondeur - 1
        private static int CalculerReductionLMR(ContexteRecherche ctx, Echiquier.Mouvement mouvement, int profondeur, int numeroCoup)
        {
            int dIdx = Math.Min(profondeur, 63);
            int mIdx = Math.Min(numeroCoup, 63);
            int reduction = TableLMR[dIdx, mIdx];

            // Si le coup a un bon score historique, on réduit moins
            int scoreHist = ctx.TableHistorique[mouvement.CaseDepart, mouvement.CaseArrivee];
            if (scoreHist > 10000) reduction--;

            // Sécurité pour garder au moins 1 pli de profondeur
            return Math.Clamp(reduction, 1, profondeur - 1);
        }

        // Le coup améliore alpha : il devient le premier coup de la variante principale (PV) de ce pli,
        // suivi de la PV du pli suivant
        private static void MettreAJourPV(ContexteRecherche ctx, Echiquier.Mouvement mouvement, int distanceRacine)
        {
            ctx.TablePV[distanceRacine, distanceRacine] = mouvement;

            int limiteSuivante = Math.Min(ctx.LongueurPV[distanceRacine + 1], 127);
            for (int suivant = distanceRacine + 1; suivant < limiteSuivante; suivant++)
            {
                ctx.TablePV[distanceRacine, suivant] = ctx.TablePV[distanceRacine + 1, suivant];
            }
            ctx.LongueurPV[distanceRacine] = limiteSuivante;
        }

        // Un coup calme provoque une coupure : il devient coup tueur et son historique augmente
        private static void EnregistrerCoupureCalme(ContexteRecherche ctx, Echiquier.Mouvement mouvement, int profondeur, int distanceRacine)
        {
            if (!mouvement.PieceCapturee.HasValue && distanceRacine < 64)
            {   // On décale le premier tueur en position 2, et on place le nouveau en position 1
                ctx.KillerMoves[distanceRacine, 1] = ctx.KillerMoves[distanceRacine, 0];
                ctx.KillerMoves[distanceRacine, 0] = mouvement;

                // Plus la profondeur est grande, plus le coup est important
                ctx.TableHistorique[mouvement.CaseDepart, mouvement.CaseArrivee] += profondeur * profondeur;
            }
        }

        // Stocke le résultat du nœud dans la table de transposition (le score de mat est rendu indépendant de la profondeur)
        private static void StockerResultatTT(Echiquier e, int profondeur, int distanceRacine, int alphaOrigine, int betaOrigine,
                                              int meilleurScore, Echiquier.Mouvement meilleurMouvement)
        {
            TypeBorne type;
            if (meilleurScore <= alphaOrigine) type = TypeBorne.BorneSuperieure;
            else if (meilleurScore >= betaOrigine) type = TypeBorne.BorneInferieure;
            else type = TypeBorne.Exact;
            TT.Stocker(e.CleActuelle, FiltrerScoreTT_Entree(meilleurScore, distanceRacine), profondeur, type, meilleurMouvement);
        }
        private static int FiltrerScoreTT_Entree(int score, int distanceRacine)
        {   // À l'entrée, on transforme le score relatif à la racine en score absolu (indépendant de la profondeur)
            if (score > INFINI - 10000) return score + distanceRacine;
            if (score < -INFINI + 10000) return score - distanceRacine;
            return score;
        }
        private static int FiltrerScoreTT_Sortie(int score, int distanceRacine)
        {   // À la sortie, on retransforme le score absolu en score relatif à la racine actuelle
            if (score > INFINI - 10000) return score - distanceRacine;
            if (score < -INFINI + 10000) return score + distanceRacine;
            return score;
        }

        // Table de transposition en quiescence. Retourne true si elle permet de couper tout de suite
        // (le score à renvoyer est alors dans scoreRetour). Sinon alpha et beta ont pu être resserrés ;
        // entree est l'entrée trouvée, pour décider plus tard s'il faut la remplacer.
        private static bool SonderTableQuiescence(ContexteRecherche ctx, Echiquier e, int distanceRacine,
                                                  ref int alpha, ref int beta, out EntreeTT? entree, out int scoreRetour)
        {
            scoreRetour = 0;
            entree = TT.Recuperer(e.CleActuelle);
            if (!entree.HasValue)
                return false;

            // SÉCURITÉ : On n'utilise l'entrée TT que si sa profondeur de calcul
            // est au moins égale à celle de la quiescence (ici, -1)
            if (entree.Value.Profondeur >= -1)
            {
                // Filtrage du score de Mat en sortie de la TT
                int scoreTT = FiltrerScoreTT_Sortie(entree.Value.Score, distanceRacine);

                if (entree.Value.Type == TypeBorne.Exact)
                {
                    ctx.CoupuresTTQuiescence++; // C'est une vraie coupure directe
                    scoreRetour = scoreTT;
                    return true;
                }
                else if (entree.Value.Type == TypeBorne.BorneInferieure)
                    alpha = Math.Max(alpha, scoreTT);
                else if (entree.Value.Type == TypeBorne.BorneSuperieure)
                    beta = Math.Min(beta, scoreTT);

                if (alpha >= beta)
                {
                    ctx.CoupuresTTQuiescence++; // C'est un élagage alpha/beta grâce aux bornes de la TT
                    scoreRetour = scoreTT;
                    return true;
                }
            }
            return false;
        }

        // Tri des coups de la quiescence : MVV-LVA affiné par le SEE. Une capture dont l'échange est perdant
        // est poussée tout en bas ; un coup calme (parade d'échec) passe après les captures.
        private static void TrierCapturesQuiescence(Echiquier e, List<Echiquier.Mouvement> captures)
        {
            for (int i = 0; i < captures.Count; i++)
            {
                var m = captures[i];
                if (m.PieceCapturee.HasValue)
                {
                    int valCapturee = ObtenirValeurPiece(m.PieceCapturee.Value);
                    int valAttaquante = ObtenirValeurPiece(m.Piece);

                    // On utilise le SEE pour corriger le tri MVV-LVA de base (si l'échange statique est perdant, on le pénalise)
                    int scoreSEE = EchangeStatiqueEval.ObtenirSEE(e, m.CaseArrivee, valCapturee, valAttaquante);

                    if (scoreSEE >= 0)
                        m.ScoreTri = 100000 + (valCapturee * 100) - valAttaquante;
                    else
                        m.ScoreTri = -100000 + scoreSEE; // Mauvaise capture poussée tout en bas de l'échelle
                }
                else
                {   // Un coup calme pour parer un échec est important, mais passe après les captures.
                    // On lui donne un score de base ou un bonus si c'est une fuite du Roi.
                    m.ScoreTri = m.Piece == Echiquier.TypePiece.Roi ? 10 : 0;
                }
                captures[i] = m;
            }
        }

        // Élagage d'une capture de la quiescence, sans la jouer : marge sur le score statique, perte nette au SEE,
        // puis Delta Pruning (jamais en échec, jamais pour une promotion ni pour la prise d'une tour ou d'une dame)
        private static bool CaptureElaguee(Echiquier e, Echiquier.Mouvement m, bool enEchec, int scoreStatique, int alpha)
        {
            if (!enEchec && m.PieceCapturee.HasValue && m.Promotion == null)
            {
                int valCapturee = ObtenirValeurPiece(m.PieceCapturee.Value);
                // Marge dynamique (200 + valeurCapture/2)
                if (scoreStatique + valCapturee + 200 + (valCapturee / 2) < alpha)
                    return true;
                int valAttaquante = ObtenirValeurPiece(m.Piece);

                int see = EchangeStatiqueEval.ObtenirSEE(e, m.CaseArrivee, valCapturee, valAttaquante);
                int seuil = -ObtenirValeurPiece(m.Piece) / 2;
                if (see < seuil)
                    return true;
            }

            // Delta Pruning : même en capturant la pièce, le score reste loin derrière alpha
            if (!enEchec && m.PieceCapturee.HasValue)
            {
                int valeurCapture = ObtenirValeurPiece(m.PieceCapturee ?? Echiquier.TypePiece.Pion);
                // SÉCURITÉ : si la capture concerne une Tour ou une Reine (valeur >= 500), on ne peut pas élaguer aveuglément
                if (valeurCapture < 500)
                {
                    int margeDelta = 200 + (valeurCapture / 2);
                    if (scoreStatique + valeurCapture + margeDelta < alpha && m.Promotion == null)
                        return true;
                }
            }
            return false;
        }

        // Stocke un résultat de quiescence (profondeur -1), sauf si la table contient déjà une entrée de recherche normale
        private static void StockerQuiescence(Echiquier e, EntreeTT? entree, int distanceRacine, int score, TableTransposition.TypeBorne borne)
        {
            if (!entree.HasValue || entree.Value.Profondeur < 0)
                TT.Stocker(e.CleActuelle, FiltrerScoreTT_Entree(score, distanceRacine), -1, borne, default);
        }

        private static int RechercheQuiescence(ContexteRecherche ctx, Echiquier e, int alpha, int beta, int distanceRacine)
        {
            ctx.NombreDeNoeuds++;
            ctx.NoeudsQuiescence++; // On compte l'entrée en Quiescence
            if ((ctx.NombreDeNoeuds & 63) == 0)
            {   // La quiescence vérifie aussi le temps (elle ne le faisait pas : arrêt "stop" seulement)
                GestionTempsEnCours?.DoitArreter();
            }

            if (e.RegleDes50Coups >= 100) return 0;     // Règle des 50 coups : match nul immédiat

            // On vérifie si on est en échec, tout simplement.
            // La sécurité globale contre l'explosion de nœuds est gérée plus bas par ta limite de sécurité 
            bool enEchec = e.RoiEnEchec(e.CoteBlanc);

            // --- NOUVEAU : LIMITE DE SÉCURITÉ ÉLASTIQUE ---
            // Si on a creusé trop loin (ex: +15 plis) ET qu'on n'est pas en échec,
            // on arrête l'explosion tactique et on rend l'évaluation actuelle.
            // Si on est en échec, on continue pour ne pas évaluer une position instable (ou rater un mat).
            if (!enEchec && distanceRacine > ctx.ProfondeurReglee + 10)
            {
                return BrunoNNUE.EvaluerNNUE(e, ctx.Nnue);
            }

            // --- Optimisation TT en Quiescence ---
            if (SonderTableQuiescence(ctx, e, distanceRacine, ref alpha, ref beta, out EntreeTT? entree, out int scoreTT))
                return scoreTT;

            // On suit la profondeur réelle (pour voir si ça dépasse 6)
            if (distanceRacine > ctx.ProfondeurMaxAtteinte) ctx.ProfondeurMaxAtteinte = distanceRacine;

            // --- 1. Standing Pat (Évaluation statique) ---
            // On considère que le joueur peut choisir de ne pas capturer s'il est satisfait.
            int scoreStatique = BrunoNNUE.EvaluerNNUE(e, ctx.Nnue);

            // Si on est en échec, le score statique n'est pas fiable (on ne peut pas "rester tranquille" en étant menceau)
            if (!enEchec)
            {
                if (scoreStatique >= beta) return beta;
                if (scoreStatique > alpha) alpha = scoreStatique;
            }

            // Si on est en échec, le score statique ne veut rien dire.
            // On initialise 'meilleurScoreQ' à une valeur plancher relative au Mat.
            int meilleurScoreQ = enEchec ? -99999 + distanceRacine : scoreStatique;

            // --- 2. Génération des captures uniquement ---
            // ✅ Utilisation du Pool avec décalage pour ne pas écraser le Negamax
            var captures = ctx.PoolListes[distanceRacine + 32];
            int coupsJouesLegaux = 0;

            // Point 3 : Isolation robuste via try/finally pour le nettoyage systématique du pool de captures
            try
            {
                if (enEchec)
                    e.GenererMouvementsLegauxCommun(captures);
                else
                {
                    e.GenererCapturesLegales(captures, false);
                }

                // --- 3. Tri des coups (MVV-LVA affiné par le SEE) ---
                // Most Valuable Victim - Least Valuable Attacker
                // On trie pour explorer les meilleures captures en premier (ex: Fou prend Reine avant Pion prend Pion)
                TrierCapturesQuiescence(e, captures);

                int alphaInitial = alpha;   // Sauvegarde de l'alpha initial pour déterminer le bon type de borne en fin de fonction ---

                for (int i = 0; i < captures.Count; i++)
                {   // On amène le meilleur coup parmi ceux restants à l'index i
                    ChoisirMeilleurMouvement(captures, i);
                    var m = captures[i];

                    // Élagage des captures perdantes (marge, SEE) et Delta Pruning : voir CaptureElaguee
                    if (CaptureElaguee(e, m, enEchec, scoreStatique, alpha))
                        continue;

                    // --- 5. Simulation du coup ---
                    bool nnueJoue = BrunoNNUE.JouerCoup(ctx.Nnue, m.CaseDepart, m.CaseArrivee, m.Promotion);
                    var etatPrecedent = e.JouerCoup(m.CaseDepart, m.CaseArrivee, m.Promotion);

                    coupsJouesLegaux++;

                    int score = -RechercheQuiescence(ctx, e, -beta, -alpha, distanceRacine + 1);
                    if (nnueJoue) BrunoNNUE.Annuler(ctx.Nnue);
                    e.AnnulerCoup(etatPrecedent);

                    if (score >= beta)
                    {        // Élagage Alpha-Beta standard
                        // Si on bat beta, on stocke IMMÉDIATEMENT cette borne inférieure avant de s'enfuir !
                        StockerQuiescence(e, entree, distanceRacine, score, TableTransposition.TypeBorne.BorneInferieure);
                        return score;
                    }

                    if (score > meilleurScoreQ)
                    {   // Mise à jour du meilleur score trouvé en Quiescence
                        meilleurScoreQ = score;
                        if (score > alpha) alpha = score;
                    }
                }

                // Si on était en échec et qu'aucun coup n'a pu être joué, c'est Échec et Mat.
                if (enEchec && coupsJouesLegaux == 0)
                {
                    return -99999 + distanceRacine;
                }

                // --- STOCKAGE EN FIN DE BOUCLE (Fail-Low ou Exact) ---
                // Une BorneSuperieure (Fail-Low) n'arrive QUE si le meilleur score n'a même pas pu atteindre l'alpha d'ENTRÉE de la fonction.
                StockerQuiescence(e, entree, distanceRacine, meilleurScoreQ,
                    meilleurScoreQ <= alphaInitial ? TableTransposition.TypeBorne.BorneSuperieure : TableTransposition.TypeBorne.Exact);
            }
            finally
            {
                captures.Clear(); // Nettoyage indispensable pour que la pile reste libre de tout nœud fantôme
            }

            return meilleurScoreQ;
        }

        private static void ChoisirMeilleurMouvement(List<Mouvement> mouvements, int indexDebut)
        {   // Trouve le meilleur mouvement (avec le score de tri le plus élevé) parmi les mouvements
            int meilleurIndex = indexDebut;
            int meilleurScore = mouvements[indexDebut].ScoreTri;
            for (int i = indexDebut + 1; i < mouvements.Count; i++)
            {
                if (mouvements[i].ScoreTri > meilleurScore)
                {
                    meilleurScore = mouvements[i].ScoreTri;
                    meilleurIndex = i;
                }
            }
            // Échange (Swap)
            (mouvements[meilleurIndex], mouvements[indexDebut]) = (mouvements[indexDebut], mouvements[meilleurIndex]);
        }

        private static Mouvement? ObtenirPremierMouvement(Echiquier e)
        {   // Reconstruit le premier mouvement de la ligne principale à partir de la table de transposition.
            EntreeTT? entree = TT.Recuperer(e.CleActuelle);

            if (!entree.HasValue || (entree.Value.CaseDepartMeilleur == 0 && entree.Value.CaseArriveeMeilleur == 0))
                return null;

            int de = entree.Value.CaseDepartMeilleur;
            int vers = entree.Value.CaseArriveeMeilleur;
            char? promo = entree.Value.PromotionMeilleur;

            // On reconstruit les infos manquantes pour la structure Mouvement
            Echiquier.TypePiece type = e.IdentifierTypePiece(de);

            // Détection de capture (via le bitboard combiné)
            Echiquier.TypePiece? capturee = null;
            if ((e.ObtenirToutesLesPieces() & Bitboard.CaseVersBitboard(vers)) != 0)
            {
                capturee = e.IdentifierTypePiece(vers);
            }

            // Détection roque / en passant simplifiée pour l'objet Mouvement
            bool estRoque = (type == Echiquier.TypePiece.Roi && Math.Abs(de % 8 - vers % 8) == 2);
            bool estEP = (type == Echiquier.TypePiece.Pion && de % 8 != vers % 8 && capturee == null);

            return new Mouvement(de, vers, type, capturee, promo, estEP, estRoque);
        }
        public static string ObtenirLignePV(Echiquier e, int profondeurActuelle, ContexteRecherche? ctx = null)
        {   // Reconstruit la ligne principale à partir de la table PV pour l'affichage dans le protocole UCI.
            ctx ??= DernierContexte ?? new ContexteRecherche();
            System.Text.StringBuilder pb = new();

            int taillePV = ctx.LongueurPV[0];
            int limite = Math.Min(taillePV, profondeurActuelle);

            for (int i = 0; i < limite; i++)
            {
                Mouvement m = ctx.TablePV[0, i];

                if (m.CaseDepart == m.CaseArrivee) break;

                string mouvementTexte = e.TraduireEnNotationAlgebraique(m);
                pb.Append(mouvementTexte + " ");
            }
            return pb.ToString().Trim();
        }
        private static int AppliquerScalingStockfish(Echiquier echiquier, int score)
        {
            int nbPions = BitOperations.PopCount(echiquier.PionsBlancs) + BitOperations.PopCount(echiquier.PionsNoirs);
            int materielNonPions = (BitOperations.PopCount(echiquier.CavaliersBlancs) +
                                    BitOperations.PopCount(echiquier.CavaliersNoirs)) * 320 +
                                    (BitOperations.PopCount(echiquier.FousBlancs) +
                                        BitOperations.PopCount(echiquier.FousNoirs)) * 330 +
                                    (BitOperations.PopCount(echiquier.ToursBlanches) +
                                        BitOperations.PopCount(echiquier.ToursNoires)) * 500 +
                                    (BitOperations.PopCount(echiquier.ReineBlanche) +
                                        BitOperations.PopCount(echiquier.ReineNoire)) * 900;
            int npm = materielNonPions / 64;

            return (score * (915 + npm + 9 * nbPions)) / 1024;
        }

    // Ajout de l'entrée TT pour prioriser le meilleur coup trouvé précédemment
    private static int CalculerScoreMouvement(ContexteRecherche ctx, Echiquier.Mouvement m, Echiquier e, Echiquier.Mouvement coupTT, int distanceRacine)
        {   // SÉCURITÉ TT : On ne vérifie le coup TT que s'il est valide (cases différentes et cohérentes)
            if (coupTT.CaseDepart != coupTT.CaseArrivee
                && coupTT.CaseDepart >= 0 && coupTT.CaseDepart < 64
                && coupTT.CaseArrivee >= 0 && coupTT.CaseArrivee < 64)
            {
                // PRIORITÉ ABSOLUE : Si les cases correspondent ET que s'il y a une promotion, c'est la même
                if (m.CaseDepart == coupTT.CaseDepart
                    && m.CaseArrivee == coupTT.CaseArrivee
                    && m.Promotion == coupTT.Promotion)     // Sécurité indispensable pour les promotions !
                {
                    return 10000000;                        // Priorité absolue sur tout le monde
                }
            }

            // Captures (MVV-LVA) : entre 100 000 et 105 000 points
            if (m.PieceCapturee.HasValue)
            {
                int indexCapturee = (int)m.PieceCapturee.Value;
                int indexAttaquante = (int)m.Piece;

                // SÉCURITÉ INDEX : On s'assure que les enums convertis en int rentrent bien dans le tableau de valeurs (0 à 5)
                if (indexCapturee >= 0 && indexCapturee < 6 && indexAttaquante >= 0 && indexAttaquante < 6)
                {
                    int valCapturee = ObtenirValeurPiece(m.PieceCapturee.Value);
                    int valAttaquante = ObtenirValeurPiece(m.Piece);

                    // --- AJOUT RECOURS AU SEE POUR LE TRI ---
                    // On évalue si l'échange de matériel est rentable statiquement
                    int scoreSEE = EchangeStatiqueEval.ObtenirSEE(e, m.CaseArrivee, valCapturee, valAttaquante);

                    if (scoreSEE >= 0)
                    {   // C'est une bonne capture ou un échange égal : on garde la priorité haute classique
                        return 100000 + (valCapturee * 100) - valAttaquante;
                    }
                    else
                    {   // Mauvaise capture : reléguée sous le niveau zéro pour l'exclure du haut de l'arbre
                        return -100000 + scoreSEE;
                    }
                }
            }

            if (m.Promotion.HasValue) return 90000;     // Promotions : 90 000 points

            if (distanceRacine < 128)                    // Priorité aux Killer Moves
            {
                var k1 = ctx.KillerMoves[distanceRacine, 0];
                if (k1.CaseDepart != k1.CaseArrivee && m.CaseDepart == k1.CaseDepart && m.CaseArrivee == k1.CaseArrivee)
                    return 80000; // Premier tueur

                var k2 = ctx.KillerMoves[distanceRacine, 1];
                if (k2.CaseDepart != k2.CaseArrivee && m.CaseDepart == k2.CaseDepart && m.CaseArrivee == k2.CaseArrivee)
                    return 70000; // Deuxième tueur
            }

            // 1. On récupère le score de l'historique (bridé à 50000)
            int scoreHistorique = Math.Min(ctx.TableHistorique[m.CaseDepart, m.CaseArrivee], 50000);
            // 2. Si l'historique a du score, on le renvoie en priorité
            if (scoreHistorique > 0) return scoreHistorique;
            /*
            // 3. Si l'historique est à 0, on va lire la valeur de la case d'arrivée 
            // dans les tables PST_MG pour départager et ordonner intelligemment les coups calmes !
            if (m.CaseArrivee >= 0 && m.CaseArrivee < 64)
            {   // SÉCURITÉ INDEX : On s'assure que CaseArrivee est bien entre 0 et 63
                switch (m.Piece)
                {
                    case Echiquier.TypePiece.Cavalier:
                        return Evaluation.PST_CAV_MG[m.CaseArrivee];
                    case Echiquier.TypePiece.Fou:
                        return Evaluation.PST_FOU_MG[m.CaseArrivee];
                    case Echiquier.TypePiece.Pion:
                        return Evaluation.PST_PION_MG[m.CaseArrivee];
                    case Echiquier.TypePiece.Tour:
                        return Evaluation.PST_TOUR_MG[m.CaseArrivee];
                    case Echiquier.TypePiece.Reine:
                        return Evaluation.PST_DAME_MG[m.CaseArrivee];
                    case Echiquier.TypePiece.Roi:
                        return Evaluation.PST_ROI_MG[m.CaseArrivee];
                }
            }
            */
            return 0;
        }

        private static int[,] InitialiserTableLMR()
        {   // Initialisation de la Matrice de réduction LMR précalculée [Profondeur, IndexCoup]
            int[,] table = new int[64, 64];
            for (int depth = 1; depth < 64; depth++)
            {
                for (int moveIndex = 1; moveIndex < 64; moveIndex++)
                {   // Formule standard logarithmique : plus la profondeur et l'index augmentent, plus on réduit
                    table[depth, moveIndex] = (int)(0.5 + Math.Log(depth) * Math.Log(moveIndex) / 1.95);
                }
            }
            return table;
        }
        public static void ReinitialiserMoteur(TableTransposition tableTransposition)
        {   // 1. Vidage complet de la mémoire de la TT
            tableTransposition?.Vider();

            // 2. Remise à zéro de toutes les statistiques de recherche
            // CoupuresTT = 0;
            // UtilisationsCoupTT = 0;
            // InterrogationsTT = 0;
            // UtilisationsTTQuiescence = 0;
            // CoupuresTTQuiescence = 0;
        }
        private static int ObtenirValeurPiece(Echiquier.TypePiece type)
        {   // On utilise le type comme index (si ton Enum commence à 0 pour Vide, 1 pour Pion, etc.)
            return ValeursPieces[(int)type];
        }
    }
}
