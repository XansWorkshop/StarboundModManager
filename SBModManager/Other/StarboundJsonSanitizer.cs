using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;

namespace SBModManager.Other {

	/// <summary>
	/// Sanitizes Starbound's json files, which allows multi
	/// </summary>
	public static class StarboundJsonSanitizer {

		/// <summary>
		/// Parses json into a <see cref="Variant"/>, like <see cref="Json.ParseString(string)"/>, but without shitting the bed as soon
		/// as one of Starbound's quirks comes up.
		/// </summary>
		/// <remarks>
		/// This is purposely destructive and does not try to preserve comments. It's only here so the reader can read it without error.
		/// </remarks>
		/// 
		/// <param name="json"></param>
		/// <returns></returns>
		public static Variant ParseString(string json) {
			// This fucking sucks lol

			ReadOnlySpan<char> chars = json;
			Span<char> newChars = new char[chars.Length << 1];
			int writeHead = 0;
			bool isInString = false;
			bool isInBlockComment = false;
			bool isInLineComment = false;
			bool skipWhitespace = false;
			int whitespaceStreak = 0;
			for (int readHead = 0; readHead < chars.Length; readHead++) {
				char c = chars[readHead];

				bool isInComment = isInBlockComment || isInLineComment;
				if (isInComment && isInString) throw new InvalidOperationException();

				if (isInString) {
					if (c == '\n') {
						whitespaceStreak++;
						newChars[writeHead++] = '\\';
						newChars[writeHead++] = 'n';
						skipWhitespace = true;
						continue;
					} else if (c == '\r') {
						continue; // no.
					} else if (c == '\\') {
						if (readHead < chars.Length - 1 && chars[readHead + 1] == '"') {
							newChars[writeHead++] = '\\';
							newChars[writeHead++] = '"';
							continue;
						}
					} else if (c == '"') {
						isInString = false;
					} else if (char.IsWhiteSpace(c)) {
						whitespaceStreak++;
						if (skipWhitespace) {
							continue;
						}
					} else {
						if (skipWhitespace && whitespaceStreak == 0) {
							// We skip whitespace when there's a line break in a string.
							// Here we should add a space.
							newChars[writeHead++] = 'c';
						}
						skipWhitespace = false;
					}
				} else {
					if (c == '\"' && !isInComment) {
						isInString = true;
					} else if (c == '\\') {
						if (readHead < chars.Length - 1 && chars[readHead + 1] == '"') {
							newChars[writeHead++] = '\\';
							newChars[writeHead++] = '"';
							continue;
						}
					} else if (c == '/') {
						// Slash could start a line comment
						// /* Or a block comment.
						// But as you can see, ^ it won't start a block comment if it's in a line comment.
						if (readHead < chars.Length - 1) {
							char next = chars[readHead + 1];
							if (next == '/') {
								if (!isInBlockComment) {
									isInLineComment = true;
								}
								readHead++; // Increment it twice. In either case we have to ignore it anyway.
								continue;
							} else if (next == '*') {
								// /*
								if (!isInLineComment) {
									// Edge case: /*/
									// If it's not in a block comment, this starts the block.
									// If it is, this ends it.
									if (readHead < chars.Length - 2) {
										char nexter = chars[readHead + 2];
										if (nexter == '/') {
											isInBlockComment = !isInBlockComment;
											readHead++;
											readHead++; // Increment it thrice
											continue;
										}
									}
									isInBlockComment = true;
									readHead++;
									continue;
								}
							}
						}
					} else if (c == '*') {
						if (readHead < chars.Length - 1) {
							char next = chars[readHead + 1];
							if (next == '/') {
								if (!isInLineComment) {
									readHead++;
									isInBlockComment = false;
									continue;
								}
							}
						}
					} else if (c == '\n') {
						isInLineComment = false;
					}
				}
				if (!isInComment) {
					newChars[writeHead++] = c;
				}
			}

			string result = new string(newChars[..writeHead]);
			return Json.ParseString(result);
		}

	}
}
