using System.Text.Json.Serialization;

namespace GroverInQiskit.ViewModels
{
    public class GroverApiResultViewModel
    {
        [JsonPropertyName("backend_name")]
        public string BackendName { get; set; } = string.Empty;


        [JsonPropertyName("shots")] 
        public int Shots { get; set; } 


        [JsonPropertyName("iterations")] 
        public int? Iterations { get; set; }


        [JsonPropertyName("counts")] 
        public Dictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();


        [JsonPropertyName("histogram")] 
        public string Histogram { get; set; } = string.Empty;
    }
}
