namespace CustomerManagementSystem.Models
{
    public class DashboardViewModel
    {
        public int TotalCustomers { get; set; }
        public int ActiveCustomers { get; set; }
        public int InactiveCustomers { get; set; }
        public List<Customer> RecentCustomers { get; set; } = new List<Customer>();
        public List<string> RegistrationDates { get; set; } = new List<string>();
        public List<int> CustomersCount { get; set; } = new List<int>();
    }
}
