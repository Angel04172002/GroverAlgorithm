namespace GroverInQiskit.ViewModels
{
    public class DashboardViewModel
    {
        public List<GroverResultViewModel> Results { get; set; } = new List<GroverResultViewModel>();
        public List<string> ErrorMessages { get; set; } = new List<string>();
    }
}
