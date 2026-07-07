global using Godot;

global using GDArray = Godot.Collections.Array;
global using GDDictionary = Godot.Collections.Dictionary;

global using NoDiscard = System.Diagnostics.Contracts.PureAttribute;
global using RID = Godot.Rid;
using HttpClient = System.Net.Http.HttpClient;

namespace SBModManager {

	public static class SBModManagerGlobals {

		/// <summary>
		/// A shared <see cref="HttpClient"/>. Microsoft recommends having a shared instance of this type.
		/// </summary>
		public static readonly HttpClient HTTP_CLIENT = new HttpClient();

	}

}