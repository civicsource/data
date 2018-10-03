using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace Archon.Data
{
	public static class ConnectionExtensions
	{
		public static void EnsureOpen(this IDbConnection conn)
		{
			if (conn.State != ConnectionState.Open)
				conn.Open();
		}

		public static async Task EnsureOpenAsync(this DbConnection conn)
		{
			if (conn.State != ConnectionState.Open)
				await conn.OpenAsync();
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

		public static async Task<DbTransaction> EnsureTransactionAsync(this DbConnection conn)
		{
			await conn.EnsureOpenAsync();
			return conn.BeginTransaction();
		}

		public static async Task<DbTransaction> EnsureTransactionAsync(this DbConnection conn, IsolationLevel isolationLevel)
		{
			await conn.EnsureOpenAsync();
			return conn.BeginTransaction(isolationLevel);
		}
	}
}