using Xunit;

// Config.DataDir est un état statique partagé (chemin de données courant) -
// des classes de test tournant en parallèle se marcheraient dessus. Chaque
// classe de test reste isolée sur son propre dossier temporaire (voir
// SessionTests), mais l'exécution doit rester séquentielle entre classes.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
