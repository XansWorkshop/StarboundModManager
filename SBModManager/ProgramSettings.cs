using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SBModManager {

	/// <summary>
	/// The program's settings.
	/// </summary>
	public static class ProgramSettings {

		/// <summary>
		/// The location of SteamCMD, or <see langword="null"/> if it is not installed.
		/// </summary>
		public static bool DidFirstTimeSetup { get; set; }

		public static void Load() {
			try {
				string cfg = Directories.GetAppConfigFile();
				Variant json = Json.ParseString(File.ReadAllText(cfg));
				if (json.Obj is GDDictionary dictionary) {
					DidFirstTimeSetup = (bool)dictionary["did_first_time_setup"];
				}
			} catch { }
		}

		public static void Save() {
			try {
				string cfg = Directories.GetAppConfigFile();
				File.WriteAllText(
					cfg, 
					Json.Stringify(
						new GDDictionary {
							{ "did_first_time_setup", DidFirstTimeSetup }
						},
						"\t", false, false
					)
				);
			} catch { }
		}



	}
}
