namespace elite.Models
{
    public class Promotion
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public string DiscountType { get; set; } // Percentage/Fixed
        public decimal DiscountValue { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int UsageLimit { get; set; }
        public int TimesUsed { get; set; }
    }
}
