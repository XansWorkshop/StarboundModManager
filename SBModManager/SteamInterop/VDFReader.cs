using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;

using Range = System.Range;

namespace SBModManager.SteamInterop {

	/// <summary>
	/// A class which reads Valve's .vdf format. This is used to find the Steam installation of Starbound.
	/// </summary>
	public static class VDFReader {

		/// <summary>
		/// Returns a <see cref="VDFObject"/> named "VDF" which contains all entries of a vdf file.
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static VDFObject? TryReadVDF(string path) {
			try {
				string[] lines = File.ReadAllLines(path);
				VDFObject root = new VDFObject() {
					Name = "VDF"
				};
				int index = 0;
				ReadNext(root, lines, ref index);
				return root;
			} catch (Exception exc) {
				GD.PushError(exc);
				return null;
			}
		}

		private static string StripQuotesOrThrow(string text) {
			if (text[0] == '"' && text[^1] == '"') {
				return text[1..^1];
			}
			throw new InvalidOperationException("Line is not a string surrounded by quotation marks.");
		}

		private static string ReplaceEscapeSequences(string text) {
			return text	.Replace("\\\\", "\\")
						.Replace("\\n", "\n")
						.Replace("\\r", "\r")
						.Replace("\\t", "\t")
						.Replace("\\v", "\v")
						.Replace("\\0", "\0")
						.Replace("\\\"", "\"");
		}

		private static void ReadNext(VDFObject parent, ReadOnlySpan<string> lines, ref int index) {
			while (string.IsNullOrWhiteSpace(lines[index])) index++;

			Span<Range> ranges = stackalloc Range[2];
			string line = lines[index].Trim();
			int splits = line.Split(ranges, '\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (splits == 1) {
				if (line[0] == '"') {
					// Name
					string name = StripQuotesOrThrow(line);
					VDFObject container = new VDFObject {
						Name = name
					};

					bool gotObjectOpening = false;
					while (true) {
						index++;
						line = lines[index].Trim();
						if (string.IsNullOrWhiteSpace(line)) continue;
						if (line[0] == '{') {
							if (gotObjectOpening) throw new InvalidDataException($"Unexpected object opener on line index {index}");
							gotObjectOpening = true;
							continue;
						}
						if (line[0] == '}') break;
						ReadNext(container, lines, ref index);
					}
					parent.Values[name] = container;
				} else {
					throw new InvalidDataException($"Unexpected token on line index {index}.");
				}
			} else {
				string key = StripQuotesOrThrow(line[ranges[0]]);
				string value = StripQuotesOrThrow(line[ranges[1]]);
				key = ReplaceEscapeSequences(key);
				value = ReplaceEscapeSequences(value);
				parent.Values[key] = value;
			}
		}
	}
	
	/// <summary>
	/// An object block in a VDF file.
	/// </summary>
	public class VDFObject {

		/// <summary>
		/// The key of this object.
		/// </summary>
		public string Name { get; init; } = string.Empty;

		/// <summary>
		/// The values in this object. Values are either strings, or <see cref="VDFObject"/>s, and nothing else.
		/// </summary>
		public Dictionary<string, object> Values { get; } = [];

		/// <summary>
		/// A proxy to reading the same key from <see cref="Values"/>.
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public object this[string key] => Values[key];

		/// <summary>
		/// Reads the provided key as a string. Raises <see cref="InvalidCastException"/> if it's a <see cref="VDFObject"/>.
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public string GetValue(string key) => (string)Values[key];

		/// <summary>
		/// Reads the provided key as a <see cref="VDFObject"/>. Raises <see cref="InvalidCastException"/> if it's a <see cref="string"/>.
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public VDFObject GetChild(string key) => (VDFObject)Values[key];

	}
}
