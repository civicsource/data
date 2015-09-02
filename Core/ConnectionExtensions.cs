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
	}
}