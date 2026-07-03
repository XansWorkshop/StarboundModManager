using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SBModManager.Async {
	/// <summary>
	/// <strong>Copied from The Conservatory's codebase.</strong>
	/// <para/>
	/// 
	/// The base class for a <see cref="TaskScheduler"/> which runs on a specific thread.
	/// </summary>
	/// <remarks>
	/// In The Conservatory, this is used for more than just the main thread scheduler. So here it's kind of bloat.
	/// </remarks>
	public abstract class SpecificThreadTaskScheduler : TaskScheduler {
		private readonly LinkedList<Task> _pendingTasks = [];
		private readonly LinkedList<Action> _continuations = [];
		private readonly Lock _orderedTaskLock = new Lock();

		/// <summary>
		/// Used by <see cref="TryExecuteTaskInline(Task, bool)"/>, to serve its one purpose.
		/// </summary>
		protected abstract bool IsOnCorrectThread { get; }

		/// <summary>
		/// A default method to run the provided <paramref name="action"/> on this scheduler. This automates some parameters for you.
		/// </summary>
		/// <param name="action">A method to run as a <see cref="Task"/>.</param>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> which can be used to stop the work before it begins.</param>
		/// <returns></returns>
		public Task Run(Action action, CancellationToken cancellationToken) {
			return Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.DenyChildAttach | TaskCreationOptions.HideScheduler, this);
		}

		// The Conservatory specific, no use here.
		#if false
		/// <summary>
		/// A default method to run the provided <paramref name="function"/> on this scheduler. This automates some parameters for you.
		/// Note that due to how <see cref="SynchronousTask"/> works, the returned <see cref="Task{TResult}"/> will block if you attempt
		/// to get the result. The returned <see cref="Task{TResult}"/> completes when the <see cref="SynchronousTask"/> does.
		/// </summary>
		/// <param name="function">A method which returns <see cref="SynchronousTask"/>.</param>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> which can be used to stop the work before it begins.</param>
		/// <returns></returns>
		public Task<SynchronousTask> Run(Func<SynchronousTask> function, CancellationToken cancellationToken) {
			return Task.Factory.StartNew(function, cancellationToken, TaskCreationOptions.DenyChildAttach | TaskCreationOptions.HideScheduler, this);
		}

		/// <summary>
		/// A default method to run the provided <paramref name="function"/> on this scheduler. This automates some parameters for you.
		/// Note that due to how <see cref="SynchronousTask"/> works, the returned <see cref="Task{TResult}"/> will block if you attempt
		/// to get the result. The returned <see cref="Task{TResult}"/> completes when the <see cref="SynchronousTask"/> does.
		/// </summary>
		/// <param name="function">A method which returns <see cref="SynchronousTask"/>.</param>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> which can be used to stop the work before it begins.</param>
		/// <returns></returns>
		public Task<SynchronousTask<TResult>> Run<TResult>(Func<SynchronousTask<TResult>> function, CancellationToken cancellationToken) {
			return Task.Factory.StartNew(function, cancellationToken, TaskCreationOptions.DenyChildAttach | TaskCreationOptions.HideScheduler, this);
		}
		#endif

		internal void QueueContinuation(Action continuation) {
			lock (_orderedTaskLock) {
				_continuations.AddLast(continuation);
			}
		}

		/// <inheritdoc/>
		protected override void QueueTask(Task task) {
			if (IsOnCorrectThread) {
				TryExecuteTask(task); // Yes, this is legal.
			} else {
				lock (_orderedTaskLock) {
					_pendingTasks.AddLast(task);
				}
			}
		}

		/// <inheritdoc/>
		protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) {
			if (IsOnCorrectThread) {
				if (taskWasPreviouslyQueued) {
					_pendingTasks.Remove(task);
				}
				TryExecuteTask(task);
			}
			return false;
		}

		/// <inheritdoc/>
		protected override IEnumerable<Task>? GetScheduledTasks() {
			if (_orderedTaskLock.TryEnter()) {
				try {
					return _pendingTasks;
				} finally {
					_orderedTaskLock.Exit();
				}
			}
			throw new NotSupportedException("The lock is not available right now, so tasks cannot be viewed.");
		}


		/// <summary>
		/// Performs all tasks.
		/// </summary>
		internal bool FlushImpl() {
			if (!IsOnCorrectThread) throw new InvalidOperationException("Illegal attempt to flush pending tasks on the wrong thread.");

			Task[] tasks;
			Action[] continuations;
			lock (_orderedTaskLock) {
				tasks = _pendingTasks.ToArray();
				_pendingTasks.Clear();

				continuations = _continuations.ToArray();
				_continuations.Clear();
			}

			bool didAnything = false;
			for (int i = 0; i < tasks.Length; i++) {
				TryExecuteTask(tasks[i]);
				didAnything = true;
			}
			for (int i = 0; i < continuations.Length; i++) {
				continuations[i]();
				didAnything = true;
			}
			return didAnything;
		}
	}
}
