using System.Data;
using System.Data.Common;

namespace Archon.Data
{
	public class DataContext : DbConnection
	{
		readonly DbConnection inner;

		public DbTransaction CurrentTransaction { get; private set; }

		public DataContext(DbConnection inner)
		{
			this.inner = inner;
		}

		protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
		{
			inner.EnsureOpen();
			CurrentTransaction = inner.BeginTransaction(isolationLevel);
			return CurrentTransaction;
		}

		protected override DbCommand CreateDbCommand()
		{
			var cmd = inner.CreateCommand();
			cmd.Transaction = CurrentTransaction;
			return cmd;
		}

		#region Decorated

		public override string ConnectionString
		{
			get => inner.ConnectionString;
			set => inner.ConnectionString = value;
		}

		public override string Database => inner.Database;
		public override string DataSource => inner.DataSource;
		public override string ServerVersion => inner.ServerVersion;
		public override ConnectionState State => inner.State;

		public override void ChangeDatabase(string databaseName)
		{
			inner.ChangeDatabase(databaseName);
		}
		public override void Open()
		{
			inner.Open();
		}

		public override void Close()
		{
			inner.Close();
		}

		#endregion
	}
}