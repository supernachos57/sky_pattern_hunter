using System.Windows;

namespace SkyPatternHunter.Presentation;

public partial class App : System.Windows.Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		var validation = StartupConfigurationValidator.ValidateAtStartup();
		if (!validation.IsValid)
		{
			Environment.ExitCode = validation.ExitCode;
			MessageBox.Show(
				validation.ErrorMessage ?? "Application startup configuration is invalid.",
				"Sky Pattern Hunter Startup Error",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
			Shutdown(validation.ExitCode);
			return;
		}

		base.OnStartup(e);
	}
}
