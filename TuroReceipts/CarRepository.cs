using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Dapper;

namespace TuroReceipts
{
    public class CarRepository : IRepository<Car>
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
        public CarRepository()
        {
            Context = Database.DbConnection();
        }
        #endregion

        #region Generic Repository
        public Car Insert(Car model)
        {
            var result = Context.Execute(
                @"insert into Cars (CarId, MakeModel, CarUrl) 
                values (@CarId, @MakeModel, @CarUrl)",
                model);

            if (result == 0) return null;
            return model;
        }

        public Car Update(Car model)
        {
            var result = Context.Execute(
                @"update Cars set MakeModel = @MakeModel, CarUrl = @CarUrl
                where CarId = @CarId",
                model);

            if (result == 0) return null;
            return model;
        }

        public bool Delete(Car model)
        {
            var command = Context.Execute(@"delete from Cars where CarId = @CarId", new { Id = model.CarId });
            return command.Equals(1);
        }

        public bool Exists(int id)
        {
            return Context.Query<int>(@"select CarId from Cars where CarId = @Id", new { Id = id }).Any();
        }

        public Car Select(int id)
        {
            return Context.Query<Car>(@"select CarId, MakeModel, CarUrl from Cars where CarId = @Id", new { Id = id }).FirstOrDefault();
        }

        public IEnumerable<Car> SelectAll()
        {
            return Context.Query<Car>(@"select CarId, MakeModel, CarUrl from Cars");
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
