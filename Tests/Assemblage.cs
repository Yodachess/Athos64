// Le moteur garde de l'état statique (table de transposition, compteurs de la recherche) :
// les tests ne s'exécutent donc jamais en parallèle.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
