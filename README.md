NuGet.Services.Dashboard
========================

This repo contains the source code for NuGet Dashboard frontend website and back end operations.

###NuGet.Services.Dashboard.Operations###

Contains tasks that retrieves monitoring data from various sources like Pingdom, WAD performance data, SQL Azure DMV queries and gallery database.
Each task collects the required data and generates a json report which gets uploaded to the specified blob storage container.

###NuGet.Services.Dashboard.FrontEnd###

Contains the Web application that displays the monitoring data. The Website reads the json report created by the backend tasks and displays them as charts and tables.

###NuGet.Services.Dashboard.Operations.Tools###

Contains the commandline runner (galops.exe) that is used to invoke invidiual dashboard tasks.
