using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZeroDrive.Core.Services;
using ZeroDrive.Core.Protos;
using ZeroDrive.Core.Hashing;
using ZeroDrive.Client.Chunking;

namespace ZeroDrive.Core;

public readonly record struct DirtyFileInfo(string FilePath, string? OldFilePath = null);

class Program
{
	static async Task<int> Main(string[] args)
	{
		var builder = Host.CreateApplicationBuilder(args);

		// Register factory delegate for content hasher
		builder.Services.AddTransient<IContentHasher, SHA256ContentHasher>();

		// Register a factory that creates chunkers
		builder.Services.AddSingleton<Func<ChunkerBase>>(sp =>
		{
			var hasher = sp.GetRequiredService<IContentHasher>();
			return () => new FixedSizeChunker(hasher, chunkSize: 1024);
		});

		// Add the ClientConfigService
		builder.Services.AddSingleton<ClientConfigService>();

		// Add the WorkspaceService
		builder.Services.AddSingleton<WorkspaceService>();

		// Configure the gRPC client
		builder.Services.AddGrpcClient<SyncService.SyncServiceClient>(c =>
		{
			c.Address = new Uri(
				builder.Configuration.GetValue<string>("GrpcServiceHost") 
				?? "http://localhost:5000"
			);
		});

		// Add the SyncWorkerService as hosted service
		builder.Services.AddHostedService<SyncWorkerService>();

		var host = builder.Build();
		await host.StartAsync();

		bool exit = false;

		while (!exit)
		{
			Console.WriteLine("Enter a command (type 'exit' to quit):");
			var input = Console.ReadLine();
			switch (input?.ToLower())
			{
				case "exit":
					Console.WriteLine("Exiting...");
					exit = true;
					break;
				case "watch":
					Console.WriteLine("Enter the directory path to watch:");
					var directoryPath = Console.ReadLine();
					if (!string.IsNullOrWhiteSpace(directoryPath))
					{
						var directoryWatcherService = host.Services.GetRequiredService<DirectoryWatcherService>();
						directoryWatcherService.AddWatch(directoryPath);
						Console.WriteLine($"Started watching directory: {directoryPath}");
					}
					else
					{
						Console.WriteLine("Invalid directory path.");
					}
					break;
				case "unwatch":
					Console.WriteLine("Enter the directory path to unwatch:");
					var unwatchDirectoryPath = Console.ReadLine();
					if (!string.IsNullOrWhiteSpace(unwatchDirectoryPath))
					{
						var directoryWatcherService = host.Services.GetRequiredService<DirectoryWatcherService>();
						if (directoryWatcherService.RemoveWatch(unwatchDirectoryPath))
						{
							Console.WriteLine($"Stopped watching directory: {unwatchDirectoryPath}");
						}
						else
						{
							Console.WriteLine($"Failed to stop watching directory: {unwatchDirectoryPath}");
						}
					}
					else
					{
						Console.WriteLine("Invalid directory path.");
					}
					break;
				default:
					Console.WriteLine($"Unknown command: {input}");
					break;
			}
		}

		await host.StopAsync();

		return 0;
	}
}
