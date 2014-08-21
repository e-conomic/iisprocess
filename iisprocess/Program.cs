using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Mono.Options;
using Microsoft.Web.Administration;
using System.Diagnostics;

namespace iisprocess
{
	class Program
	{
		private static Site _newSite;
        private static Boolean debug = false;

        private const int CHECK_DELAY_MS = 2000;

		[DllImport("Kernel32")]
		public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);
		public delegate bool HandlerRoutine(CtrlTypes CtrlType);

		public enum CtrlTypes
		{
			CTRL_C_EVENT = 0,
			CTRL_BREAK_EVENT = 1,
			CTRL_CLOSE_EVENT = 2,
			CTRL_LOGOFF_EVENT = 5,
			CTRL_SHUTDOWN_EVENT = 6
		}

		private static bool ConsoleCtrlCheck(CtrlTypes ctrlType)
		{
            if (_newSite != null) {
                Debug("Ctrl-C: Stopping site");
                _newSite.Stop();
            }
			return true;
		}

        private static void Debug(string format, params object[] args) {
            if (debug)
            {
                var prefix = String.Format("{0}: ", Process.GetCurrentProcess().Id);
                Console.WriteLine(prefix + format, args);
            }
        }

        private static Site CreateSite(string siteName, string framework, string path, int port, ProcessModelIdentityType identityType) {
            var serverManager = new ServerManager();
            if (serverManager.ApplicationPools.Any(ap => ap.Name == siteName)) {
                Debug("Removing existing AppPool {0}", siteName);
                serverManager.ApplicationPools.Remove(serverManager.ApplicationPools.Single(ap => ap.Name == siteName));
            }

            Debug("Creating new AppPool {0}, Framework={1}, IdentityType={2}", siteName, framework, identityType);
    
            var applicationPool = serverManager.ApplicationPools.Add(siteName);
            applicationPool.ManagedRuntimeVersion = framework;
            applicationPool.Enable32BitAppOnWin64 = true;
            applicationPool.ProcessModel.IdentityType = identityType;

            if (serverManager.Sites.Any(si => si.Name == siteName)) {
                Debug("Removing existing site {0}", siteName);
                serverManager.Sites.Remove(serverManager.Sites.Single(si => si.Name == siteName));
            }
            Debug("Creating new Site {0}, Path={1}, Port={2}", siteName, path, port);

            var newSite = serverManager.Sites.Add(siteName, path, port);
            newSite.ServerAutoStart = true;
            newSite.Applications[0].ApplicationPoolName = siteName;
            serverManager.CommitChanges();
            System.Threading.Thread.Sleep(500);
            Debug("Starting site");
            newSite.Start();
            return newSite;
        }

        private static void StopSite(string siteName)
        {
            var serverManager = new ServerManager();

            try
            {
                var site = serverManager.Sites.First(si => si.Name == siteName);
                Debug("Stopping site {0}", siteName);
                site.Stop();
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to stop site {0}: {1}", siteName, e.Message);
            }
        }

        private static void WatchSite(Site site) {
    	    do {
                var state = site.State;
                Debug("Current site state: {0}", state);
				if (state != ObjectState.Starting && state != ObjectState.Started)
				{
                    Debug("Stopping site");
					site.Stop();
					return;					
				}
                System.Threading.Thread.Sleep(CHECK_DELAY_MS);
			} while (true);
        }

        static void Main(string[] args)
		{
			SetConsoleCtrlHandler(ConsoleCtrlCheck, true);

			var siteName = "";
			var port = -1;
			var framework = "v4.0";
			var showHelp = false;
            var stop = false;
            var parentPID = -1;
            var identityType = ProcessModelIdentityType.ApplicationPoolIdentity;

			var p = new OptionSet {
            { "n|name=", "name of the IIS site",
              v => siteName = v},
            { "s|stop", "stop the IIS site",
              v => stop = true},
            { "p|port=", "port used by the iis site",
              (int v) => port = v},
            { "f|framework=", "framework version used by the application pool: v2.0 or v4.0 (default)",
              v => framework = v},
            { "h|help",  "show this message and exit", 
              v => showHelp = true },
            { "d|debug",  "output debug messages", 
              v => debug = true },
            { "i|identity=",  "The IdentityType to use for Application pool",
                i =>
                    {
                    if (!ProcessModelIdentityType.TryParse(i, true, out identityType))
                        {
                        Console.Error.WriteLine("Cannot parse identity: {0}", i);
                            throw new ArgumentException("identity");
                        }   
                    }
            }};

			try
			{
				p.Parse(args);
			}
			catch (Exception e)
			{
				p.WriteOptionDescriptions(Console.Out);
				return;
			}

			if (showHelp || siteName == "")
			{
				p.WriteOptionDescriptions(Console.Out);
				return;
			}

            if (stop)
            {
                StopSite(siteName);
                return;
            }

            if (port < 0)
            {
                p.WriteOptionDescriptions(Console.Out);
                return;
            }

            string currentPath = Directory.GetCurrentDirectory();

	        _newSite = CreateSite(siteName, framework, currentPath, port, identityType);
            WatchSite(_newSite);
		}
	}
}
