using System;
using System.Linq;
using Mono.Options;
using Microsoft.Web.Administration;

namespace iismanager
{
	class Program
	{
		private static Site _newSite;

		protected static void ConsoleCancel(object sender, ConsoleCancelEventArgs args)
		{
			if (_newSite != null)
				_newSite.Stop();
		}

		static void Main(string[] args)
		{
			Console.Clear();
			Console.CancelKeyPress += ConsoleCancel;

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

			string currentPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			
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
