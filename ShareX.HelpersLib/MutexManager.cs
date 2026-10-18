#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ShareX.HelpersLib
{
    public class MutexManager : IDisposable
    {
        public bool HasHandle { get; private set; }

        private Mutex mutex;
        private FileStream lockFile;

        public MutexManager(string mutexName) : this(mutexName, Timeout.Infinite)
        {
        }

        public MutexManager(string mutexName, int timeout)
        {
            if (!OperatingSystem.IsWindows())
            {
                lockFile = AcquireLockFile(mutexName, timeout);
                HasHandle = lockFile != null;
                return;
            }

            mutex = new Mutex(false, mutexName);

            try
            {
                HasHandle = mutex.WaitOne(timeout, false);
            }
            catch (AbandonedMutexException)
            {
                HasHandle = true;
            }
        }

        public static bool IsRunning(string mutexName)
        {
            if (!OperatingSystem.IsWindows())
            {
                using (FileStream lockFile = AcquireLockFile(mutexName, 0))
                {
                    return lockFile == null;
                }
            }

            try
            {
                using (Mutex mutex = new Mutex(false, mutexName, out bool createdNew))
                {
                    return !createdNew;
                }
            }
            catch
            {
            }

            return false;
        }

        // Named mutexes on Unix are scoped to the session, and every terminal starts a new one, so a per-user lock file is
        // used instead. The kernel releases the lock when the process exits, even after a crash.
        private static FileStream AcquireLockFile(string name, int timeout)
        {
            string folder = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                folder = Path.Combine(Path.GetTempPath(), "sharex-" + Environment.UserName);
                Directory.CreateDirectory(folder);
            }

            string path = Path.Combine(folder, name + ".lock");
            Stopwatch timer = Stopwatch.StartNew();

            while (true)
            {
                try
                {
                    // FileShare.None takes an exclusive advisory lock on Unix.
                    return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    if (timeout != Timeout.Infinite && timer.ElapsedMilliseconds >= timeout)
                    {
                        return null;
                    }

                    Thread.Sleep(100);
                }
            }
        }

        public void Dispose()
        {
            lockFile?.Dispose();

            if (mutex != null)
            {
                if (HasHandle)
                {
                    mutex.ReleaseMutex();
                }

                mutex.Dispose();
            }
        }
    }
}