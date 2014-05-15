using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Mono.Options;
using Microsoft.Web.Administration;

namespace iismanager
{
	class Program
	{
		private static Site _newSite;
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
			if (_newSite != null)
				_newSite.Stop();

			return true;
		}

		static void Main(string[] args)
		{
			SetConsoleCtrlHandler(ConsoleCtrlCheck, true);

			string siteName = "";
			int port = -1;
			string framework = "";
			bool showHelp = false;
			var p = new OptionSet {
            { "s|site=", "name of the iis site",
              v => siteName = v},
            { "p|port=", "port used by the iis site",
              (int v) => port = v},
            { "f|framework=", "framework used by the iis site: v2.0 or v4.0",
              v => framework = v},
            { "h|help",  "show this message and exit", 
              v => showHelp = true }
			};

			try
			{
				p.Parse(args);
			}
			catch (Exception)
			{
				p.WriteOptionDescriptions(Console.Out);
				return;
			}
			if (showHelp || siteName == "" || port == -1 || framework == "")
			{
				p.WriteOptionDescriptions(Console.Out);
				return;
			}

            string currentPath = Directory.GetCurrentDirectory();
			
			var serverManager = new ServerManager();
			if (serverManager.ApplicationPools.Any(ap => ap.Name == siteName))
				serverManager.ApplicationPools.Remove(serverManager.ApplicationPools.Single(ap => ap.Name == siteName));
			var applicationPool = serverManager.ApplicationPools.Add(siteName);			
			applicationPool.ManagedRuntimeVersion = framework;

			if (serverManager.Sites.Any(si => si.Name == siteName))
				serverManager.Sites.Remove(serverManager.Sites.Single(si => si.Name == siteName));
			_newSite = serverManager.Sites.Add(siteName, currentPath, port);
			_newSite.ServerAutoStart = true;
			_newSite.Applications[0].ApplicationPoolName = siteName;
			serverManager.CommitChanges();
			System.Threading.Thread.Sleep(500);
			_newSite.Start();

			do
			{
				if (_newSite.State != ObjectState.Starting && _newSite.State != ObjectState.Started)
				{
					_newSite.Stop();
					return;					
				}
				System.Threading.Thread.Sleep(5000);
			} while (true);
		}
	}
}
