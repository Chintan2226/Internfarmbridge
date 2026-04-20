namespace API.Models.Vendor
{
    public class VM_MonthlyTrends
    {
        public List<VM_MonthlyData> MonthlyData { get; set; } = new();
    }

    public class VM_MonthlyData
    {
        public string Month { get; set; }
        public decimal Amount { get; set; }
    }

    public class VM_CategorySpending
    {
        public string Category { get; set; }
        public decimal TotalSpent { get; set; }
        public int OrderCount { get; set; }
    }
}