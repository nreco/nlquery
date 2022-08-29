using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace NReco.NLQuery.Examples.NliDataFilter
{
	public class MvcApplication : System.Web.HttpApplication
	{
		protected void Application_Start()
		{
			AreaRegistration.RegisterAllAreas();
			RouteConfig.RegisterRoutes(RouteTable.Routes);
			// workaround for SQLite DLL load issue under IISExpress
			Environment.SetEnvironmentVariable("PreLoadSQLite_NoSearchForDirectory", "true");
			Environment.SetEnvironmentVariable("PreLoadSQLite_BaseDirectory", System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"bin") );			
		}
	}
}
