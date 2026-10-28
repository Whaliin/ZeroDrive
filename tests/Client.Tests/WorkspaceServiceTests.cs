using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZeroDrive.Core;
using ZeroDrive.Core.Config;
using ZeroDrive.Core.Services;
using ZeroDrive.Core.DTOs;
using ZeroDrive.Tests.Helpers;

public class WorkspaceServiceTests : IAsyncLifetime
{
	/*
	Add workspace, verify it exists, create a file in the directory (check channel for changes)
	Remove workspace, verify its no longer in the list, verify changes are no longer pushed
	Remove non-existent folder (should not throw)

	SetWorkspaces (check changes are made in the list)
	TryGetWorkspace WORKS (returns true, gives a valid map)
	TryGetWorkspace FAILS (returns false with null map)

	TryFindWorkspaceForPath (returns true/false, gives valid/null respectively)

	Disposal tests? can use JetBrains dotMemoryUnit
	*/

	private readonly WorkspaceService wss;
	private TestDir dir;
	private string cfgPath;

	private readonly DirectoryWatcherOptions dwso = new()
	{
		DebounceTimeoutMs = 100,
		PushLoopDelayMs = 5
	};

	public WorkspaceServiceTests()
	{
		var sp = new ServiceCollection()
			.AddLogging()
			.BuildServiceProvider();
		
		var lf = sp.GetService<ILoggerFactory>() ?? throw new Exception("no ILoggerFactory");

		dir = new TestDir();

		cfgPath = dir.CreateTestPath("config.json");

		var ccs = new ClientConfigService(lf.CreateLogger<ClientConfigService>(), cfgPath);

		wss = new WorkspaceService(lf, ccs, dwso);
	}

	public async Task InitializeAsync()
	{
		// w start
		await wss.StartAsync();
	}

#region Tests

	[Fact]
	public void Test_DryRun()
	{
		// This is actually a ClientConfigService test
		// Assert.True(File.Exists(cfgPath));
		Assert.Equal(0, wss.WorkspaceCount);
	}

	[Fact]
	public async Task Test_WithConfig()
	{
		// Now we need to create a config file, then re-initialize the ccs and ws with that path
		var testPath = dir.CreateTestPath("testCfg.json");
		File.WriteAllText(testPath, JsonSerializer.Serialize(new ClientConfig(new()
		{
			new WorkspaceMappingConfig(Guid.NewGuid(), dir.CreateTestPath("Workspace1")),
			new WorkspaceMappingConfig(Guid.NewGuid(), dir.CreateTestPath("Workspace2")),
		})));

		var sp = new ServiceCollection()
			.AddLogging()
			.BuildServiceProvider();
		
		var lf = sp.GetService<ILoggerFactory>() ?? throw new Exception("no ILoggerFactory");

		var ccs = new ClientConfigService(lf.CreateLogger<ClientConfigService>(), testPath);

		await using var wsTest = new WorkspaceService(lf, ccs);
		await wsTest.StartAsync();

		Assert.Equal(2, wsTest.WorkspaceCount);
	}

	// Add workspace, verify it exists, create a file in the directory (check channel for changes)
	[Fact]
	public async Task Test_Workspace_Add()
	{
		var wsT = dir.CreateTestPath("WorkspaceTest");
		var ws = new LocalWorkspaceMap(wsT);

		wss.AddWorkspace(ws);
		Assert.Equal(1, wss.WorkspaceCount);

		// Add a file
		using var fs = File.Create(Path.Combine(wsT, "test.txt"));

		// Wait for debouncing
		await Task.Delay(dwso.DebounceTimeoutMs * 2);

		// Check an event was written to the channel
		Assert.Equal(1, wss.FileChanges.Count);
	}

	// Remove workspace, verify its no longer in the list, verify changes are no longer pushed
	[Fact]
	public async Task Test_Workspace_Remove_Delete()
	{
		var wsT = dir.CreateTestPath("WorkspaceTest");
		var ws = new LocalWorkspaceMap(wsT);
		var fsP = Path.Combine(wsT, "test.txt");

		wss.AddWorkspace(ws);

		var fs = File.Create(fsP);
		fs.Dispose();

		await Task.Delay(dwso.DebounceTimeoutMs * 2);

		wss.RemoveWorkspace(ws.Workspace.Id, true);

		// Test that the dir was deleted
		Assert.False(File.Exists(fsP));
		Assert.False(Directory.Exists(wsT));
		
		// Create dir again and write again
		Directory.CreateDirectory(wsT);

		fs = File.Create(fsP);
		fs.Dispose();

		await Task.Delay(dwso.DebounceTimeoutMs * 2);

		// The event should stay in the queue, because any consumer would call TryGetWorkspaceId anyways
		Assert.Equal(1, wss.FileChanges.Count);
	}

	[Fact]
	public async Task Test_Workspace_Remove_Keep()
	{
		var wsT = dir.CreateTestPath("WorkspaceTest");
		var ws = new LocalWorkspaceMap(wsT);
		var fsP = Path.Combine(wsT, "test.txt");

		wss.AddWorkspace(ws);

		var fs = File.Create(fsP);
		fs.Dispose();

		await Task.Delay(dwso.DebounceTimeoutMs * 2);

		wss.RemoveWorkspace(ws.Workspace.Id, false);

		// Inadvertently works since the file would probably get deleted with RemoveWorkspace
		Assert.True(File.Exists(fsP));

		fs = File.Create(fsP);
		fs.Dispose();

		await Task.Delay(dwso.DebounceTimeoutMs * 2);

		// The event should stay in the queue, because any consumer would call TryGetWorkspaceId anyways
		Assert.Equal(1, wss.FileChanges.Count);
	}

	// Remove non-existent folder (should not throw)
	[Fact]
	public void Test_Workspace_Remove_Nonexistent()
	{
		Assert.Equal(0, wss.WorkspaceCount);
		wss.RemoveWorkspace(Guid.NewGuid());
		Assert.Equal(0, wss.WorkspaceCount);
	}

	// SetWorkspaces (check changes are made in the list)
	[Fact]
	public void Test_Workspace_Set()
	{
		List<LocalWorkspaceMap> maps = [
			new LocalWorkspaceMap(dir.CreateTestPath("WS1")),
			new LocalWorkspaceMap(dir.CreateTestPath("WS2")),
			new LocalWorkspaceMap(dir.CreateTestPath("WS3"))
		];
		wss.SetWorkspaces(maps);

		Assert.Equal(3, wss.WorkspaceCount);
	}

	// TryGetWorkspace WORKS (returns true, gives a valid map)
	[Fact]
	public void Test_Workspace_TryGetWorkspace_Success()
	{
		var wsT = new LocalWorkspaceMap(dir.CreateTestPath("WSTEST"));
		List<LocalWorkspaceMap> maps = [wsT];
		wss.SetWorkspaces(maps);

		Assert.True(wss.TryGetWorkspace(wsT.Workspace.Id, out var map));
		Assert.NotNull(map);
	}

	// TryGetWorkspace FAILS (returns false with null map)
	[Fact]
	public void Test_Workspace_TryGetWorkspace_Failure()
	{
		var wsT = new LocalWorkspaceMap(dir.CreateTestPath("WSTEST"));

		Assert.False(wss.TryGetWorkspace(wsT.Workspace.Id, out var map));
		Assert.Null(map);
	}

	// TryFindWorkspaceForPath (returns true/false, gives valid/null respectively)
	[Fact]
	public void Test_Workspace_TryFindWorkspaceForPath_Success()
	{
		var tP = dir.CreateTestPath("WSTEST");
		var tF = Path.Combine(tP, "testFile.txt");
		var wsT = new LocalWorkspaceMap(tP);
		List<LocalWorkspaceMap> maps = [wsT];
		wss.SetWorkspaces(maps);

		Assert.True(wss.TryFindWorkspaceForPath(tF, out var map));
		Assert.NotNull(map);
	}

	[Fact]
	public void Test_Workspace_TryFindWorkspaceForPath_Failure()
	{
		var tP = dir.CreateTestPath("WSTEST");
		var tF = Path.Combine(tP, "testFile.txt");
		// var wsT = new LocalWorkspaceMap(tP);
		// List<LocalWorkspaceMap> maps = [wsT];
		// wss.SetWorkspaces(maps);

		Assert.False(wss.TryFindWorkspaceForPath(tF, out var map));
		Assert.Null(map);
	}

#endregion

	public async Task DisposeAsync()
	{
		// w dispose
		await wss.DisposeAsync();
		dir.Dispose();
	}
}