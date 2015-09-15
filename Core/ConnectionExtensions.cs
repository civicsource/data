using System.Data;

namespace Archon.Data
{
	public static class ConnectionExtensions
	{
		public static void EnsureOpen(this IDbConnection conn)
		{
			if (conn.State != ConnectionState.Open)
				conn.Open();
		}

		public static IDbTransaction EnsureTransaction(this IDbConnection conn)
		{
			conn.EnsureOpen();
			return conn.BeginTransaction();
		}

		public static IDbTransaction EnsureTransaction(this IDbConnection conn, IsolationLevel isolationLevel)
		{
			conn.EnsureOpen();
			return conn.BeginTransaction(isolationLevel);
		}
	}
}