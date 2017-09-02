namespace TuroReceipts
{
    class Trip
    {
        public string ReservationId { get; set; }

        public string TripUrl { get; set; }

        public string ReceiptUrl { get; set; }

        public string CarUrl { get; set; }

        public string CarId { get; set; }

        public string Car { get; set; }

        public string Status { get; set; }

        public string PickupDateTime { get; set; }

        public string DropoffDateTime { get; set; }

        public decimal Cost { get; set; }

        public decimal TuroFees { get; set; }

        public decimal Earnings { get; set; }

        public decimal ReimbursementTolls { get; set; }

        public decimal ReimbursementMileage { get; set; }
    }
}
