using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SBModManager.SteamInterop {

	/// <summary>
	/// Provides an interface to run SteamCMD from C#.
	/// </summary>
	public static class SteamCMD {

		// FUTURE XAN:
		// No matter how hard you tried, you could not get SteamCMD to accept input from stdin.
		// I don't know why this doesn't work. I was doing it wrong maybe? But I tried a lot, including
		// adapting VB code that Valve themselves vetted. Maybe something broke.

		// But that's why I rely on scripts for more complex operations.

		/// <summary>
		/// Runs a SteamCMD script by its file path then exits. Returns a task which is completed once SteamCMD exits.
		/// </summary>
		/// <param name="scriptPath">The path to a script file.</param>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> which will stop running the script. If SteamCMD is running, cancellation will kill the program.</param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="OperationCanceledException"></exception>
		public static async Task RunSteamCMDScriptAsync(string scriptPath, CancellationToken cancellationToken) {
			ProcessStartInfo info = new ProcessStartInfo {
				FileName = Directories.GetSteamCMDProgram(),
				CreateNoWindow = true
			};
			info.ArgumentList.Add($"+runscript \"{scriptPath}\"");
			info.ArgumentList.Add("+quit");
			Process? steamCMD = Process.Start(info);
			if (steamCMD == null) throw new InvalidOperationException("Failed to start SteamCMD");
			try {
				await steamCMD.WaitForExitAsync(cancellationToken);
			} catch (OperationCanceledException) {
				steamCMD.Kill(true);
				throw;
			}
		}

		/// <summary>
		/// Runs SteamCMD then closes it after all commands have run. Returns a task which will complete
		/// once SteamCMD exits.
		/// </summary>
		/// <param name="args">Each command to run, with its own args. Should not include the leading + symbol. Quotations are handled for you and no escape sequences are necessary.</param>
		/// <returns></returns>
		public static async Task RunSteamCMDAsync(string[] args, CancellationToken cancellationToken) {
			ProcessStartInfo info = new ProcessStartInfo {
				FileName = Directories.GetSteamCMDProgram(),
				CreateNoWindow = true
			};
			for (int i = 0; i < args.Length; i++) {
				string arg = args[i];
				arg = arg.Replace("\"", "\\\"");
				info.ArgumentList.Add($"+{arg}");
			}
			info.ArgumentList.Add("+exit");
			Process? steamCMD = Process.Start(info);
			if (steamCMD == null) throw new InvalidOperationException("Failed to start SteamCMD");
			try {
				await steamCMD.WaitForExitAsync(cancellationToken);
			} catch (OperationCanceledException) {
				steamCMD.Kill(true);
				throw;
			}
		}

	}

}
