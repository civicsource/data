using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;

namespace Archon.Data
{
	public class Database
	{
		static readonly Regex goEx = new Regex(@"^\s*go\s*$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

		readonly string connectionString;
		readonly Assembly ass;
		readonly string[] createSql;
		readonly string[] clearSql;

		public string ConnectionString => connectionString;

		public Database(string connectionString, Type scriptType)
		{
			if (String.IsNullOrWhiteSpace(connectionString))
				throw new ArgumentNullException(nameof(connectionString));

			if (scriptType == null)
				throw new ArgumentNullException(nameof(scriptType));

			this.connectionString = connectionString;
			ass = scriptType.GetTypeInfo().Assembly;
			createSql = ParseScript(ReadScript(scriptType.Namespace, "create"));
			clearSql = ParseScript(ReadScript(scriptType.Namespace, "clear"));
		}

		public Database(string connectionString, Assembly scriptAss, string scriptNamespace)
		{
			if (String.IsNullOrWhiteSpace(connectionString))
				throw new ArgumentNullException(nameof(connectionString));

			if (scriptAss == null)
				throw new ArgumentNullException(nameof(scriptAss));

			if (String.IsNullOrWhiteSpace(scriptNamespace))
				throw new ArgumentNullException(nameof(scriptNamespace));

			this.connectionString = connectionString;
			ass = scriptAss;
			createSql = ParseScript(ReadScript(scriptNamespace, "create"));
			clearSql = ParseScript(ReadScript(scriptNamespace, "clear"));
		}

		string ReadScript(string @namespace, string name)
		{
			string resourceName = $"{@namespace}.{name}.sql";

			using (var str = ass.GetManifestResourceStream(resourceName))
			{
				if (str == null)
				{
					string availableResources = ass.GetManifestResourceNames().Aggregate(new StringBuilder(), (sb, r) => sb.AppendLine(r)).ToString();
					throw new DatabaseScriptException($"Could not find embedded resource '{resourceName}'. Available resources in assembly: {availableResources}");
				}

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

		public bool Exists()
		{
			var builder = new SqlConnectionStringBuilder(connectionString);

			string database = builder.InitialCatalog;
			builder.InitialCatalog = "";

			int count;
			using (var conn = new SqlConnection(builder.ToString()))
			{
				count = conn.Query<int>("select count(*) from sysdatabases where [Name] = @database", new { database }).SingleOrDefault();
			}

			return count > 0;
		}

		public void Rebuild()
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

			Build();
		}

		public void Build()
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

		public void Clear()
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