#pragma once

#include <cstdint>

namespace Stockfish::Probe {
    bool init(const char* netFile);
    int eval(const char* fen);
    int eval(const int pieceBoard[], bool side, int rule50, int optimism);
    int eval(const int pieces[], const int squares[], int pieceAmount, bool side, int rule50);

    // Contexte incrémental (position + accumulateurs), un par thread de recherche
    struct Contexte;
    Contexte* ctx_creer();
    void      ctx_detruire(Contexte* c);
    bool      ctx_definir(Contexte* c, const int pieceBoard[], bool blancAuTrait, int rule50);
    bool      ctx_jouer(Contexte* c, int de, int vers, int promo);
    bool      ctx_jouer_nul(Contexte* c);
    void      ctx_annuler(Contexte* c);
    bool      ctx_evaluer(Contexte* c, int optimism, std::uint64_t occupation, bool blancAuTrait, int& valeur);
}
