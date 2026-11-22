// Perft : nombre de positions atteignables à une profondeur donnée, comparé aux valeurs de référence
// https://www.chessprogramming.org/Perft_Results
// Toute modification de la génération des coups doit garder ces tests verts.

using Athos64;
using Xunit;

namespace Athos64.Tests
{
    public class PerftTests
    {
        private const string PositionInitiale = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        private const string Kiwipete = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
        private const string Position3 = "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1";
        private const string Position4 = "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";
        private const string Position5 = "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8";
        private const string Position6 = "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10";

        // Chaque ligne : la position, la profondeur et le nombre de positions attendu
        [Theory]
        [InlineData(PositionInitiale, 1, 20UL)]
        [InlineData(PositionInitiale, 2, 400UL)]
        [InlineData(PositionInitiale, 3, 8902UL)]
        [InlineData(PositionInitiale, 4, 197281UL)]
        [InlineData(PositionInitiale, 5, 4865609UL)]
        [InlineData(Kiwipete, 1, 48UL)]
        [InlineData(Kiwipete, 2, 2039UL)]
        [InlineData(Kiwipete, 3, 97862UL)]
        [InlineData(Kiwipete, 4, 4085603UL)]
        [InlineData(Position3, 1, 14UL)]
        [InlineData(Position3, 2, 191UL)]
        [InlineData(Position3, 3, 2812UL)]
        [InlineData(Position3, 4, 43238UL)]
        [InlineData(Position3, 5, 674624UL)]
        [InlineData(Position4, 1, 6UL)]
        [InlineData(Position4, 2, 264UL)]
        [InlineData(Position4, 3, 9467UL)]
        [InlineData(Position4, 4, 422333UL)]
        [InlineData(Position5, 1, 44UL)]
        [InlineData(Position5, 2, 1486UL)]
        [InlineData(Position5, 3, 62379UL)]
        [InlineData(Position5, 4, 2103487UL)]
        [InlineData(Position6, 1, 46UL)]
        [InlineData(Position6, 2, 2079UL)]
        [InlineData(Position6, 3, 89890UL)]
        [InlineData(Position6, 4, 3894594UL)]
        public void Perft_donne_la_valeur_de_reference(string fen, int profondeur, ulong attendu)
        {
            var echiquier = new Echiquier();
            ChargementFen.ChargerFen(echiquier, fen, false);
            echiquier.CleActuelle = echiquier.CalculerCleComplete();

            Assert.Equal(attendu, echiquier.Perft(profondeur));
        }
    }
}
