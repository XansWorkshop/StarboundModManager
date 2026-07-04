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
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> used to terminate launching or exit the game.</param>
		/// <returns></returns>
		public static async Task LaunchAsync(Modpack modpack, GeneralProgressWindow progressWindow, CancellationToken cancellationToken) {
			ArgumentNullException.ThrowIfNull(modpack);
			ArgumentNullException.ThrowIfNull(progressWindow);
			if (!cancellationToken.CanBeCanceled) throw new ArgumentException("The CancellationToken must be valid.");

			progressWindow.SetStatus("Preparing launch configuration...\nIf any mods are missing, they will be downloaded during this step.");
			progressWindow.SetProgress(float.NaN);
			string sbinitConfig = await modpack.SaveAndUpdateInitAsync(cancellationToken);

			progressWindow.SetStatus("Launching Starbound...");
			ProcessStartInfo starboundStartInfo = new ProcessStartInfo {
				FileName = Directories.GetLocalStarboundProgram()
			};
			starboundStartInfo.ArgumentList.Add("-bootconfig");
			starboundStartInfo.ArgumentList.Add(sbinitConfig);

			Process starbound = new Process() {
				StartInfo = starboundStartInfo
			};
			starbound.Start();

			progressWindow.SetStatus("Starbound is running.");
			progressWindow.CancelButton.Text = "Force Quit Starbound";

			try {
				await starbound.WaitForExitAsync(cancellationToken);
			} catch (OperationCanceledException) {
				starbound.Kill();
			}
		}

	}
}
