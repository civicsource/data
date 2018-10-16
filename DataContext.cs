using System;
using System.Data;
using System.Data.Common;

namespace Archon.Data
{
	public class DataContext : DbConnection
	{
		public DbConnection InnerConnection { get; }
		public DbTransaction CurrentTransaction { get; private set; }

		public DataContext(DbConnection inner)
		{
			InnerConnection = inner ?? throw new ArgumentNullException(nameof(inner));
		}

		protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
		{
			InnerConnection.EnsureOpen();
			CurrentTransaction = InnerConnection.BeginTransaction(isolationLevel);
			return CurrentTransaction;
		}

		protected override DbCommand CreateDbCommand()
		{
			var cmd = InnerConnection.CreateCommand();
			cmd.Transaction = CurrentTransaction;
			return cmd;
		}

		protected override void Dispose(bool disposing)
		{
			InnerConnection?.Dispose();
		}

		#region Decorated

		public override string ConnectionString
		{
			get => InnerConnection.ConnectionString;
			set => InnerConnection.ConnectionString = value;
		}

		public override string Database => InnerConnection.Database;
		public override string DataSource => InnerConnection.DataSource;
		public override string ServerVersion => InnerConnection.ServerVersion;
		public override ConnectionState State => InnerConnection.State;

		public override void ChangeDatabase(string databaseName)
		{
			InnerConnection.ChangeDatabase(databaseName);
		}
		public override void Open()
		{
			InnerConnection.Open();
		}

		public override void Close()
		{
			InnerConnection.Close();
		}

		#endregion
	}
}