using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroDrive.Core.Config;

namespace ZeroDrive.Core.Services;

public class ClientConfigService(ILogger<ClientConfigService> _logger, string ConfigPath = "config.json")
{
	private readonly ILogger _logger = _logger;
	private readonly string ConfigPath = ConfigPath;
	private readonly SemaphoreSlim _gate = new(1, 1);

	public async Task<ClientConfig> LoadAsync(CancellationToken ct = default)
	{
		await _gate.WaitAsync(ct);
		try
		{
			// Read the file from disk
			if (!File.Exists(ConfigPath))
				return new ClientConfig([]);

			ClientConfig readCfg;
			try
			{
				// Convert it to ClientConfig
				await using var stream = File.OpenRead(ConfigPath);
				readCfg = await JsonSerializer.DeserializeAsync<ClientConfig>(
					stream,
					JsonSerializerOptions.Default,
					ct) ?? throw new Exception("Config is null after serialization");
			}
			catch (Exception e)
			{
				_logger.LogError(e, "Error reading config");
				throw;
			}
			
			// Return it
			return readCfg;
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task SaveAsync(ClientConfig config, CancellationToken ct = default)
	{
		await _gate.WaitAsync(ct);
		try
		{
			try
			{
				var dir = Path.GetDirectoryName(ConfigPath);

				// Make sure we create supplied directories, and check if it's a relative path or not.
				if (!string.IsNullOrEmpty(dir))
					Directory.CreateDirectory(dir);
				
				await using var stream = File.Create(ConfigPath);
				await JsonSerializer.SerializeAsync(stream, config, cancellationToken: ct);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Unable to save config");
				throw;
			}
		}
		finally
		{
			_gate.Release();
		}
	}
}