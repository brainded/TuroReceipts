using System;
using System.Collections.Generic;

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

        public DateTime PickupDate { get; set; }

        public DateTime DropoffDate { get; set; }

        public decimal Cost { get; set; }

        public decimal TuroFees { get; set; }

        public decimal Earnings { get; set; }

        public decimal ReimbursementTolls { get; set; }

        public decimal ReimbursementMileage { get; set; }

        public string Error { get; set; }

        public bool CanSplit()
        {
            if (PickupDate.Month == DropoffDate.Month && 
                PickupDate.Year == DropoffDate.Year) return false;
            return true;
        }

        public List<Trip> Split()
        {
            //Example: Pickup 8/29, Dropoff 10/3
            //August has 3 days
            //September has 30 days
            //October has 3 days
            //Total 36 days
            //Earnings = 1296
            //Per day earnings = 36

            var timeSpan = this.DropoffDate - this.PickupDate;

            var costPerDay = this.Cost / timeSpan.Days;
            var turoFeesPerDay = this.TuroFees / timeSpan.Days;
            var earningsPerDay = this.Earnings / timeSpan.Days;

            var trips = new List<Trip>();

            //add the pickup month trip
            var pickupMonthTrip = this.CopyDetails();
            pickupMonthTrip.PickupDate = this.PickupDate; //Example: 8/29
            pickupMonthTrip.DropoffDate = new DateTime(this.PickupDate.Year, this.PickupDate.Month + 1, 1).AddDays(-1); //Example: 9/1 - 1 day = 8/31
            var pickupMonthDays = (pickupMonthTrip.DropoffDate - pickupMonthTrip.PickupDate).Days;
            pickupMonthTrip.Cost = costPerDay * pickupMonthDays;
            pickupMonthTrip.TuroFees = turoFeesPerDay * pickupMonthDays;
            pickupMonthTrip.Earnings = earningsPerDay * pickupMonthDays;
            trips.Add(pickupMonthTrip);

            var startMonth = new DateTime(this.PickupDate.Year, this.PickupDate.Month, 1).AddMonths(1); //Example: 8/1 + 1 month = 9/1
            var endMonth = new DateTime(this.DropoffDate.Year, this.DropoffDate.Month, 1).AddMonths(-1); //Example: 10/1 - 1 month = 9/1

            var months = new List<DateTime>();
            var currentMonth = startMonth;
            while(currentMonth < endMonth)
            {
                months.Add(currentMonth);
                currentMonth = currentMonth.AddMonths(1);
            }

            foreach(var month in months)
            {
                var trip = this.CopyDetails();
                trip.PickupDate = month;
                trip.DropoffDate = month.AddMonths(1).AddDays(-1);
                var monthDays = trip.DropoffDate.Day;
                trip.Cost = costPerDay * monthDays;
                trip.TuroFees = turoFeesPerDay * monthDays;
                trip.Earnings = earningsPerDay * monthDays;
                trips.Add(trip);
            }

            //add the pickup month trip
            var dropoffMonthTrip = this.CopyDetails();
            dropoffMonthTrip.PickupDate = new DateTime(this.DropoffDate.Year, this.DropoffDate.Month, 1);
            dropoffMonthTrip.DropoffDate = this.DropoffDate;
            var dropoffMonthDays = this.DropoffDate.Day;
            dropoffMonthTrip.Cost = costPerDay * dropoffMonthDays;
            dropoffMonthTrip.TuroFees = turoFeesPerDay * dropoffMonthDays;
            dropoffMonthTrip.Earnings = earningsPerDay * dropoffMonthDays;
            trips.Add(dropoffMonthTrip);

            return trips;
        }

        public Trip CopyDetails()
        {
            return new Trip()
            {
                ReservationId = this.ReservationId,
                TripUrl = this.TripUrl,
                ReceiptUrl = this.ReceiptUrl,
                CarUrl = this.CarUrl,
                CarId = this.CarId,
                Car = this.Car,
                Status = this.Status
            };
        }
    }
}
