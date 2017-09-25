using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Dapper;

namespace TuroReceipts
{
    public class TripRepository : IRepository<Trip>
    {
        #region Context Property
        SQLiteConnection context;
        protected SQLiteConnection Context
        {
            get
            {
                return context;
            }
            set
            {
                context = value;
            }
        }
        #endregion

        #region Constructor
        public TripRepository()
        {
            Context = Database.DbConnection();
        }
        #endregion

        #region Generic Repository
        public Trip Insert(Trip model)
        {
            var result = Context.Execute(
                @"insert into Trips (ReservationId, TripUrl, ReceiptUrl, Status, CarId, PickupDate, DropoffDate, Cost, TuroFees, Earnings, ReimbursementTotal, Error) 
                values (@ReservationId, @TripUrl, @ReceiptUrl, @Status, @CarId, @PickupDate, @DropoffDate, @Cost, @TuroFees, @Earnings, @ReimbursementTotal, @Error)", 
                model);

            if (result == 0) return null;
            return model;
        }

        public Trip Update(Trip model)
        {
            var result = Context.Execute(
                @"update Trips set ReservationId = @ReservationId, TripUrl = @TripUrl, ReceiptUrl = @ReceiptUrl, Status = @Status, CarId = @CarId, PickupDate = @PickupDate, DropoffDate = @DropoffDate, Cost = @Cost, TuroFees = @TuroFees, Earnings = @Earnings, ReimbursementTotal = @ReimbursementTotal, Error = @Error
                where ReservationId = @ReservationId", 
                model);

            if (result == 0) return null;
            return model;
        }

        public bool Delete(Trip model)
        {
            var command = Context.Execute(@"delete from Trips where ReservationId = @ReservationId", new { Id = model.ReservationId });
            return command.Equals(1);
        }

        public bool Exists(int id)
        {
            return Context.Query<int>(@"select ReservationId from Trips where ReservationId = @Id", new { Id = id }).Any();
        }

        public Trip Select(int id)
        {
            return Context.Query<Trip>(@"select * from Trips where ReservationId = @Id", new { Id = id }).FirstOrDefault();
        }

        public IEnumerable<Trip> SelectAll()
        {
            return Context.Query<Trip>(@"select * from Trips");
        }

        #endregion

        #region IDispose Region
        private bool disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    context.Dispose();
                }
            }
            this.disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
