using ZeroDrive.Server.Services;
using ZeroDrive.Server.Storage;

namespace ZeroDrive.Server;

using RepoDb;

public class Program
{
	public static void Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);

		// Add the configuration files
		builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
			.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
			.AddJsonFile("config.json", optional: true, reloadOnChange: true);
			//.AddEnvironmentVariables();


		// Add services to the container.
		// Configuration provider for ServerConfig
		builder.Services.Configure<ServerConfig>(builder.Configuration.GetSection("ServerConfig"));
		
		// gRPC services
		builder.Services.AddGrpc(options =>
		{
			options.MaxReceiveMessageSize = builder.Configuration.GetValue<int>("ServerConfig:GRPCMaxMessageSize");
			options.MaxSendMessageSize = builder.Configuration.GetValue<int>("ServerConfig:GRPCMaxMessageSize");

			if (builder.Environment.IsDevelopment())
			{
				options.EnableDetailedErrors = true;
			}
		});

		// Add the storage provider based on the config
		var serverConfig = builder.Configuration.GetSection("ServerConfig").Get<ServerConfig>() ?? new ServerConfig();
		var storageProvider = CreateStorageProvider(serverConfig);

		/*
		builder.Services.AddSingleton<IStorageProvider, FileStorageProvider>(sp =>
		{
			var storageDirectory = builder.Configuration.GetValue<string>("StorageDirectory") ?? "blobs";
			return new FileStorageProvider(storageDirectory);
		});
		*/

		// Build the app
		var app = builder.Build();

		// Configure the HTTP request pipeline.
		if (!app.Environment.IsDevelopment())
		{
			app.UseHttpsRedirection();
		}

		app.MapGrpcService<SyncServiceImpl>();

		app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

		app.Run();
	}

	private static IStorageProvider CreateStorageProvider(ServerConfig config) => 
		config.StorageProviderType switch
		{
			StorageProviderType.FileSystem => new FileStorageProvider(config.FileStorage ?? new FileStorageConfig()),
			StorageProviderType.SQLite => new SQLiteStorageProvider(config.SQLiteStorage ?? new SqliteStorageConfig()),
			_ => throw new InvalidOperationException($"Unsupported storage provider type: {config.StorageProviderType}")
		};
}