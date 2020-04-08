using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Async
{
    public class ExceptionsTests
    {
        [Fact]
        public async Task ThrowExceptionInAwaitedTask_ExceptionCanBeCatchedAndHasRightMessage()
        {
            var catched = false;
            try
            {
                await ThrowException();
            }
            catch (Exception e)
            {
                catched = true;
                Assert.Equal("My Exception", e.Message);
            }

            Assert.True(catched);
        }

        [Fact]
        public Task ThrowExceptionInNotAwaitedTask_ExceptionCanBeCatchedAndHasRightMessage()
        {
            var catched = false;
            try
            {
                ThrowException();
            }
            catch (Exception e)
            {
                catched = true;
                Assert.Equal("My Exception", e.Message);
            }

            Assert.True(catched);

            return Task.CompletedTask;
        }

        [Fact]
        public async Task ThrowDelayedExceptionInAwaitedTask_ExceptionCanBeCatchedAndHasRightMessage()
        {
            var catched = false;
            try
            {
                await ThrowDelayedException();
            }
            catch (Exception e)
            {
                catched = true;
                Assert.Equal("My Exception", e.Message);
            }

            Assert.True(catched);
        }

        [Fact]
        public Task ThrowDelayedExceptionInNotAwaitedTaskAndProgramFinishesBeforeExceptionIsThrown_NoExceptionThrown()
        {
            try
            {
                _ = ThrowDelayedException();
            }
            catch
            {
                throw new Exception("Wrong behaviour");
            }

            return Task.CompletedTask;
        }

        [Fact]
        public async Task ThrowDelayedExceptionInNotAwaitedTaskAndProgramFinishesAfterExceptionIsThrown_NoExceptionThrownAndTaskIsFaultedWithAggregateException()
        {
            var task = ThrowDelayedException();
            await Task.Delay(TimeSpan.FromSeconds(3));

            Assert.True(task.IsFaulted);
            Assert.IsType<AggregateException>(task.Exception);
            Assert.Equal("My Exception", task.Exception.InnerException.Message);
        }

        [Fact]
        public async Task ThrowDelayedExceptionInNotAwaitedTaskAndAwaitItAfterItsFaulted_OriginalExceptionIsThrownOnAwait()
        {
            var task = ThrowDelayedException();
            await Task.Delay(TimeSpan.FromSeconds(3));

            Assert.True(task.IsFaulted);

            var catched = false;
            try
            {
                await task;
            }
            catch (InvalidOperationException e)
            {
                catched = true;
                Assert.Equal("My Exception", e.Message);
            }

            Assert.True(catched);
        }

        [Fact]
        public async Task ThrowDelayedExceptionOnATaskAssignedToVariableAndAwaitedLaterOn_ExceptionIsCatchedOnAwait()
        {
            var catched = false;
            var task = ThrowDelayedException();

            try
            {
                await task;
            }
            catch (Exception e)
            {
                catched = true;
                Assert.Equal("My Exception", e.Message);
            }

            Assert.True(catched);
        }

        [Fact]
        public async Task ThrowDelayedExceptionInNonAwaitedTaskAndExceptionThrowsBeforeProgramFinishes_AggregateExceptionIsThrownSilently()
        {
            Task task;
            try
            {
                task = ThrowDelayedException();
            }
            catch
            {
                throw new Exception("Wrong behaviour");
            }

            await Task.Delay(2000);

            Assert.True(task.IsCompleted);
            Assert.False(task.IsCompletedSuccessfully);
            Assert.True(task.IsFaulted);
            Assert.IsType<AggregateException>(task.Exception);
            Assert.Equal("My Exception", task.Exception.InnerException.Message);
        }

        [Fact]
        public async Task CancelDelayInANonAwaitedTask_TaskCanceledExceptionIsCatchedOnAwait()
        {
            var catched = false;

            using var source = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), source.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        catched = true;
                    }
                });

            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.True(catched);
        }

        [Fact]
        public async Task CancelDelayInAwaitedTask_TaskCanceledExceptionIsCatchedOnAwait()
        {
            var catched = false;

            using var source = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            try
            {
                await Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), source.Token);
                });
            }
            catch (TaskCanceledException)
            {
                catched = true;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.True(catched);
        }

        [Fact]
        public async Task CancelTokenGivenToAwaitedTaskRun_NoExceptionIsThrown()
        {
            using var source = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            try
            {
                await Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(4));
                }, source.Token);
            }
            catch (Exception)
            {
                throw new Exception("Wrong behaviour");
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        [Fact]
        public async Task PreCancelledTokenGivenToTaskRun_TaskCanceledExceptionThrown()
        {
            using var source = new CancellationTokenSource();
            source.Cancel();

            var cached = false;

            try
            {
                await Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(4));
                }, source.Token);
            }
            catch (TaskCanceledException)
            {
                cached = true;
            }

            Assert.True(cached);
        }

        [Fact]
        public async Task WhenAllUsedAndOneTaskThrowsException_WhenAllWaitsForOtherTaskToFinishAndSameExceptionIsThrown()
        {
            var task1Finished = false;
            var task1 = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(4));
                task1Finished = true;
            });

            var task2 = ThrowDelayedException();

            var catched = false;

            try
            {
                await Task.WhenAll(task1, task2);
            }
            catch (InvalidOperationException e)
            {
                catched = true;
                Assert.Equal("My Exception", e.Message);
            }

            Assert.True(catched);
            Assert.True(task1Finished);
        }

        [Fact]
        public async Task WhenAllUsedAndTwoTasksThrowExceptions_FirstTasksExceptionIsThrownAndWhenAllTaskContainsAllExceptions()
        {
            var task1 = ThrowDelayedException(4, "Exception 1");
            var task2 = ThrowDelayedException(2, "Exception 2");

            var catched = false;
            Task whenAllTask = null;

            try
            {
                whenAllTask = Task.WhenAll(task1, task2);
                await whenAllTask;
            }
            catch (InvalidOperationException e)
            {
                catched = true;
                Assert.Equal("Exception 1", e.Message);
            }

            Assert.True(whenAllTask.IsCompleted);
            Assert.False(whenAllTask.IsCompletedSuccessfully);
            Assert.True(whenAllTask.IsFaulted);
            Assert.IsType<AggregateException>(whenAllTask.Exception);
            Assert.Equal(2, whenAllTask.Exception.InnerExceptions.Count);
            Assert.True(catched);
        }

        private Task ThrowException()
        {
            throw new InvalidOperationException("My Exception");
        }

        private async Task ThrowDelayedException(int seconds = 1, string message = "My Exception")
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            throw new InvalidOperationException(message);
        }
    }
}