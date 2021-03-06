﻿using System;
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

		private readonly string connectionString;
		private readonly int? commandTimeout;
		private readonly Assembly ass;
		private readonly string[] createSql;
		private readonly string[] clearSql;

		public string ConnectionString => connectionString;

		public Database(string connectionString, Type scriptType, int? commandTimeout = null)
		{
			if (String.IsNullOrWhiteSpace(connectionString))
				throw new ArgumentNullException(nameof(connectionString));

			if (scriptType == null)
				throw new ArgumentNullException(nameof(scriptType));

			this.connectionString = connectionString;
			this.commandTimeout = commandTimeout;
			ass = scriptType.GetTypeInfo().Assembly;
			createSql = ParseScript(ReadScript(scriptType.Namespace, "create"));
			clearSql = ParseScript(ReadScript(scriptType.Namespace, "clear"));
		}

		public Database(string connectionString, Assembly scriptAss, string scriptNamespace, int? commandTimeout = null)
		{
			if (String.IsNullOrWhiteSpace(connectionString))
				throw new ArgumentNullException(nameof(connectionString));

			if (scriptAss == null)
				throw new ArgumentNullException(nameof(scriptAss));

			if (String.IsNullOrWhiteSpace(scriptNamespace))
				throw new ArgumentNullException(nameof(scriptNamespace));

			this.connectionString = connectionString;
			ass = scriptAss;
			this.commandTimeout = commandTimeout;
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

			using (var conn = new SqlConnection(builder.ToString()))
			{
				return await conn.ExecuteScalarAsync<bool>("SELECT TOP 1 1 FROM sys.sysdatabases WHERE [Name] = @database", new { database },
					commandTimeout: commandTimeout);
			}
		}

		public Task RebuildAsync() => RebuildAsync(null);

		public async Task RebuildAsync(Func<string, string> modifyScript)
		{
			await DropAsync();
			await BuildAsync(modifyScript);
		}

		public Task BuildAsync() => BuildAsync(null);

		public async Task BuildAsync(Func<string, string> modifyScript)
		{
			var builder = new SqlConnectionStringBuilder(connectionString);

			string database = builder.InitialCatalog;
			builder.InitialCatalog = "";

			bool isAzure = false;
			using (var conn = new SqlConnection(builder.ToString()))
			{
				isAzure = await IsAzure(conn);

				await conn.ExecuteAsync($"IF NOT EXISTS (SELECT 1 FROM sys.sysdatabases WHERE name = '{database}') CREATE DATABASE [{database}]", commandTimeout: commandTimeout);
			}

			if (isAzure)
			{
				// wait for the "azure juices" to settle after creating the database
				// the newly created database is not always immediately available to connect to
				await Task.Delay(2000);
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

		public async Task DropAsync()
		{
			var builder = new SqlConnectionStringBuilder(connectionString);

			string database = builder.InitialCatalog;
			builder.InitialCatalog = "";

			using (var conn = new SqlConnection(builder.ToString()))
			{
				bool isAzure = await IsAzure(conn);

				if (isAzure)
				{
					await conn.ExecuteAsync($@"DROP DATABASE IF EXISTS [{database}]", commandTimeout: commandTimeout);
				}
				else
				{
					await conn.ExecuteAsync($@"
						IF EXISTS (SELECT 1 FROM sys.sysdatabases WHERE name = '{database}')
						BEGIN
							ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
							DROP DATABASE [{database}];
						END", commandTimeout: commandTimeout);
				}
			}
		}

		async Task ExecuteScriptAsync(DbConnection conn, string sql, Func<string, string> modify, IDbTransaction tx = null)
		{
			if (modify != null)
			{
				string newStatement = modify(sql);
				if (!String.IsNullOrWhiteSpace(newStatement))
				{
					await conn.ExecuteAsync(newStatement, transaction: tx, commandTimeout: commandTimeout);
				}
			}
			else
			{
				await conn.ExecuteAsync(sql, transaction: tx, commandTimeout: commandTimeout);
			}
		}

		Task<bool> IsAzure(DbConnection conn) => conn.ExecuteScalarAsync<bool>("SELECT CASE WHEN SERVERPROPERTY ('edition') = N'SQL Azure' THEN 1 END", commandTimeout: commandTimeout);
	}
}
