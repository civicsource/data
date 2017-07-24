using System.Data;

namespace Archon.Data
{
	public class DataContext : IDbConnection
	{
		readonly IDbConnection inner;
		IDbTransaction currentTx;

		public IDbTransaction CurrentTransaction
		{
			get { return currentTx; }
		}

		public DataContext(IDbConnection inner)
		{
			this.inner = inner;
		}

		public IDbTransaction BeginTransaction()
		{
			inner.EnsureOpen();
			currentTx = inner.BeginTransaction();
			return currentTx;
		}

		public IDbTransaction BeginTransaction(IsolationLevel il)
		{
			inner.EnsureOpen();
			currentTx = inner.BeginTransaction(il);
			return currentTx;
		}

		public IDbCommand CreateCommand()
		{
			var cmd = inner.CreateCommand();
			cmd.Transaction = currentTx;
			return cmd;
		}

		#region Decorated

		public string ConnectionString
		{
			get { return inner.ConnectionString; }
			set { inner.ConnectionString = value; }
		}

		public int ConnectionTimeout
		{
			get { return inner.ConnectionTimeout; }
		}

		public string Database
		{
			get { return inner.Database; }
		}

		public ConnectionState State
		{
			get { return inner.State; }
		}

		public void ChangeDatabase(string databaseName)
		{
			inner.ChangeDatabase(databaseName);
		}

		public void Close()
		{
			inner.Close();
		}

		public void Dispose()
		{
			inner.Dispose();
		}

		public void Open()
		{
			inner.Open();
		}

		#endregion
	}
}