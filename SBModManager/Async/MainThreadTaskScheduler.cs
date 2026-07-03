using System;
using System.Collections.Generic;
using System.Text;

using static Godot.OpenXRSpatialEntityExtension;

namespace SBModManager.Async {
	/// <summary>
	/// <strong>Copied from The Conservatory's codebase.</strong>
	/// <para/>
	/// 
	/// 
	/// 
	/// <admonition type="crucial">
	/// <strong>Plain <see cref="Task"/>s <em>must not</em> be used in this scheduler!</strong>
	/// To use <see langword="async"/>/<see langword="await"/>, your method should return <see cref="SynchronousTask"/> or <see cref="SynchronousTask{TResult}"/>,
	/// <strong>NOT <see cref="Task"/> or <see cref="Task{TResult}"/>!</strong> The reason for this is highly complex and requires understanding how <see langword="async"/>
	/// code is compiled by .NET itself. You can also use synchronous (i.e. non-<see langword="async"/> methods) to avoid the <see cref="Task"/> problem.
	/// </admonition>
	/// <para/>
	/// <admonition type="danger">
	/// <strong>Avoid deadlocks with <see cref="SynchronousTask"/>!</strong>
	/// When using <see cref="Task.Factory"/>'s <see cref="TaskFactory.StartNew"/>, ensure you define <see cref="TaskCreationOptions.HideScheduler"/>, as failure to do so
	/// might cause deadlocks due to the internal work also being queued on this scheduler.
	/// </admonition>
	/// <para/>
	/// A <see cref="TaskScheduler"/> implementation which is specifically made to run its tasks on the main thread.
	/// This is required for a number of engine functions (like scene tree access).
	/// </summary>
	/// <remarks>
	/// Even though the code is within a task, you do not need to implement thread guards. Code run by this scheduler is guaranteed
	/// to be on the main thread, always (that's the entire point).
	/// </remarks>
	public sealed class MainThreadTaskScheduler : SpecificThreadTaskScheduler {

		/// <summary>
		/// The instance of this scheduler to be used in <see cref="TaskFactory.StartNew"/>
		/// </summary>
		public static MainThreadTaskScheduler Instance { get; } = new MainThreadTaskScheduler();

		/// <inheritdoc/>
		protected override bool IsOnCorrectThread => OS.GetThreadCallerId() == OS.GetMainThreadId(); // ThreadInfo.IsExecutingInMainThread;

		private MainThreadTaskScheduler() { }

		/// <inheritdoc cref="SpecificThreadTaskScheduler.FlushImpl"/>
		internal static void Flush() => Instance.FlushImpl();
	}
}
