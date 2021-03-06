# Data Utils

> Utility classes to create databases and build schemas from sql scripts.

## How to Use

### Install

```
dotnet add package Archon.Data
```

### Create a Database Instance

You should have a `create.sql` & a `clear.sql` as embedded resources in your class library. The `create.sql` script should only create tables & schemas and whatnot. It shouldn't try to create the database. The `clear.sql` script should simply clear the tables. Don't drop or create tables in the `clear.sql` script.

```cs
string connectionString = "server=.\\sqlexpress;database=mydb;integrated security=true;";
var db = new Database(connectionString, typeof(MyType)); //where MyType is in the same namespace as your create.sql & clear.sql
```

or

```cs
string connectionString = "server=.\\sqlexpress;database=mydb;integrated security=true;";
var db = new Database(connectionString, typeof(MyType).Assembly, "MyAssembly.Weird.Namespace.Folder1"); //specify what namespace the embedded create & clear sql scripts are in
```

### Check if Database Exists

To check if a database exists:

```cs
bool exists = await db.ExistsAsync();
```

It will return `true` or `false` depending on if the database in the connection string exists or not.

### Build a Database

To build a new database using the `create.sql` script:

```cs
await db.BuildAsync();
```

This will create the database if it doesn't already exist and then run the `create.sql` script against it. You can also use the overload which takes a `modifyScript` delegate to modify each script before it is executed (e.g. to replace tokens based on environment):

```cs
await db.BuildAsync(sql => sql.Replace("{schema_name}", Thread.CurrentThread.Name));
```

Note: if the delegate returns `null` for a particular script, that script will not be run.

### Rebuild a Database

To drop an existing database and recreate it using the `create.sql` script:

```cs
await db.RebuildAsync();
```

This will drop the database if it exists and then recreate it running the `create.sql` script against it. This method also has an overload which accepts a a `modifyScript` delegate.

### Drop a Database

To drop an existing database:

```cs
await db.DropAsync();
```

This will drop the database if it exists.


### Build the Schema Only

To only run the `create.sql` script with an existing `IDbConnection`:

```cs
await db.BuildSchemaAsync(myConn);
```

The connection will be opened if it is not already opened and then the `create.sql` script will be run. This method also has an overload which accepts a a `modifyScript` delegate.

### Clear the Database

To run the `clear.sql` against an existing database:

```cs
await db.ClearAsync(myConn); //you can also not pass a connection object to run against the original connection string
```

This will open the connection if it is not already open (or create a connection from the connection string) and then run the `clear.sql` script. This will fail if the database does not exist. This method also has an overload which accepts a a `modifyScript` delegate.

### `DataContext`

The `DataContext` class inherits from `DbConnection` (and consequently implements `IDbConnection`) and provides an auto-opening transaction-tracking connection. It allows you to write code like this:

```cs
using(var conn = new DataContext(new SqlConnection("myconnectionString")))
{
	//no need to call conn.Open() here
	using (var tx = conn.BeginTransaction())
	{
		using (var cmd = conn.CreateCommand())
		{
			//no need to call cmd.Transaction = tx
			await cmd.ExecuteAsync(...);
		}

		tx.Commit();
	}
}
```

## Build from Source

Clone this repo and run:

```
dotnet build
```

Or if you have Visual Studio, just open & build.