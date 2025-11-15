#nullable disable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UTSTwitchIntegration.Config;
using UTSTwitchIntegration.Models;

namespace UTSTwitchIntegration.Queue
{
    /// <summary>
    /// Queue for managing viewers waiting to spawn
    /// </summary>
    public class ViewerQueue
    {
        private readonly ConcurrentQueue<ViewerInfo> _queue;
        private readonly HashSet<string> _usernamesInQueue;

        /// <summary>
        /// Number of viewers currently in queue
        /// </summary>
        public int Count => _queue.Count;

        public ViewerQueue()
        {
            _queue = new ConcurrentQueue<ViewerInfo>();
            _usernamesInQueue = new HashSet<string>();
        }

        /// <summary>
        /// Add a viewer to the queue
        /// </summary>
        /// <param name="username">Twitch username</param>
        /// <param name="role">User's permission level</param>
        /// <returns>True if added, false if already in queue</returns>
        public bool Enqueue(string username, PermissionLevel role)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            string normalizedUsername = username.ToLowerInvariant();

            lock (_usernamesInQueue)
            {
                if (_usernamesInQueue.Contains(normalizedUsername))
                {
                    return false; // Already in queue
                }

                try
                {
                    ViewerInfo viewerInfo = new ViewerInfo(username, role);
                    _queue.Enqueue(viewerInfo);
                    _usernamesInQueue.Add(normalizedUsername);
                    return true;
                }
                catch (System.Exception)
                {
                    _usernamesInQueue.Remove(normalizedUsername);
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
            if (_queue.TryDequeue(out ViewerInfo viewer))
            {
                lock (_usernamesInQueue)
                {
                    if (viewer != null && !string.IsNullOrWhiteSpace(viewer.Username))
                    {
                        string normalizedUsername = viewer.Username.ToLowerInvariant();
                        _usernamesInQueue.Remove(normalizedUsername);
                    }
                }
                return viewer;
            }

            return null;
        }

        public ViewerInfo DequeueRandom()
        {
            // TODO: Make random selection more efficient
            lock (_usernamesInQueue)
            {
                if (_queue.Count == 0)
                {
                    return null;
                }

                List<ViewerInfo> viewers = new List<ViewerInfo>();
                while (_queue.TryDequeue(out ViewerInfo viewer))
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
                    _queue.Enqueue(viewer);
                }

                // Remove selected viewer from tracking set
                if (selectedViewer != null && !string.IsNullOrWhiteSpace(selectedViewer.Username))
                {
                    string normalizedUsername = selectedViewer.Username.ToLowerInvariant();
                    _usernamesInQueue.Remove(normalizedUsername);
                }

                return selectedViewer;
            }
        }

        /// <summary>
        /// Check if a username is already in the queue
        /// </summary>
        /// <param name="username">Username to check</param>
        /// <returns>True if username is in queue</returns>
        public bool Contains(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            string normalizedUsername = username.ToLowerInvariant();
            lock (_usernamesInQueue)
            {
                return _usernamesInQueue.Contains(normalizedUsername);
            }
        }

        /// <summary>
        /// Clear all viewers from queue
        /// </summary>
        public void Clear()
        {
            while (_queue.TryDequeue(out _))
            {
            }

            lock (_usernamesInQueue)
            {
                _usernamesInQueue.Clear();
            }
        }
    }
}

