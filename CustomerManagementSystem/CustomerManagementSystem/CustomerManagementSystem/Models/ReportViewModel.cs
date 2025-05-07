using System;
using System.Collections.Generic;

namespace CustomerManagementSystem.Models
{
    public class ReportViewModel
    {
        public List<Customer> Customers { get; set; }
        public string ReportTitle { get; set; }
        public DateTime GeneratedDate { get; set; }
    }
}