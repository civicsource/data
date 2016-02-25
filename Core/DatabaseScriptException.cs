using System;

namespace Archon.Data
{
	public class DatabaseScriptException : Exception
	{
		public DatabaseScriptException(string message)
			: base(message) { }
	}
}