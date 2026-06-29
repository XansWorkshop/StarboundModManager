using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SBModManager {

	/// <summary>
	/// Represents an entire modpack.
	/// </summary>
	public sealed class Modpack {

		/// <summary>
		/// The name of this modpack, in a user-friendly format.
		/// </summary>
		public required string Name { get; set; }

		/// <summary>
		/// The name of the modpack directory, typically a sanitized version of its name.
		/// </summary>
		public required string DirectoryName { get; set; }

		/// <summary>
		/// The location of the Starbound executable.
		/// </summary>
		public required FileInfo StarboundExecutable { get; set; }

		/// <summary>
		/// Determines if the launcher should skip loading mods from the Steam workshop.
		/// </summary>
		/// <remarks>
		/// This is enabled by default to support setting up mods from the workshop manually by
		/// copying them out of the workshop directory and into the custom mods folder.
		/// 
		/// // Backed by includeUGC parameter.
		/// </remarks>
		public bool AllowSteamWorkshop { get; set; } = true;

		private Modpack() { }

		/// <summary>
		/// Attempts to create a modpack with the provided name.
		/// </summary>
		/// <param name="starbound">The location of Starbound.exe</param>
		/// <param name="name"></param>
		/// <param name="modpack"></param>
		/// <returns></returns>
		public static Error CreateModpack(FileInfo starbound, string name, out Modpack? modpack) {
			modpack = null;
			return Error.PrinterOnFire;
		}

		/// <summary>
		/// Returns the JSON string which represents the contents of sbinit.config
		/// </summary>
		/// <returns></returns>
		public string GetSBInitContents() {
			GDArray assetDirectories = [];
			GDDictionary sbInit = [

			];
			return "";
		}

	}
}
