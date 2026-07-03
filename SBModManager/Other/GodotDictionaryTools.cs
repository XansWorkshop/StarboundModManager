using System;
using System.Collections.Generic;
using System.Text;

namespace SBModManager.Other {
	public static class GodotDictionaryTools {

		/// <summary>
		/// Attempts to read a value from the dictionary as a string, strictly. Other types will not be converted,
		/// only <see cref="string"/> and <see cref="StringName"/>.
		/// </summary>
		/// <param name="this"></param>
		/// <param name="key"></param>
		/// <param name="default"></param>
		/// <returns></returns>
		public static string GetValueAsStringOrDefault(this GDDictionary @this, string key, string @default) {
			if (@this.TryGetValue(key, out Variant value)) {
				if (value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName) {
					return (string)value;
				}
			}
			return @default;
		}

	}
}
