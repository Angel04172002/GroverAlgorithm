using GroverInQiskit.ViewModels;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace GroverInQiskit.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IConfiguration _config;
        public DashboardController(IConfiguration config)
        {
            _config = config;
        }

        // GET: /Dashboard/Index
        public IActionResult Index()
        {
            return View(new DashboardViewModel());
        }

        // POST: /Dashboard/Index (handle CSV upload)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(DashboardViewModel model)
        {
            // The uploaded file is accessible via Request (it's not a property on DashboardViewModel)
            var file = Request.Form.Files.FirstOrDefault();

            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Please upload a valid CSV file.");
                return View(model);
            }

            var results = new List<GroverResultViewModel>();
            var errors = new List<string>();

            try
            {
                using (var stream = file.OpenReadStream())
                {
                    using (var reader = new System.IO.StreamReader(stream))
                    {
                        string headerLine = reader.ReadLine();
                        if (headerLine == null) throw new Exception("CSV file is empty");
                        // Determine column indices by header names (if headers are present)
                        string[] headers = headerLine.Split(',');
                        int idxMarked = Array.IndexOf(headers, "marked_states");
                        int idxShots = Array.IndexOf(headers, "shots");
                        int idxSimulator = Array.IndexOf(headers, "use_simulator");
                        int idxOptLevel = Array.IndexOf(headers, "optimization_level");
                        int idxNumIter = Array.IndexOf(headers, "num_iterations");
                        int idxHistogram = Array.IndexOf(headers, "return_histogram");

                        string line;
                        // Loop through each run specified in CSV
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue; // skip empty lines
                            string[] fields = line.Split(',');
                            // Parse marked_states (could be multiple values separated by ';')
                            List<string> markedStatesList = new List<string>();
                            if (idxMarked >= 0 && idxMarked < fields.Length)
                            {
                                string markedField = fields[idxMarked].Trim();
                                if (markedField.Contains(';'))
                                {
                                    markedStatesList.AddRange(markedField.Split(';', StringSplitOptions.RemoveEmptyEntries));
                                }
                                else if (!string.IsNullOrEmpty(markedField))
                                {
                                    markedStatesList.Add(markedField);
                                }
                            }
                            if (markedStatesList.Count == 0)
                            {
                                // Skip if no marked state provided
                                errors.Add("No marked_states specified for a run.");
                                continue;
                            }

                            // Build JSON payload for this run
                            var payload = new Dictionary<string, object>();
                            payload["marked_states"] = markedStatesList;
                            if (idxShots >= 0 && idxShots < fields.Length && !string.IsNullOrWhiteSpace(fields[idxShots]))
                            {
                                if (int.TryParse(fields[idxShots].Trim(), out int shots))
                                {
                                    payload["shots"] = shots;
                                }
                            }
                            if (idxSimulator >= 0 && idxSimulator < fields.Length && !string.IsNullOrWhiteSpace(fields[idxSimulator]))
                            {
                                string useSim = fields[idxSimulator].Trim().ToLower();
                                if (useSim == "true" || useSim == "1") payload["use_simulator"] = true;
                                else if (useSim == "false" || useSim == "0") payload["use_simulator"] = false;
                            }
                            if (idxOptLevel >= 0 && idxOptLevel < fields.Length && !string.IsNullOrWhiteSpace(fields[idxOptLevel]))
                            {
                                if (int.TryParse(fields[idxOptLevel].Trim(), out int optLevel))
                                {
                                    payload["optimization_level"] = optLevel;
                                }
                            }
                            if (idxNumIter >= 0 && idxNumIter < fields.Length && !string.IsNullOrWhiteSpace(fields[idxNumIter]))
                            {
                                if (int.TryParse(fields[idxNumIter].Trim(), out int numIter))
                                {
                                    payload["num_iterations"] = numIter;
                                }
                            }
                            // Always request histogram image unless explicitly disabled
                            bool returnHistogram = true;
                            if (idxHistogram >= 0 && idxHistogram < fields.Length && !string.IsNullOrWhiteSpace(fields[idxHistogram]))
                            {
                                string val = fields[idxHistogram].Trim().ToLower();
                                if (val == "false" || val == "0") returnHistogram = false;
                            }
                            payload["return_histogram"] = returnHistogram;

                            // Call the Python API for this run
                            string apiBase = _config["PythonApiBaseUrl"]?.TrimEnd('/') ?? "";
                            string url = apiBase + "/api/run-grover";
                            using (var client = new HttpClient())
                            {
                                client.Timeout = TimeSpan.FromMinutes(10);

                                var json = JsonSerializer.Serialize(payload);
                                var content = new StringContent(json, Encoding.UTF8, "application/json");
                                HttpResponseMessage response = await client.PostAsync(url, content);
                                if (!response.IsSuccessStatusCode)
                                {
                                    errors.Add($"API call failed (status {response.StatusCode}) for marked_states={string.Join(',', markedStatesList)}");
                                    continue;
                                }
                                string responseJson = await response.Content.ReadAsStringAsync();
                                // Deserialize JSON into our GroverApiResult model
                                var apiResult = JsonSerializer.Deserialize<GroverApiResultViewModel>(responseJson);
                                if (apiResult == null)
                                {
                                    errors.Add($"Invalid API response for marked_states={string.Join(',', markedStatesList)}");
                                    continue;
                                }
                                // Prepare result for UI
                                string markedStatesStr = string.Join(", ", markedStatesList);
                                string histBase64 = apiResult.Histogram ?? string.Empty;
                                // If histogram string has data URI prefix, strip it
                                int base64Index = histBase64.IndexOf("base64,");
                                if (base64Index != -1)
                                {
                                    histBase64 = histBase64.Substring(base64Index + 7);
                                }
                                var result = new GroverResultViewModel
                                {
                                    MarkedStates = markedStatesStr,
                                    BackendName = apiResult.BackendName,
                                    Shots = apiResult.Shots,
                                    Iterations = apiResult.Iterations,
                                    Counts = apiResult.Counts,
                                    HistogramBase64 = histBase64
                                };
                                results.Add(result);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Catch any unexpected errors (e.g., file read issues)
                errors.Add("Error processing file: " + ex.Message);
            }

            // Prepare final view model with results and any errors
            var viewModel = new DashboardViewModel
            {
                Results = results,
                ErrorMessages = errors
            };
            return View(viewModel);
        }
    }
}
