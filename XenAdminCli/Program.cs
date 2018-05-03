using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
//using System.Windows.Forms;
using System.Threading;
//using System.Drawing;
using XenAdmin.Core;
using XenAdmin.Network;
//using XenAdmin.Dialogs;
using XenAdmin.XenSearch;
using XenAPI;
using System.Runtime.InteropServices;
using XenAdmin.Actions;


namespace XenAdmin
{
	class MainClass
	{
		public const int DEFAULT_WLB_PORT = 8012;

		/// <summary>
		/// Module for authenticating with proxy server using the Basic authentication scheme.
		/// </summary>
		private static IAuthenticationModule BasicAuthenticationModule = null;
		/// <summary>
		/// Module for authenticating with proxy server using the Digest authentication scheme.
		/// </summary>
		private static IAuthenticationModule DigestAuthenticationModule = null;

		/// <summary>
		/// A UUID for the current instance of XenCenter.  Used to identify our own task instances.
		/// </summary>
		public static readonly string XenCenterUUID = Guid.NewGuid().ToString();

		private static NamedPipes.Pipe pipe;
		private const string PIPE_PATH_PATTERN = @"\\.\pipe\XenCenter-{0}-{1}-{2}";


		public static readonly XenAdmin.Core.PropertyManager PropertyManager = new XenAdmin.Core.PropertyManager();


		/// <summary>
		/// The secure hash of the master password used to load the client session.
		/// If this is null then no prior session existed and the user should be prompted
		/// to save his session when the UI is quit.
		/// </summary>
		public static byte[] MasterPassword = null;

		/// <summary>
		/// A true value here indicates the user does not want to save session information for this
		/// particular instance of the UI; but when the UI is restarted he should be prompted again.
		/// </summary>
		public static bool SkipSessionSave = false;

		public static bool RunInAutomatedTestMode = false;
		public static string TestExceptionString = null;  // an exception passed back to the test framework
		private static log4net.ILog log = null;

		public static volatile bool Exiting = false;

		public static readonly string AssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

		public static readonly DateTime ApplicationStartTime = DateTime.Now;

		private static readonly System.Threading.Timer dailyTimer;

		public static bool IsThemed = false;

		public static void Main (string[] args)
		{
				//XenAdminConfigManager.Provider = new WinformsXenAdminConfigProvider();
				// Start timer to record resource usage every 24hrs
				//dailyTimer = new System.Threading.Timer((TimerCallback)delegate(object state)
				//	{
				//		Program.logApplicationStats();
				//	}, null, new TimeSpan(24, 0, 0), new TimeSpan(24, 0, 0));

				log4net.Config.XmlConfigurator.ConfigureAndWatch(new FileInfo(Assembly.GetCallingAssembly().Location + ".config"));
				log = log4net.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);



			//Upgrade settings
			System.Reflection.Assembly a = System.Reflection.Assembly.GetExecutingAssembly();
			Version appVersion = a.GetName().Version;
			string appVersionString = appVersion.ToString();
			log.DebugFormat("Application version of new settings {0}", appVersionString);


	

			// Reset statics, because XenAdminTests likes to call Main() twice.
			TestExceptionString = null;
			Exiting = false;
			// Clear XenConnections and History so static classes like OtherConfigAndTagsWatcher 
			// listening to changes still work when Main is called more than once.
			ConnectionsManager.XenConnections.Clear();
			ConnectionsManager.History.Clear();

			Search.InitSearch(Branding.Search);
			//TreeSearch.InitSearch();

			ArgType argType = ArgType.None;
			AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
//			Application.ThreadException -= Application_ThreadException;
//			Application.ThreadException += Application_ThreadException;
//			Application.EnableVisualStyles();
//			Application.SetCompatibleTextRenderingDefault(false);

//			try
//			{
//				if (SystemInformation.FontSmoothingType == 2) // ClearType
//					TransparentUsually = SystemColors.Window;
//			}
//			catch (NotSupportedException)
//			{
//				// Leave TransparentUsually == Color.Transparent.  This is an old platform
//				// without FontSmoothingType support.
//			}

			// Force the current culture, to make the layout the same whatever the culture of the underlying OS (CA-46983).
			Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture = new CultureInfo(InvisibleMessages.LOCALE, false);

			if (string.IsNullOrEmpty(Thread.CurrentThread.Name))
				Thread.CurrentThread.Name = "Main program thread";

			ServicePointManager.DefaultConnectionLimit = 20;
			ServicePointManager.ServerCertificateValidationCallback = SSL.ValidateServerCertificate;
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
			XenAPI.Session.UserAgent = string.Format("XenCenter/{0}", ClientVersion());
			RememberProxyAuthenticationModules();
			ReconfigureConnectionSettings();

			log.Info("Application started");
			logSystemDetails();
			OptionsDialog.Log();

			if (Args.Length > 0)
				log.InfoFormat("Args[0]: {0}", Args[0]);

			List<string> sanitizedArgs = new List<string>(Args);
			// Remove the '--wait' argument, which may have been passed to the splash screen
			sanitizedArgs.Remove("--wait");

			if (sanitizedArgs.Count > 1)
			{
				argType = ParseFileArgs(sanitizedArgs, out args);

				if (argType == ArgType.Passwords)
				{
					log.DebugFormat("Handling password request using '{0}'", args[0]);
					try
					{
						PasswordsRequest.HandleRequest(args[0]);
					}
					catch (Exception exn)
					{
						log.Fatal(exn, exn);
					}
					Application.Exit();
					return;
				}
			}
			else if (sanitizedArgs.Count == 1 && sanitizedArgs[0] == "messageboxtest")
			{
				new Dialogs.MessageBoxTest().ShowDialog();
				Application.Exit();
				return;
			}
			else if (sanitizedArgs.Count > 0)
			{
				log.Warn("Unrecognised command line options");
			}

			try
			{
				//ConnectPipe();
			}
			catch (System.ComponentModel.Win32Exception exn)
			{
				log.Error("Creating named pipe failed. Continuing to launch XenCenter.", exn);
			}

			Application.ApplicationExit -= Application_ApplicationExit;
			Application.ApplicationExit += Application_ApplicationExit;
		}

		static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
		{
			ProcessUnhandledException(sender, e.Exception, false);
		}

		static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			ProcessUnhandledException(sender, (Exception)e.ExceptionObject, e.IsTerminating);
		}
	}

	public enum ArgType { Import, License, Restore, Update, None, XenSearch, Passwords, Connect };
}
