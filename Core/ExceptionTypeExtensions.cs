using System;
using System.Data.SqlClient;

namespace Archon.Data
{
	public static class ExceptionTypeExtensions
	{
		public static bool IsDuplicateKeyException(this SqlException ex)
		{
			if (ex == null)
				return false;

			if (ex.Message == null)
				return false;

			return ex.Message.Contains("duplicate key");
		}
	}
}