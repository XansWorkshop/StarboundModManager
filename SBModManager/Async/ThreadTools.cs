using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SBModManager.Async {

	/// <summary>
	/// Tools to run code on other threads.
	/// </summary>
	public static class ThreadTools {

		/// <summary>
		/// Executes the provided <paramref name="action"/> on the main thread asynchronously. If this is called from the main thread,
		/// this method will run synchronously.
		/// </summary>
		/// <param name="action"></param>
		/// <returns></returns>
		public static Task ExecuteOnMainThreadAsync(Action action) {
			if (OS.GetThreadCallerId() == OS.GetMainThreadId()) {
				action();
				return Task.CompletedTask;
			} else {
				return Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, MainThreadTaskScheduler.Instance);
			}
		}

	}
}
