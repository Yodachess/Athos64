// Static Exchange Evaluation : gain ou perte matérielle d'une suite de captures sur une case

using Athos64;
using Xunit;

namespace Athos64.Tests
{
    public class SeeTests
    {
        // Chaque ligne : la FEN, la case cible, la valeur de la victime, celle de l'attaquant et le SEE attendu
        [Theory]
        [InlineData("r1bxk2r/pppp1ppp/2n2n2/4p3/4P3/2N2N2/PPPP1PPP/R1BQKB1R w KQkq - 0 1", 36, 100, 320, -220)]    // Capture d'un pion défendu par un cavalier
        [InlineData("rnbqkbnr/ppp1pppp/8/3p4/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2", 35, 100, 100, 0)]               // Échange de pions
        [InlineData("7k/8/8/3r4/4P3/8/3R4/3R3K w - - 0 1", 35, 500, 100, 500)]                                      // Rayons X : le pion libère deux tours
        [InlineData("2r2rk1/pp2q1pp/2n1p2b/2p1p3/R3P3/1Q1P1NP1/PP3PBP/3R2K1 w - - 0 1", 36, 100, 320, -220)]       // Batterie lourde
        public void See_donne_la_valeur_attendue(string fen, int caseCible, int valeurVictime, int valeurAttaquant, int attendu)
        {
            var e = new Echiquier();
            ChargementFen.ChargerFen(e, fen, false);

            Assert.Equal(attendu, EchangeStatiqueEval.ObtenirSEE(e, caseCible, valeurVictime, valeurAttaquant));
        }
    }
}
