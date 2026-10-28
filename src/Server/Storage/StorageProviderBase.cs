using Microsoft.Extensions.Options;

namespace ZeroDrive.Server.Storage;

/// <summary>
/// Base class for storage providers that can be configured with a specific configuration type.
/// </summary>
/// <typeparam name="TConfig"></typeparam>
/// <param name="options"></param>
public abstract class ConfigurableStorageProviderBase<TConfig> : IDisposable 
	where TConfig : class, IStorageConfig
{
	protected TConfig Configuration { get; private set; }
	private readonly IDisposable? _configMonitor;

	protected ConfigurableStorageProviderBase(IOptionsMonitor<TConfig> optionsMonitor)
	{
		Configuration = optionsMonitor.CurrentValue ?? throw new ArgumentNullException(nameof(optionsMonitor));
		_configMonitor = optionsMonitor.OnChange(OnConfigurationChanged);
	}

	private void OnConfigurationChanged(TConfig newConfig)
	{
		Configuration = newConfig ?? throw new ArgumentNullException(nameof(newConfig), "New configuration options cannot be null.");
		HandleConfigurationChanged(newConfig);
	}

	/// <summary>
	/// Called when the configuration has changed. Derived classes can implement this method to handle changes in the configuration.
	/// </summary>
	/// <param name="newConfig"></param>
	protected virtual void HandleConfigurationChanged(TConfig newConfig) {}

	public void Dispose()
	{
		_configMonitor?.Dispose();
		GC.SuppressFinalize(this);
	}
}