using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SBModManager.Menus.Windows;
using SBModManager.ModInstances;

namespace SBModManager {

	/// <summary>
	/// Handles the launch routine.
	/// </summary>
	public static class Launcher {

		/// <summary>
		/// Launches the game using the provided modpack. The task completes once Starbound is closed.
		/// </summary>
		/// <param name="modpack">The modpack to launch.</param>
		/// <param name="progressWindow">The progress of the launch. This is an exclusive window, so it doubles as a means to lock out the UI as well.</param>
		/// <param name="completeWhenStarboundCloses">If true, the task completes when Starbound closes. Otherwise, it completes once it launches.</param>
		/// <param name="launchServer">If true, launch the server. Otherwise, launch the client.</param>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> used to terminate launching or exit the game.</param>
		/// <returns></returns>
		public static async Task LaunchAsync(Modpack modpack, GeneralProgressWindow progressWindow, bool completeWhenStarboundCloses, bool launchServer, CancellationToken cancellationToken) {
			ArgumentNullException.ThrowIfNull(modpack);
			ArgumentNullException.ThrowIfNull(progressWindow);
			if (!cancellationToken.CanBeCanceled) throw new ArgumentException("The CancellationToken must be valid.");

			progressWindow.SetStatus("Preparing launch configuration...\nIf any mods are missing, they will be downloaded during this step.", "Launching Starbound");
			progressWindow.SetProgress(float.NaN);
			modpack.LastPlayed = DateTime.Now;
			(string sbInitClientPath, string sbInitServerPath) = await modpack.SaveAndUpdateInitsAsync(cancellationToken);

			if (launchServer) {
				progressWindow.SetStatus("Launching Starbound Server...", "Launching Starbound");
			} else {
				progressWindow.SetStatus("Launching Starbound...", "Launching Starbound");
			}
			ProcessStartInfo starboundStartInfo = new ProcessStartInfo {
				FileName = launchServer ? Directories.GetLocalStarboundServerProgram() : Directories.GetLocalStarboundProgram()
			};
			starboundStartInfo.ArgumentList.Add("-bootconfig");
			if (launchServer) {
				starboundStartInfo.ArgumentList.Add(sbInitServerPath);
			} else {
				starboundStartInfo.ArgumentList.Add(sbInitClientPath);
			}

			Process starbound = new Process() {
				StartInfo = starboundStartInfo
			};
			starbound.Start();

			try {
				if (completeWhenStarboundCloses) {
					if (launchServer) {
						progressWindow.SetStatus("Starbound Server is now running.\nIn order to use the mod manager,\nyou must exit the game.", "Starbound Is Running!");
					} else {
						progressWindow.SetStatus("Starbound is now running.\nIn order to use the mod manager,\nyou must exit the game.", "Starbound Is Running!");
					}
					progressWindow.CancelButton.SetDeferred("text", "Force Quit Starbound");
					await starbound.WaitForExitAsync(cancellationToken);
				}
			} catch (OperationCanceledException) {
				starbound.Kill();
			}
		}

	}
}
