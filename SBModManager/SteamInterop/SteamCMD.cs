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
	/// Helps interoperability with SteamCMD.
	/// </summary>
	public static class SteamCMD {

		/// <summary>
		/// Determines if SteamCMD is available.
		/// </summary>
		public static bool HasSteamCMD => ProgramSettings.SteamCMD != null && ProgramSettings.SteamCMD.Exists;

		/// <summary>
		/// Runs a SteamCMD script by its file path then exits. Returns a task which is completed once SteamCMD exits.
		/// </summary>
		/// <param name="scriptPath"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public static async Task RunSteamCMDScriptAsync(string scriptPath, CancellationToken cancellationToken) {
			ProcessStartInfo info = new ProcessStartInfo {
				FileName = ProgramSettings.SteamCMD?.FullName ?? throw new InvalidOperationException("SteamCMD is not installed."),
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
				FileName = ProgramSettings.SteamCMD?.FullName ?? throw new InvalidOperationException("SteamCMD is not installed."),
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
			}
		}

	}

}
