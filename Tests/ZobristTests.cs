// Clé Zobrist : la clé tenue à jour coup par coup doit toujours être égale à la clé recalculée de zéro,
// et un coup joué puis annulé doit laisser la position et sa clé inchangées.

using Athos64;
using Xunit;

namespace Athos64.Tests
{
    public class ZobristTests
    {
        [Theory]
        [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1")]
        [InlineData("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1")]     // Kiwipete : roques, prises
        [InlineData("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1")]         // Promotions
        [InlineData("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1")]                                 // Prise en passant possible
        public void La_cle_incrementale_reste_egale_a_la_cle_recalculee(string fen)
        {
            var e = new Echiquier();
            ChargementFen.ChargerFen(e, fen, false);
            e.CleActuelle = e.CalculerCleComplete();

            Parcourir(e, profondeur: 3);
        }

        private static void Parcourir(Echiquier e, int profondeur)
        {
            if (profondeur == 0)
                return;

            var coups = new List<Echiquier.Mouvement>(100);
            e.GenererMouvementsLegauxCommun(coups);

            foreach (var coup in coups)
            {
                ulong cleAvant = e.CleActuelle;
                var etat = e.SauvegarderEtat();

                e.JouerCoup(coup.CaseDepart, coup.CaseArrivee, coup.Promotion);
                Assert.True(e.VerifierIntegriteZobrist(),
                    $"Clé Zobrist incorrecte après {e.TraduireEnNotationAlgebraique(coup)}");
                Parcourir(e, profondeur - 1);

                e.RestaurerEtat(etat);
                Assert.Equal(cleAvant, e.CleActuelle);
                Assert.True(e.VerifierIntegriteZobrist(), "Clé Zobrist incorrecte après le retour en arrière");
            }
        }
    }
}
