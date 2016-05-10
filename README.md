# Data Utils

> Utility classes to create databases and build schemas from sql scripts.

## How to Use

### Install via Nuget

```
install-package Archon.Data
```

### Create a Database Instance

You should have a `create.sql` & a `clear.sql` as embedded resources in your class library. The `create.sql` script should only create tables & schemas and whatnot. It shouldn't try to create the database. The `clear.sql` script should simply clear the tables. Don't drop or create tables in the `clear.sql` script.

```cs
var db = new Database(typeof(MyType)); //where MyType is in the same namespace as your create.sql & clear.sql
```

or

```cs
var db = new Database(typeof(MyType).Assembly, "MyAssembly.Weird.Namespace.Folder1"); //specify what namespace the embedded create & clear sql scripts are in
```

### Build a Database

To build a new database using the `create.sql` script:

```cs
db.Build("server=.\\sqlexpress;database=mydb;integrated security=true;");
```

This will create the database if it doesn't already exist and then run the `create.sql` script against it.

### Rebuild a Database

To drop an existing database and recreate it using the `create.sql` script:

```cs
db.Rebuild("server=.\\sqlexpress;database=mydb;integrated security=true;");
```

This will drop the database if it exists and then recreate it running the `create.sql` script against it.

### Build the Schema Only

To only run the `create.sql` script with an existing `IDbConnection`:

```cs
db.BuildSchema(myConn);
```

The connection will be opened if it is not already opened and then the `create.sql` script will be run.

### Clear the Database

To run the `clear.sql` against an existing database:

```cs
db.Clear(myConn); //you can also pass a connection string here
```

This will open the connection if it is not already open (or create a connection from the connection string) and then run the `clear.sql` script. This will fail if the database does not exist.

### `DataContext`

The `DataContext` class implements `IDbConnection` and provides an auto-opening transaction-tracking connection. It allows you to write code like this:

```cs
using(var conn = new DataContext(new SqlConnection("myconnectionString")))
{
	//no need to call conn.Open() here
	using (var tx = conn.BeginTransaction())
	{
		using(var cmd = conn.CreateCommand())
		{
			//no need to call cmd.Transaction = tx
			cmd.Execute(...);
		}

		tx.Commit();
	}
}
```

## How to Build from Source

Open in Visual Studio and build.