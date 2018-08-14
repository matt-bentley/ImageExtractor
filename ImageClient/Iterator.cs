using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ImageClient
{
    public class Iterator
    {
        private int _concurrency;
        protected ParallelOptions _parallelOptions;

        public Iterator(int concurrency = 1)
        {
            _concurrency = concurrency;
            _parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = concurrency };
        }

        public void Iterate<T>(IEnumerable<T> items, Action<T> processor)
        {
            var exceptions = new ConcurrentBag<Exception>();

            Parallel.ForEach(items, _parallelOptions, i =>
            {
                try
                {
                    processor(i);
                }
                catch (Exception ex)
                {
                    // MB should the batches be SubClassed so we can override ToString on it? Need to think how batch errors are handled
                    Console.WriteLine($"Error processing {i.ToString()} - {ex.ToString()}");
                    exceptions.Add(new Exception($"Error while processing matches for {i.ToString()}", ex));
                }
            });

            if (exceptions.Count > 0)
            {
                Console.WriteLine($"There were {exceptions.Count} errors processing the data");
                if (items.Count() == exceptions.Count)
                {
                    throw new AggregateException(exceptions);
                }
            }
        }

        public async Task IterateAsync<T>(IEnumerable<T> items, Func<T, Task> processor)
        {
            var exceptions = new Queue<Exception>();
            int nextIndex = 0;
            List<Task> tasks = new List<Task>();
            var itemList = items.ToList();

            // populate task list with number of concurrent tasks
            while (nextIndex < _concurrency && nextIndex < itemList.Count)
            {
                tasks.Add(ProcessItemAsync(itemList[nextIndex], processor, exceptions));
                nextIndex++;
            }

            while (tasks.Count > 0)
            {
                Task task = await Task.WhenAny(tasks);
                tasks.Remove(task);

                // add another item if there are any left
                if (nextIndex < itemList.Count)
                {
                    tasks.Add(ProcessItemAsync(itemList[nextIndex], processor, exceptions));
                    nextIndex++;
                }
            }

            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }
        }

        private async Task ProcessItemAsync<T>(T item, Func<T, Task> processor, Queue<Exception> exceptions)
        {
            try
            {
                await processor(item);
            }
            catch (Exception ex)
            {
                exceptions.Enqueue(new Exception($"Error while processing matches for {item.ToString()}", ex));
            }
        }
    }
}
