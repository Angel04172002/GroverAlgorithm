namespace GroverInQiskit.ViewModels
{
    public class GroverResultViewModel
    {
        public string MarkedStates { get; set; } = string.Empty;
        public string BackendName { get; set; } = string.Empty;
        public int Shots { get; set; }
        public int? Iterations { get; set; }
        public Dictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
        public string HistogramBase64 { get; set; } = string.Empty;
    }
}
