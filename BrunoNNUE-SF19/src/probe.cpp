#include "probe.h"

#include <algorithm>
#include <atomic>
#include <cassert>
#include <cmath>
#include <cstdlib>
#include <deque>
#include <filesystem>
#include <memory>
#include <mutex>
#include <string>

#include "attacks.h"
#include "bitboard.h"
#include "position.h"
#include "types.h"
#include "evaluate.h"
#include "nnue/network.h"
#include "nnue/nnue_accumulator.h"
#include "nnue/nnue_misc.h"

namespace Stockfish::Probe {

    namespace {

        // Stockfish 19 n'a plus qu'un seul réseau (le petit réseau a disparu).
        std::unique_ptr<Eval::NNUE::Network> g_network;
        Eval::NNUE::EvalFile                 g_evalFile;
        std::mutex                           g_initMutex;

        // Un contexte NNUE par thread.
        // Indispensable pour les appels concurrents du moteur (Lazy SMP).
        thread_local std::unique_ptr<Eval::NNUE::AccumulatorStack>  t_accumulators;
        thread_local std::unique_ptr<Eval::NNUE::AccumulatorCaches> t_caches;

        // Génération du réseau : augmentée à chaque chargement réussi (setoption EvalFile).
        // Les caches d'un thread construits avec un réseau précédent sont alors refaits.
        std::atomic<int>   g_generation{0};
        thread_local int   t_generation = -1;


        bool ensure_thread_context() {
            if (!g_network)
                return false;

            const int generation = g_generation.load(std::memory_order_acquire);
            if (t_generation != generation)
            {
                t_accumulators.reset();
                t_caches.reset();
                t_generation = generation;
            }

            if (!t_accumulators)
                t_accumulators = std::make_unique<Eval::NNUE::AccumulatorStack>();

            if (!t_caches)
                t_caches = std::make_unique<Eval::NNUE::AccumulatorCaches>(*g_network);

            return true;
        }


        // -----------------------------------------------------------------------------
        // Evaluation NNUE
        //
        // Reprend la partie NNUE de Eval::evaluate() de Stockfish 19
        // (sans dépendre de evaluate.cpp).
        // -----------------------------------------------------------------------------

        // Évalue la position avec la pile d'accumulateurs et les caches donnés.
        int evaluate_avec(Position&                         pos,
                          Eval::NNUE::AccumulatorStack&     pile,
                          Eval::NNUE::AccumulatorCaches&    caches,
                          int                               optimism) {

            auto [psqt, positional] = g_network->evaluate(pos, pile, caches);

            Value nnue = psqt + positional;

            int nnueComplexity = std::abs(psqt - positional);
            optimism += optimism * i64(nnueComplexity) / 476;
            nnue -= nnue * i64(nnueComplexity) / 18236;

            int material = 534 * pos.count<PAWN>() + pos.non_pawn_material();
            int v        = nnue + (nnue * i64(material) + optimism * i64(7675)) / 91000;

            // Effet de la règle des 50 coups.
            v -= v * pos.rule50_count() / 199;

            // Empêcher l'évaluation d'entrer dans la zone réservée aux tables de finales.
            v = std::clamp(v, VALUE_TB_LOSS_IN_MAX_PLY + 1, VALUE_TB_WIN_IN_MAX_PLY - 1);

            return v;
        }

        // Évaluation sans contexte : chaque évaluation repart d'un accumulateur vide.
        int evaluate_position(Position& pos, int optimism) {

            if (!ensure_thread_context())
                return 0;

            t_accumulators->reset();

            return evaluate_avec(pos, *t_accumulators, *t_caches, optimism);
        }

    }


    // -----------------------------------------------------------------------------
    // Contexte incrémental
    //
    // Un contexte (un par thread de recherche) garde une Position de Stockfish et une pile
    // d'accumulateurs. Le moteur C# y rejoue les coups de la recherche (Do/Undo) : l'accumulateur
    // est alors mis à jour à partir de celui de la position parente, sans tout recalculer.
    // -----------------------------------------------------------------------------

    struct Contexte {
        static constexpr int MaxPli = 220;      // largement sous la capacité de la pile (MAX_PLY + 1)

        Position                                       pos;
        StateInfo                                      etats[MaxPli + 2];
        Move                                           coups[MaxPli + 2];
        int                                            profondeur = 0;
        Eval::NNUE::AccumulatorStack                   pile;
        std::unique_ptr<Eval::NNUE::AccumulatorCaches> caches;
    };

    Contexte* ctx_creer() {
        if (!g_network)
            return nullptr;

        Contexte* c = new Contexte();
        c->caches   = std::make_unique<Eval::NNUE::AccumulatorCaches>(*g_network);
        return c;
    }

    void ctx_detruire(Contexte* c) { delete c; }

    bool ctx_definir(Contexte* c, const int pieceBoard[], bool blancAuTrait, int rule50) {
        if (!c)
            return false;

        c->pos.set(pieceBoard, blancAuTrait, rule50, &c->etats[0]);
        c->profondeur = 0;
        c->pile.reset();
        return true;
    }

    // Joue un coup du moteur : "promo" vaut 0 (aucune) ou le type de pièce de Stockfish (2 à 5).
    bool ctx_jouer(Contexte* c, int de, int vers, int promo) {
        if (!c || c->profondeur >= Contexte::MaxPli)
            return false;

        const Square f  = Square(de);
        const Square t  = Square(vers);
        const Piece  pc = c->pos.piece_on(f);
        if (pc == NO_PIECE)
            return false;

        Move m;
        if (type_of(pc) == KING && std::abs(int(file_of(f)) - int(file_of(t))) == 2)
            m = Move::make<CASTLING>(f, t > f ? Square(t + 1) : Square(t - 2));  // Stockfish : le roi "prend" sa tour
        else if (type_of(pc) == PAWN && promo != 0)
            m = Move::make<PROMOTION>(f, t, PieceType(promo));
        else if (type_of(pc) == PAWN && file_of(f) != file_of(t) && c->pos.piece_on(t) == NO_PIECE)
            m = Move::make<EN_PASSANT>(f, t);
        else
            m = Move(f, t);

        Dirties& d = c->pile.push();
        c->pos.do_move(m, c->etats[c->profondeur + 1], false, d, nullptr, nullptr);
        c->coups[c->profondeur++] = m;
        return true;
    }

    bool ctx_jouer_nul(Contexte* c) {
        if (!c || c->profondeur >= Contexte::MaxPli)
            return false;

        c->pos.do_null_move(c->etats[c->profondeur + 1]);
        c->coups[c->profondeur++] = Move::null();
        return true;
    }

    void ctx_annuler(Contexte* c) {
        if (!c || c->profondeur <= 0)
            return;

        const Move m = c->coups[--c->profondeur];
        if (m == Move::null())
            c->pos.undo_null_move();
        else
        {
            c->pos.undo_move(m);
            c->pile.pop();
        }
    }

    // Retourne false si la position du contexte ne correspond pas à celle du moteur
    // (occupation ou trait différents) : l'appelant évalue alors sans le contexte.
    bool ctx_evaluer(Contexte* c, int optimism, std::uint64_t occupation, bool blancAuTrait, int& valeur) {
        if (!c)
            return false;

        if (c->pos.pieces() != occupation || (c->pos.side_to_move() == WHITE) != blancAuTrait)
            return false;

        valeur = evaluate_avec(c->pos, c->pile, *c->caches, optimism);
        return true;
    }


    // -----------------------------------------------------------------------------
    // Initialisation
    // -----------------------------------------------------------------------------

    bool init(const char* netFile) {

        if (!netFile)
            return false;

        std::lock_guard<std::mutex> lock(g_initMutex);

        Attacks::init();
        Position::init();

        auto                 network = std::make_unique<Eval::NNUE::Network>();
        Eval::NNUE::EvalFile evalFile;

        // Le réseau est cherché dans le dossier courant (ou chemin donné).
        // load_external évite de dépendre du réseau embarqué.
        network->load_external(std::filesystem::path{}, std::filesystem::path(netFile), evalFile);

        if (!evalFile.current.has_value())
            return false;

        g_evalFile = evalFile;
        g_network  = std::move(network);

        // Nouvelle génération : chaque thread refera ses caches à sa prochaine évaluation.
        // Un échec de chargement (plus haut) laisse le réseau précédent en place.
        g_generation.fetch_add(1, std::memory_order_release);

        return ensure_thread_context();
    }


    // -----------------------------------------------------------------------------
    // Evaluation FEN
    // -----------------------------------------------------------------------------

    int eval(const char* fen) {

        StateListPtr states(new std::deque<StateInfo>(1));

        Position pos;

        if (pos.set(std::string(fen), false, &states->back()).has_value())
            return 0;

        return evaluate_position(pos, 0);
    }

    // -----------------------------------------------------------------------------
    // Evaluation depuis un tableau 64 cases
    // -----------------------------------------------------------------------------

    int eval(const int pieceBoard[], bool side, int rule50, int optimism) {

        StateListPtr states(new std::deque<StateInfo>(1));

        Position pos;

        pos.set(pieceBoard, side, rule50, &states->back());

        return evaluate_position(pos, optimism);
    }

    // -----------------------------------------------------------------------------
    // Evaluation depuis listes pièces / cases
    // -----------------------------------------------------------------------------

    int eval(const int pieces[], const int squares[], int pieceAmount, bool side, int rule50) {

        StateListPtr states(new std::deque<StateInfo>(1));

        Position pos;

        pos.set(pieces, squares, pieceAmount, side, rule50, &states->back());

        return evaluate_position(pos, 0);
    }
}
