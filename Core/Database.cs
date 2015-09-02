using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Dapper;

namespace Archon.Data
{
	public class Database
	{
		readonly string[] createSql;
		readonly string[] clearSql;
		readonly Regex goEx = new Regex(@"^\s*go\s*$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

		public Database(string @namespace)
		{
			if (String.IsNullOrEmpty(@namespace))
				throw new ArgumentNullException(nameof(@namespace));

			createSql = ParseScript(ReadScript(@namespace, "create"));
			clearSql = ParseScript(ReadScript(@namespace, "clear"));
		}

		string ReadScript(string @namespace, string name)
		{
			using (var str = Assembly.GetExecutingAssembly().GetManifestResourceStream(String.Format("{0}.{1}.sql", @namespace, name)))
			{
				using (var reader = new StreamReader(str))
				{
					return reader.ReadToEnd();
				}
			}
		}

		string[] ParseScript(string script)
		{
			return goEx.Split(script).Where(c => !goEx.IsMatch(c) && !String.IsNullOrWhiteSpace(c)).ToArray();
		}

		public void Rebuild(string connectionString)
		{
			var builder = new SqlConnectionStringBuilder(connectionString);

			string database = builder.InitialCatalog;
			builder.InitialCatalog = "";

			using (var conn = new SqlConnection(builder.ToString()))
			{
				conn.Execute(String.Format(@"
					if db_id('{0}') is not null
					begin
						alter database {0} set single_user with rollback immediate;
						drop database {0};
					end", database
				));
			}

			Build(connectionString);
		}

		public void Build(string connectionString)
		{
			var builder = new SqlConnectionStringBuilder(connectionString);

			string database = builder.InitialCatalog;
			builder.InitialCatalog = "";

			using (var conn = new SqlConnection(builder.ToString()))
			{
				conn.Execute(String.Format("if db_id('{0}') is null create database {0}", database));
			}

			using (var conn = new SqlConnection(connectionString))
			{
				BuildSchema(conn);
			}
		}

		public void BuildSchema(IDbConnection conn)
		{
			conn.EnsureOpen();

			foreach (string statement in createSql)
				conn.Execute(statement);
		}

		public void Clear(string connectionString)
		{
			using (var conn = new SqlConnection(connectionString))
			{
				Clear(conn);
			}
		}

		public void Clear(IDbConnection conn)
		{
			conn.EnsureOpen();

			using (var tx = conn.BeginTransaction())
			{
				foreach (string statement in clearSql)
					conn.Execute(statement, transaction: tx);

				tx.Commit();
			}
		}
	}
}