using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SBModManager.Other {

	/// <summary>
	/// Supplements to <see cref="Path"/>
	/// </summary>
	public static class Path2 {

		/// <summary>
		/// The same as <see cref="Path.Combine(string, string)"/> but enforces Unix path separators (<c>/</c>) instead of Windows (<c>\</c>).
		/// </summary>
		/// <param name="path1"></param>
		/// <param name="path2"></param>
		/// <returns></returns>
		public static string Combine(string path1, string path2) {
			return Path.Combine(path1, path2).Replace('\\', '/');
		}

		/// <summary>
		/// The same as <see cref="Path.Combine(string, string, string)"/> but enforces Unix path separators (<c>/</c>) instead of Windows (<c>\</c>).
		/// </summary>
		/// <param name="path1"></param>
		/// <param name="path2"></param>
		/// <param name="path3"></param>
		/// <returns></returns>
		public static string Combine(string path1, string path2, string path3) {
			return Path.Combine(path1, path2, path3).Replace('\\', '/');
		}

		/// <summary>
		/// The same as <see cref="Path.Combine(string, string, string, string)"/> but enforces Unix path separators (<c>/</c>) instead of Windows (<c>\</c>).
		/// </summary>
		/// <param name="path1"></param>
		/// <param name="path2"></param>
		/// <param name="path3"></param>
		/// <param name="path4"></param>
		/// <returns></returns>
		public static string Combine(string path1, string path2, string path3, string path4) {
			return Path.Combine(path1, path2, path3, path4).Replace('\\', '/');
		}

		/// <summary>
		/// The same as <see cref="Path.Combine(ReadOnlySpan{string})"/> but enforces Unix path separators (<c>/</c>) instead of Windows (<c>\</c>).
		/// </summary>
		/// <param name="paths"></param>
		/// <returns></returns>
		public static string Combine(params ReadOnlySpan<string> paths) {
			return Path.Combine(paths).Replace('\\', '/');
		}

	}
}
