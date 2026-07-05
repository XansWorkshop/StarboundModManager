using System;
using System.Collections.Generic;
using System.Text;

namespace SBModManager.Other {
	public static class ExceptionBullshittery {

		/// <summary>
		/// I'm not angry, what makes you think I'm angry?
		/// </summary>
		/// <param name="exc"></param>
		/// <returns></returns>
		public static bool IsCancellation(this Exception exc) {
			if (exc is OperationCanceledException) return true;
			if (exc is AggregateException aggregate) {
				AggregateException flattened = aggregate.Flatten();
				foreach (Exception inner in flattened.InnerExceptions) {
					if (inner is not OperationCanceledException) return false;
				}
				return true;
			}
			return false;
		}

	}
}
