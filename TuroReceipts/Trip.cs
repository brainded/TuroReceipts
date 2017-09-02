namespace TuroReceipts
{
    class Trip
    {
        public string ReceiptUrl { get; set; }

        public string PickupDateTime { get; set; }

        public string DropoffDateTime { get; set; }

        public decimal Cost { get; set; }

        public decimal TuroFees { get; set; }

        public decimal Earnings { get; set; }

        public decimal ReimbursementTolls { get; set; }

        public decimal ReimbursementMileage { get; set; }
    }
}
