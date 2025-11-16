using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UTSTwitchIntegration.Models;

namespace UTSTwitchIntegration.Queue
{
    /// <summary>
    /// Queue for managing viewers waiting to spawn
    /// </summary>
    public class ViewerQueue
    {
        private readonly ConcurrentQueue<ViewerInfo> queue = new ConcurrentQueue<ViewerInfo>();
        private readonly HashSet<string> usernamesInQueue = new HashSet<string>();

        /// <summary>
        /// Number of viewers currently in queue
        /// </summary>
        public int Count => this.queue.Count;

        /// <summary>
        /// Add a viewer to the queue
        /// </summary>
        /// <param name="username">Twitch username</param>
        /// <returns>True if added, false if already in queue</returns>
        public bool Enqueue(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            string normalizedUsername = username.ToLowerInvariant();

            lock (this.usernamesInQueue)
            {
                if (this.usernamesInQueue.Contains(normalizedUsername))
                {
                    return false;
                }

                try
                {
                    ViewerInfo viewerInfo = new ViewerInfo(username);
                    this.queue.Enqueue(viewerInfo);
                    this.usernamesInQueue.Add(normalizedUsername);
                    return true;
                }
                catch (Exception)
                {
                    this.usernamesInQueue.Remove(normalizedUsername);
                    throw;
                }
            }
        }

        /// <summary>
        /// Remove and return the next viewer from queue
        /// </summary>
        /// <returns>ViewerInfo if available, null if queue is empty</returns>
        public ViewerInfo Dequeue()
        {
            if (!this.queue.TryDequeue(out ViewerInfo viewer))
                return null;

            lock (this.usernamesInQueue)
            {
                if (viewer == null || string.IsNullOrWhiteSpace(viewer.Username))
                    return viewer;

                string normalizedUsername = viewer.Username.ToLowerInvariant();
                this.usernamesInQueue.Remove(normalizedUsername);
            }

            return viewer;
        }

        public ViewerInfo DequeueRandom()
        {
            // TODO: Make random selection more efficient
            lock (this.usernamesInQueue)
            {
                if (this.queue.Count == 0)
                {
                    return null;
                }

                List<ViewerInfo> viewers = new List<ViewerInfo>();
                while (this.queue.TryDequeue(out ViewerInfo viewer))
                {
                    viewers.Add(viewer);
                }

                if (viewers.Count == 0)
                {
                    return null;
                }

                Random random = new Random();
                int randomIndex = random.Next(viewers.Count);
                ViewerInfo selectedViewer = viewers[randomIndex];

                viewers.RemoveAt(randomIndex);

                foreach (ViewerInfo viewer in viewers)
                {
                    this.queue.Enqueue(viewer);
                }

                // Remove selected viewer from tracking set
                if (selectedViewer == null || string.IsNullOrWhiteSpace(selectedViewer.Username))
                    return selectedViewer;

                string normalizedUsername = selectedViewer.Username.ToLowerInvariant();
                this.usernamesInQueue.Remove(normalizedUsername);

                return selectedViewer;
            }
        }

        /// <summary>
        /// Clear all viewers from queue
        /// </summary>
        public void Clear()
        {
            while (this.queue.TryDequeue(out _))
            {
            }

            lock (this.usernamesInQueue)
            {
                this.usernamesInQueue.Clear();
            }
        }
    }
}