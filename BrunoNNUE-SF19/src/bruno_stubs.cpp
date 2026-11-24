#include "position.h"
#include "tt.h"
#include "uci.h"

namespace Stockfish
{
    namespace Tablebases
    {
        int MaxCardinality = 0;

        WDLScore probe_wdl(Position&, ProbeState*)
        {
            return WDLScore(0);
        }

        int probe_dtz(Position&, ProbeState*)
        {
            return 0;
        }
    }

    TTEntry* TranspositionTable::first_entry(const Key) const
    {
        return nullptr;
    }

    std::string UCIEngine::square(Square s)
    {
        return std::string(1, char('a' + file_of(s)))
             + std::string(1, char('1' + rank_of(s)));
    }

    int UCIEngine::to_cp(Value v, const Position&)
    {
        return int(v);
    }
}