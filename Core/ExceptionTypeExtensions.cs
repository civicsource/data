using System;
using System.Data.SqlClient;

namespace Archon.Data
{
	public static class ExceptionTypeExtensions
	{
		public static bool IsSqlDuplicateKeyException(this Exception ex)
		{
			return IsDuplicateKeyException(ex as SqlException);
		}

		public static bool IsDuplicateKeyException(this SqlException ex)
		{
			if (ex == null)
				return false;

			return ex.Number == 2627;
		}
	}
}