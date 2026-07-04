using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;

using Range = System.Range;

namespace SBModManager.IO {

	/// <summary>
	/// Validators for incoming data streams.
	/// </summary>
	public static class StreamValidators {

		/// <summary>
		/// Intended for use in mod list files. For directory-based mods, the path is stored without a leading slash for
		/// every file in the hierarchy i.e. <c>interface/codex/foo.png</c>.
		/// <para/>
		/// This method asserts that the input string follows that rule <em>flawlessly</em>. Even the slightest error
		/// will result in a hard failure. We do not toy with the possible security ramifications of malicious file paths.
		/// </summary>
		/// <exception cref="InvalidDataException">Something is wrong.</exception>
		public static void AssertModPathIsValid(ReadOnlySpan<char> path) {
			if (path.Length == 0) throw new InvalidDataException("An empty file path was encountered.");
			if (path.Contains('\\')) throw new InvalidDataException("Unsupported directory separator was detected in path. Only Unix-style separators are permitted (/).");

			const int MAX_SUBSTRINGS = 64;
			Span<Range> substringRanges = stackalloc Range[MAX_SUBSTRINGS];
			int splits = path.Split(substringRanges, '/', StringSplitOptions.None);
			if (splits == MAX_SUBSTRINGS) throw new InvalidDataException("Path has too many directory separators.");

			for (int i = 0; i < splits; i++) {
				ReadOnlySpan<char> substring = path[substringRanges[i]];
				AssertValidSubdirectorySegment(substring);
			}
		}

		/// <summary>
		/// An alternative to using <see cref="Path.GetInvalidFileNameChars"/>
		/// </summary>
		/// <param name="c"></param>
		/// <returns></returns>
		private static bool IsForbiddenCharacter(char c) {
			return (ushort)c switch {
				'"' => true,
				'<' => true,
				'>' => true,
				'|' => true,
				':' => true,
				'*' => true,
				'?' => true,
				'\\' => true,
				'/' => true,
				>= 0 and < 32 => true,

				// Custom assertion:
				'%' => true,

				_ => false
			};
		}

		private static void AssertValidSubdirectorySegment(ReadOnlySpan<char> segment) {
			const string MSG = "Invalid subdirectory name.";

			// Empty names are not OK.
			if (segment.Length == 0) throw new InvalidDataException(MSG);

			// Windows trims trailing periods. I deny them outright.
			if (segment[^1] == '.') throw new InvalidDataException(MSG);
			// ^ This covers "." and ".."

			// Leading/trailing whitespace is also disallowed.
			if (char.IsWhiteSpace(segment[0]) || char.IsWhiteSpace(segment[^1])) throw new InvalidOperationException(MSG);

			// Forbidden characters.
			for (int cIndex = 0; cIndex < segment.Length; cIndex++) {
				char toTest = segment[cIndex];
				if (IsForbiddenCharacter(toTest)) throw new InvalidDataException(MSG);
			}

			// Some special stuff for Windows. On Windows, these are restricted because
			// they are aliases to system IO streams:

			// Remove the extension, since Windows ignores that for these special names.
			int dot = segment.LastIndexOf('.');
			if (dot != -1) segment = segment[..dot];

			ReadOnlySpan<string> invalidObjectNames = [
				"con", "prn", "aux", "nul",
				"com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
				"lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"
			];

			for (int i = 0; i < invalidObjectNames.Length; i++) {
				if (MemoryExtensions.Equals(segment, invalidObjectNames[i], StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException();
			}
		}

	}
}
