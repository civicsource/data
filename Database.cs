using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

		public async Task<bool> ExistsAsync()
		{
			var builder = new SqlConnectionStringBuilder(connectionString);

			string database = builder.InitialCatalog;
			builder.InitialCatalog = "";

			int count;
			using (var conn = new SqlConnection(builder.ToString()))
			{
				count = (await conn.QueryAsync<int>("select count(*) from sysdatabases where [Name] = @database", new { database })).SingleOrDefault();
			}

			return count > 0;
		}

		public Task RebuildAsync() => RebuildAsync(null);

		public async Task RebuildAsync(Func<string, string> modifyScript)
		{
			var builder = new SqlConnectionStringBuilder(connectionString);

			string database = builder.InitialCatalog;
			builder.InitialCatalog = "";

			using (var conn = new SqlConnection(builder.ToString()))
			{
				await conn.ExecuteAsync($@"
					if db_id('{database}') is not null
					begin
						alter database {database} set single_user with rollback immediate;
						drop database {database};
					end"
				);
			}

			await BuildAsync(modifyScript);
		}

		public Task BuildAsync() => BuildAsync(null);

		public async Task BuildAsync(Func<string, string> modifyScript)
		{
			var builder = new SqlConnectionStringBuilder(connectionString);

			string database = builder.InitialCatalog;
			builder.InitialCatalog = "";

			using (var conn = new SqlConnection(builder.ToString()))
			{
				await conn.ExecuteAsync($"if db_id('{database}') is null create database {database}");
			}

			using (var conn = new SqlConnection(connectionString))
			{
				await BuildSchemaAsync(conn, modifyScript);
			}
		}

		public Task BuildSchemaAsync(DbConnection conn) => BuildSchemaAsync(conn, null);

		public async Task BuildSchemaAsync(DbConnection conn, Func<string, string> modifyScript)
		{
			await conn.EnsureOpenAsync();

			foreach (string statement in createSql)
				await ExecuteScriptAsync(conn, statement, modifyScript);
		}

		public Task ClearAsync() => ClearAsync((Func<string, string>)null);

		public async Task ClearAsync(Func<string, string> modifyScript)
		{
			using (var conn = new SqlConnection(connectionString))
			{
				await ClearAsync(conn, modifyScript);
			}
		}

		public Task ClearAsync(DbConnection conn) => ClearAsync(conn, null);

		public async Task ClearAsync(DbConnection conn, Func<string, string> modifyScript)
		{
			await conn.EnsureOpenAsync();

			using (var tx = conn.BeginTransaction())
			{
				foreach (string statement in clearSql)
					await ExecuteScriptAsync(conn, statement, modifyScript, tx);

				tx.Commit();
			}
		}

		async Task ExecuteScriptAsync(DbConnection conn, string sql, Func<string, string> modify, IDbTransaction tx = null)
		{
			if (modify != null)
			{
				string newStatement = modify(sql);
				if (!String.IsNullOrWhiteSpace(newStatement))
				{
					await conn.ExecuteAsync(newStatement, transaction: tx);
				}
			}
			else
			{
				await conn.ExecuteAsync(sql, transaction: tx);
			}
		}
	}
}