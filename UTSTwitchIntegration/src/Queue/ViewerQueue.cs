using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UTSTwitchIntegration.Config;
using UTSTwitchIntegration.Models;

namespace UTSTwitchIntegration.Queue
{
    /// <summary>
    /// Queue for managing viewers waiting to spawn using shuffled rounds
    /// </summary>
    public class ViewerQueue
    {
        private readonly List<ViewerInfo> allViewers = new List<ViewerInfo>();
        private readonly ConcurrentDictionary<string, byte> spawnedUsernames = new ConcurrentDictionary<string, byte>();
        private readonly ConcurrentDictionary<string, byte> usernamesInList = new ConcurrentDictionary<string, byte>();
        private int[] roundOrder;
        private int currentOrderIndex;
        private bool isRoundActive;
        private QueueSelectionMethod selectionMethod = QueueSelectionMethod.Random;
        private int cachedCount;

        private static readonly ThreadLocal<Random> threadLocalRandom = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

        private readonly object lockObject = new object();

        /// <summary>
        /// Number of viewers in the pool (lock-free read)
        /// </summary>
        public int Count => Interlocked.CompareExchange(ref this.cachedCount, 0, 0);

        public void SetSelectionMethod(QueueSelectionMethod method)
        {
            lock (this.lockObject)
            {
                this.selectionMethod = method;
                this.isRoundActive = false;
            }
        }

        /// <summary>
        /// Add a viewer to the permanent list
        /// </summary>
        /// <param name="username">Twitch username</param>
        /// <returns>True if added, false if already in list or currently spawned</returns>
        public bool Enqueue(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            string normalizedUsername = username.ToLowerInvariant();

            if (this.usernamesInList.ContainsKey(normalizedUsername))
            {
                return false;
            }

            lock (this.lockObject)
            {
                if (this.usernamesInList.ContainsKey(normalizedUsername))
                {
                    return false;
                }

                try
                {
                    ViewerInfo viewerInfo = new ViewerInfo(username);
                    this.allViewers.Add(viewerInfo);
                    this.usernamesInList.TryAdd(normalizedUsername, 0);
                    Interlocked.Increment(ref this.cachedCount);
                    return true;
                }
                catch (Exception)
                {
                    this.usernamesInList.TryRemove(normalizedUsername, out _);
                    throw;
                }
            }
        }

        /// <summary>
        /// Get the next available viewer from the pool
        /// </summary>
        /// <returns>ViewerInfo if available, null if all are spawned or pool is empty</returns>
        public ViewerInfo GetNext()
        {
            lock (this.lockObject)
            {
                if (this.allViewers.Count == 0)
                {
                    return null;
                }

                if (!this.isRoundActive || this.roundOrder == null || this.currentOrderIndex >= this.roundOrder.Length)
                {
                    StartNewRound();
                }

                int attempts = 0;
                int startOrderIndex = this.currentOrderIndex;

                while (attempts < this.roundOrder?.Length)
                {
                    int dataIndex = this.roundOrder[this.currentOrderIndex];
                    ViewerInfo viewer = this.allViewers[dataIndex];
                    this.currentOrderIndex++;

                    if (this.currentOrderIndex >= this.roundOrder.Length)
                    {
                        this.currentOrderIndex = 0;
                    }

                    if (viewer != null && !string.IsNullOrWhiteSpace(viewer.Username))
                    {
                        string normalized = viewer.Username.ToLowerInvariant();
                        if (!this.spawnedUsernames.ContainsKey(normalized))
                        {
                            return viewer;
                        }
                    }

                    attempts++;

                    if (this.currentOrderIndex == startOrderIndex && attempts > 0)
                    {
                        break;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Mark a username as in-use (called when assigned to a customer)
        /// </summary>
        /// <param name="username">Username that was assigned</param>
        public void MarkUsernameInUse(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            string normalizedUsername = username.ToLowerInvariant();
            this.spawnedUsernames.TryAdd(normalizedUsername, 0);
        }

        /// <summary>
        /// Mark a username as available again (called when customer leaves)
        /// </summary>
        /// <param name="username">Username that became available</param>
        public void MarkUsernameAvailable(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            string normalizedUsername = username.ToLowerInvariant();
            this.spawnedUsernames.TryRemove(normalizedUsername, out _);
        }

        /// <summary>
        /// Check if a username is currently available (not spawned)
        /// </summary>
        /// <param name="username">Username to check</param>
        /// <returns>True if available, false if in use or not in list</returns>
        public bool IsUsernameAvailable(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            string normalizedUsername = username.ToLowerInvariant();

            if (!this.usernamesInList.ContainsKey(normalizedUsername))
            {
                return false;
            }

            return !this.spawnedUsernames.ContainsKey(normalizedUsername);
        }

        /// <summary>
        /// Start a new round (shuffle indices for random, reset index for FIFO)
        /// </summary>
        private void StartNewRound()
        {
            if (this.allViewers.Count == 0)
            {
                this.isRoundActive = false;
                this.roundOrder = null;
                return;
            }

            int count = this.allViewers.Count;

            this.roundOrder = new int[count];
            for (int i = 0; i < count; i++)
            {
                this.roundOrder[i] = i;
            }

            if (this.selectionMethod == QueueSelectionMethod.Random)
            {
                Random random = threadLocalRandom.Value;
                for (int i = count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (this.roundOrder[i], this.roundOrder[j]) = (this.roundOrder[j], this.roundOrder[i]);
                }
            }

            this.currentOrderIndex = 0;
            this.isRoundActive = true;
        }

        /// <summary>
        /// Remove and return the next viewer from queue (legacy method for compatibility)
        /// </summary>
        /// <returns>ViewerInfo if available, null if queue is empty</returns>
        [Obsolete("Use GetNext() instead")]
        public ViewerInfo Dequeue()
        {
            return this.GetNext();
        }

        /// <summary>
        /// Remove and return a random viewer from queue (legacy method for compatibility)
        /// </summary>
        /// <returns>ViewerInfo if available, null if queue is empty</returns>
        [Obsolete("Use GetNext() instead - selection method is configured via SetSelectionMethod()")]
        public ViewerInfo DequeueRandom()
        {
            return this.GetNext();
        }

        /// <summary>
        /// Clear all viewers from queue
        /// </summary>
        public void Clear()
        {
            lock (this.lockObject)
            {
                this.allViewers.Clear();
                this.roundOrder = null;
                this.currentOrderIndex = 0;
                this.isRoundActive = false;
            }

            this.spawnedUsernames.Clear();
            this.usernamesInList.Clear();
            Interlocked.Exchange(ref this.cachedCount, 0);
        }
    }
}