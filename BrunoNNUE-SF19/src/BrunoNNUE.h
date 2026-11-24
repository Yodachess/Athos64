#pragma once

#ifdef BRUNONNUE_EXPORTS
#define BRUNONNUE_API __declspec(dllexport)
#else
#define BRUNONNUE_API __declspec(dllimport)
#endif

extern "C" {
    BRUNONNUE_API int __cdecl NNUE_Init(const char* netFile);
    BRUNONNUE_API int __cdecl NNUE_EvalFEN(const char* fen);
    BRUNONNUE_API int __cdecl NNUE_EvalBoard(const int* pieceBoard, int side, int rule50, int optimism);
    BRUNONNUE_API int __cdecl NNUE_EvalPieces(const int* pieces, const int* squares, int pieceAmount, int side, int rule50);

    // Contexte incrémental : une position et des accumulateurs par thread de recherche.
    // side : 0 = les blancs jouent, 1 = les noirs. promo : 0 ou type de pièce Stockfish (2 cavalier, 3 fou, 4 tour, 5 dame).
    BRUNONNUE_API void* __cdecl NNUE_CtxCreate();
    BRUNONNUE_API void  __cdecl NNUE_CtxDestroy(void* ctx);
    BRUNONNUE_API int   __cdecl NNUE_CtxSet(void* ctx, const int* pieceBoard, int side, int rule50);
    BRUNONNUE_API int   __cdecl NNUE_CtxDo(void* ctx, int from, int to, int promo);
    BRUNONNUE_API int   __cdecl NNUE_CtxDoNull(void* ctx);
    BRUNONNUE_API void  __cdecl NNUE_CtxUndo(void* ctx);
    // Retourne 1 et remplit *valeur si le contexte correspond à la position (occupation, trait), sinon 0.
    BRUNONNUE_API int   __cdecl NNUE_CtxEval(void* ctx, int side, unsigned long long occupation, int optimism, int* valeur);
}
