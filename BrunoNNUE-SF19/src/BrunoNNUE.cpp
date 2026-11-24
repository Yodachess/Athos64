#include "BrunoNNUE.h"
#include "probe.h"

#include <iostream>
#include <exception>

// Chaque fonction exportée attrape toutes les exceptions : rien ne doit traverser la frontière C#.

extern "C" BRUNONNUE_API int __cdecl NNUE_Init(const char* netFile)
{
    if (!netFile)
        return 0;

    try
    {
        return Stockfish::Probe::init(netFile) ? 1 : 0;
    }
    catch (const std::exception& e)
    {
        std::cerr << "[NNUE EXCEPTION] NNUE_Init : " << e.what() << std::endl;
        return 0;
    }
    catch (...)
    {
        std::cerr << "[NNUE EXCEPTION] NNUE_Init : exception inconnue" << std::endl;
        return 0;
    }
}

extern "C" BRUNONNUE_API int __cdecl NNUE_EvalFEN(const char* fen)
{
    if (!fen)
        return 0;

    try
    {
        return Stockfish::Probe::eval(fen);
    }
    catch (const std::exception& e)
    {
        std::cerr << "[NNUE EXCEPTION] NNUE_EvalFEN : " << e.what() << std::endl;
        return 0;
    }
    catch (...)
    {
        std::cerr << "[NNUE EXCEPTION] NNUE_EvalFEN : exception inconnue" << std::endl;
        return 0;
    }
}

extern "C" BRUNONNUE_API int __cdecl NNUE_EvalBoard(
    const int* pieceBoard,
    int side,
    int rule50,
    int optimism)
{
    if (!pieceBoard)
        return 0;

    try
    {
        return Stockfish::Probe::eval(pieceBoard, side == 0, rule50, optimism);
    }
    catch (const std::exception& e)
    {
        std::cerr << "[NNUE EXCEPTION] NNUE_EvalBoard : " << e.what() << std::endl;
        return 0;
    }
    catch (...)
    {
        std::cerr << "[NNUE EXCEPTION] NNUE_EvalBoard : exception inconnue" << std::endl;
        return 0;
    }
}

extern "C" BRUNONNUE_API int __cdecl NNUE_EvalPieces(
    const int* pieces,
    const int* squares,
    int pieceAmount,
    int side,
    int rule50)
{
    if (!pieces || !squares || pieceAmount < 0 || pieceAmount > 32)
        return 0;

    try
    {
        return Stockfish::Probe::eval(pieces, squares, pieceAmount, side == 0, rule50);
    }
    catch (const std::exception& e)
    {
        std::cerr << "[NNUE EXCEPTION] NNUE_EvalPieces : " << e.what() << std::endl;
        return 0;
    }
    catch (...)
    {
        std::cerr << "[NNUE EXCEPTION] NNUE_EvalPieces : exception inconnue" << std::endl;
        return 0;
    }
}

// ----------------------------------------------------------------------------
// Contexte incrémental
// ----------------------------------------------------------------------------

extern "C" BRUNONNUE_API void* __cdecl NNUE_CtxCreate()
{
    try { return Stockfish::Probe::ctx_creer(); }
    catch (...) { std::cerr << "[NNUE EXCEPTION] NNUE_CtxCreate" << std::endl; return nullptr; }
}

extern "C" BRUNONNUE_API void __cdecl NNUE_CtxDestroy(void* ctx)
{
    try { Stockfish::Probe::ctx_detruire(static_cast<Stockfish::Probe::Contexte*>(ctx)); }
    catch (...) { std::cerr << "[NNUE EXCEPTION] NNUE_CtxDestroy" << std::endl; }
}

extern "C" BRUNONNUE_API int __cdecl NNUE_CtxSet(void* ctx, const int* pieceBoard, int side, int rule50)
{
    if (!ctx || !pieceBoard)
        return 0;
    try { return Stockfish::Probe::ctx_definir(static_cast<Stockfish::Probe::Contexte*>(ctx), pieceBoard, side == 0, rule50) ? 1 : 0; }
    catch (...) { std::cerr << "[NNUE EXCEPTION] NNUE_CtxSet" << std::endl; return 0; }
}

extern "C" BRUNONNUE_API int __cdecl NNUE_CtxDo(void* ctx, int from, int to, int promo)
{
    if (!ctx || from < 0 || from > 63 || to < 0 || to > 63)
        return 0;
    try { return Stockfish::Probe::ctx_jouer(static_cast<Stockfish::Probe::Contexte*>(ctx), from, to, promo) ? 1 : 0; }
    catch (...) { std::cerr << "[NNUE EXCEPTION] NNUE_CtxDo" << std::endl; return 0; }
}

extern "C" BRUNONNUE_API int __cdecl NNUE_CtxDoNull(void* ctx)
{
    if (!ctx)
        return 0;
    try { return Stockfish::Probe::ctx_jouer_nul(static_cast<Stockfish::Probe::Contexte*>(ctx)) ? 1 : 0; }
    catch (...) { std::cerr << "[NNUE EXCEPTION] NNUE_CtxDoNull" << std::endl; return 0; }
}

extern "C" BRUNONNUE_API void __cdecl NNUE_CtxUndo(void* ctx)
{
    if (!ctx)
        return;
    try { Stockfish::Probe::ctx_annuler(static_cast<Stockfish::Probe::Contexte*>(ctx)); }
    catch (...) { std::cerr << "[NNUE EXCEPTION] NNUE_CtxUndo" << std::endl; }
}

extern "C" BRUNONNUE_API int __cdecl NNUE_CtxEval(void* ctx, int side, unsigned long long occupation, int optimism, int* valeur)
{
    if (!ctx || !valeur)
        return 0;
    try { return Stockfish::Probe::ctx_evaluer(static_cast<Stockfish::Probe::Contexte*>(ctx), optimism, occupation, side == 0, *valeur) ? 1 : 0; }
    catch (...) { std::cerr << "[NNUE EXCEPTION] NNUE_CtxEval" << std::endl; return 0; }
}