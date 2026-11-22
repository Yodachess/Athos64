// Règles de nulle vues par la recherche : triple répétition et règle des 50 coups

using Athos64;
using Xunit;

namespace Athos64.Tests
{
    public class RegleTests
    {
        [Fact]
        public void Triple_repetition_donne_nulle()
        {
            var e = new Echiquier();
            ChargementFen.ChargerFen(e, Echiquier.FenDepart, true);
            // Allers-retours de cavaliers : la position initiale revient pour la troisième fois après Ng1
            e.JouerCoup(6, 21);     // 1. Nf3
            e.JouerCoup(62, 45);    // 1... Nf6
            e.JouerCoup(21, 6);     // 2. Ng1
            e.JouerCoup(45, 62);    // 2... Ng8
            e.JouerCoup(6, 21);     // 3. Nf3
            e.JouerCoup(62, 45);    // 3... Nf6
            var etatPrecedent = e.JouerCoup(21, 6);     // 4. Ng1

            int score = Recherche.Negamax(e, profondeur: 2, distanceRacine: 1, alpha: -10000, beta: 10000);
            e.AnnulerCoup(etatPrecedent);

            Assert.Equal(0, score);
        }

        [Fact]
        public void Cinquante_coups_donne_nulle()
        {
            var e = new Echiquier();
            // Compteur à 99 demi-coups : le coup du roi le fait passer à 100
            ChargementFen.ChargerFen(e, "8/8/8/8/4k3/8/8/4K3 w - - 99 75", true);
            Assert.Equal(99, e.RegleDes50Coups);

            var etatPrecedent = e.JouerCoup(4, 5);      // Kf1
            int score = Recherche.Negamax(e, 2, 1, -10000, 10000);
            e.AnnulerCoup(etatPrecedent);

            Assert.Equal(0, score);
        }
    }
}
