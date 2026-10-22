using TileMatch.Model;

namespace TileMatch.Generation
{
    public sealed class GenerationRequest
    {
        public string Id;
        public TileMatchLevel Layout;
        public AssignerSettings Assigner = new AssignerSettings();
        public DifficultyBand? TargetBand;
        public ulong Seed = 1;
        public int MaxAttempts = 200;
    }

    public sealed class GenerationResult
    {
        public TileMatchLevel Level;
        public Assignment Assignment;
        public DifficultyMetrics Metrics;
        public DifficultyBand Band;
        public int Attempts;
        public ulong Seed;
        public bool HitTarget;
    }

    // Assign kinds, verify with the solver, measure, and retry with the next seed until the band matches.
    public sealed class LevelGenerator
    {
        private readonly TileMatchRules _rules;
        private readonly DifficultyBands _bands;
        private readonly AnalyzerSettings _analyzer;
        private readonly KindAssigner _assigner = new KindAssigner();

        public LevelGenerator(TileMatchRules rules, DifficultyBands bands, AnalyzerSettings analyzer)
        {
            _rules = rules;
            _bands = bands;
            _analyzer = analyzer;
        }

        public GenerationResult Generate(GenerationRequest request)
        {
            GenerationResult fallback = null;
            for (var attempt = 0; attempt < request.MaxAttempts; attempt++)
            {
                var seed = request.Seed + (ulong)attempt;
                var assignment = _assigner.TryAssign(request.Layout, request.Assigner, seed);
                if (assignment == null) continue;

                var level = request.Layout.WithKinds(request.Id, assignment.Kinds, request.Assigner.KindCount);
                var metrics = DifficultyAnalyzer.Measure(_rules, level, _analyzer);
                if (!metrics.Solvable && !metrics.SolverExhausted) continue;

                var result = new GenerationResult
                {
                    Level = level, Assignment = assignment, Metrics = metrics, Band = _bands.Classify(metrics),
                    Attempts = attempt + 1, Seed = seed,
                };
                result.HitTarget = request.TargetBand == null || result.Band == request.TargetBand;
                if (result.HitTarget) return result;
                if (fallback == null || Distance(result.Band, request.TargetBand.Value) < Distance(fallback.Band, request.TargetBand.Value))
                    fallback = result;
            }

            return fallback;
        }

        private static int Distance(DifficultyBand a, DifficultyBand b) => a > b ? a - b : b - a;
    }
}
