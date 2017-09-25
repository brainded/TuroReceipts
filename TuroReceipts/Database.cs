using System;
using System.Data.SQLite;
using System.IO;
using Dapper;

namespace TuroReceipts
{
    public static class Database
    {
        public static void Init()
        {
            if (!File.Exists(DbFile))
            {
                Create();
            }
        }

        public static void Create()
        {
            using (var connection = DbConnection())
            {
                connection.Open();
                connection.Execute(
                @"create table Cars 
                (
                    CarId integer primary key,
                    MakeModel varchar(255) not null,
                    CarUrl varchar(255) not null
                )
                ");

                connection.Execute(
                @"create table Trips 
                (
                    ReservationId integer primary key,
                    TripUrl varchar(255) not null,
                    ReceiptUrl varchar(255) not null,
                    Status varchar(255) not null,
                    CarId integer not null,
                    PickupDate datetime not null,
                    DropoffDate datetime not null,
                    Cost real not null,
                    TuroFees real not null,
                    Earnings real not null,
                    ReimbursementTotal real not null,
                    Error varchar(255)
                )
                ");
            }
        }

        public static SQLiteConnection DbConnection()
        {
            return new SQLiteConnection(string.Format("Data Source={0}", DbFile));
        }

        public static string DbFile
        {
            get
            {
                return Environment.CurrentDirectory + "\\TuroReceipts.sqlite";
            }
        }
    }
}
