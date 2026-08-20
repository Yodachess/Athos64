// ┌▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄┐
// █ Athos64 - Moteur d'échecs UCI en C#                                    █
// █ Copyright (C) 2026 Bruno COURTOIS                                      █
// █ SPDX-License-Identifier: GPL-3.0-or-later                              █
// █ See the LICENSE file in the project root for full license information. █
// └▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀┘

/*
Structure de Athos64/
┌─ Athos64.sln             // Solution Visual Studio
├── **MoteurEchecs/**                  // Dossier principal du moteur
│   ├── Bitboards/                     // Gestion des bitboards
│   │   ├── Bitboard.cs                // Définition des bitboards et opérations de base
│   │   ├── Attaques.cs                // Calcul des attaques (rayons X, lignes, diagonales, etc.)
│   │   └── Masques.cs                 // Masques pour les cases, les lignes, etc.
│   ├── Pieces/                        // Logique spécifique aux pièces
│   │   ├── Roi.cs                     // Mouvements et sécurité du roi
│   │   ├── Reine.cs                   // Mouvements de la reine
│   │   ├── Tour.cs                    // Mouvements de la tour
│   │   ├── Fou.cs                     // Mouvements du fou
│   │   ├── Cavalier.cs                // Mouvements du cavalier
│   │   └── Pion.cs                    // Mouvements et promotions des pions
│   ├── Echiquier.cs                   // Représentation de l'échiquier (bitboards, état du jeu)
│   ├── Moteur.cs                     // Logique principale du moteur (recherche de coups, évaluation)
│   ├── UCI.cs                         // Gestion du protocole UCI (communication avec le GUI)
│   ├── Evaluation.cs                  // Évaluation statique de la position
│   ├── Recherche.cs                   // Algorithmes de recherche (Minimax, Alpha-Bêta, etc.)
│   └── Utils/                         // Utilitaires
│       ├── Helpers.cs                 // Fonctions utiles (conversion de cases, etc.)
│       └── Constants.cs               // Constantes (valeurs des pièces, tailles, etc.)
├── **Tests/**                         // Tests unitaires (optionnel mais recommandé)
│   ├── TestBitboards.cs
│   ├── TestMouvements.cs
│   └── ...
*/

/* 
Étapes de Développement Progressif

Initialisation :
Créer la structure du projet dans Visual Studio.
Implémenter les bitboards de base et les masques.

Mouvements des Pièces :
Commencer par les pions (mouvements, captures, promotions).
Puis ajouter les autres pièces (cavalier, fou, tour, reine, roi).

Gestion UCI :
Implémenter les commandes UCI de base (uci, isready, position, go).
Tester avec mon GUI pour vérifier la communication.

Recherche et Évaluation :

Ajouter l'algorithme Minimax avec élagage alpha-bêta.
Implémenter une évaluation simple (matériel), puis l'améliorer (positionnelle).

Optimisations :
Ajouter une table de transposition.
Implémenter la détection des échecs et mat.
Optimiser la génération des mouvements (ex: attaques pré-calculées).

*/

