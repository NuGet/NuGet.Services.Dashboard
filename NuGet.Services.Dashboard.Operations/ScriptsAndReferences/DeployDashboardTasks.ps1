
param(
  [Parameter(Mandatory=$true)][string]$FrontEndDeploymentId,
  [Parameter(Mandatory=$true)][string]$FrontEndStorageConnectionString,
  [Parameter(Mandatory=$true)][string]$FrontEndLegacyDBConnectionString,
  [Parameter(Mandatory=$true)][string]$SubscriptionId,
  [Parameter(Mandatory=$false)][string]$FrontEndCloudServiceName="nuget-prod-0-v2gallery",
  [Parameter(Mandatory=$true)][string]$PrimaryDBConnectionString,
  [Parameter(Mandatory=$true)][string]$WareHouseDBConnectionString,
  [Parameter(Mandatory=$true)][string]$FrontEndLegacyDBConnectionStringForFailOverDC,
  [Parameter(Mandatory=$true)][string]$FrontEndStorageConnectionStringForFailOverDC,
  [Parameter(Mandatory=$false)][string]$ProdManagementCertName="bhuvak-dashboard.cer",
  [Parameter(Mandatory=$true)][string]$PingdomUserName,
  [Parameter(Mandatory=$true)][string]$PingdomPassword,
  [Parameter(Mandatory=$true)][string]$PingdomAppKey,
  [Parameter(Mandatory=$false)][string]$SearchServiceEndPoint="https://api-search-0.nuget.org/search/diag",
  [Parameter(Mandatory=$false)][string]$SearchServiceAdminUser="admin",
  [Parameter(Mandatory=$true)][string]$SearchServiceAdminKey,
  [Parameter(Mandatory=$true)][string]$DashboardStorageConnectionString,
  [Parameter(Mandatory=$true)][string]$DashboardStorageContainerName,
  [Parameter(Mandatory=$true)][string]$WorkingDir,
  [Parameter(Mandatory=$true)][string]$CurrentUserPassword,
  [Parameter(Mandatory=$false)][string]$EnvName="Prod0")

 $time = [System.DateTime]::Now
 $time = $time.AddMinutes(-($time.Minute))

 function CreateTask()
{
param([string]$taskName, [string]$argument, [int]$interval)

$settings = New-ScheduledTaskSettingsSet -MultipleInstances P
$Action = New-ScheduledTaskAction -Execute "$WorkingDir\galops.exe" -WorkingDirectory $WorkingDir -Argument  $argument
$trigger = New-ScheduledTaskTrigger -Once -At  $time  -RepetitionDuration  ([Timespan]::MaxValue) -RepetitionInterval (New-TimeSpan -Minutes $interval)
Unregister-ScheduledTask -TaskName $taskName -TaskPath "\NuGetDashboard\$EnvName\" -ErrorAction SilentlyContinue -Confirm:$false
Register-ScheduledTask -TaskName $taskName -TaskPath "\NuGetDashboard\$EnvName\" -User $env:USERDOMAIN\$env:USERNAME -Password $CurrentUserPassword -Action $Action -Trigger $trigger -Settings $settings
}


# Database related tasks
CreateTask "CreateDataBaseOverviewReport" "cdrt -db `"$FrontEndLegacyDBConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 30
CreateTask "CreateDataBase1HourDetailedReport" "cddrt -db `"$FrontEndLegacyDBConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName -n 1" 30
CreateTask "CreateDataBase6HourDetailedReport" "cddrt -db `"$FrontEndLegacyDBConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName -n 6" 30
CreateTask "CreateDataBase24HourDetailedReport" "cddrt -db `"$FrontEndLegacyDBConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName -n 24" 30
CreateTask "CreateTrendingOverviewReport" "cshr -db `"$FrontEndLegacyDBConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60
CreateTask "CreateDataSizeReport" "cdsrt -ldb  `"$FrontEndLegacyDBConnectionString`" -wdb `"$WareHouseDBConnectionString`"  -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 1440

#ElmahError related tasks
CreateTask "CreateElmaherrorOverviewReport" "ceeort -ea `"$FrontEndStorageConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60
CreateTask "CreateElmaherror1HourDetailedReport" "ceedrt -ea `"$FrontEndStorageConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName -n 1" 30
CreateTask "CreateElmaherror6HourDetailedReport" "ceedrt -ea `"$FrontEndStorageConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName -n 6" 30
CreateTask "CreateElmaherror24HourDetailedReport" "ceedrt -ea `"$FrontEndStorageConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName -n 24" 30

#Pingdom Tasks
CreateTask "CreatePingdomHourlyReport" "cpdwr -user $PingdomUserName -password $PingdomPassword -appkey `"$PingdomAppKey`" -frequency Hourly -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60
CreateTask "CreatePackageResponeWeeklyDetailedReport" "cpdrt -user $PingdomUserName -password $PingdomPassword -appkey `"$PingdomAppKey`" -n 7 -id 958101 -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 1440

#worker Tasks
CreateTask "RunBackgroundChecksForWorkerJobs" "rbgc -db `"$FrontEndLegacyDBConnectionString`" -iis `"$FrontEndStorageConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60
CreateTask "RunBackgroundCheckForFailoverDC" "rbgfdc -db `"$FrontEndLegacyDBConnectionStringForFailOverDC`" -pst `"$FrontEndStorageConnectionStringForFailOverDC`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60


#SearchService Tasks
CreateTask "CreateSearchIndexingLagReport" "csisrt -db `"$FrontEndLegacyDBConnectionString`" -se $SearchServiceEndPoint -sa $SearchServiceAdminUser -sk $SearchServiceAdminKey -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60

#Misc
CreateTask "CreateRequestsCountOverviewReport" "crphrt -di $FrontEndDeploymentId -iis `"$FrontEndStorageConnectionString`" -retry 3 -servicename $FrontEndCloudServiceName  -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60 
CreateTask "CreateV2GalleryInstanceCountReport" "ccsdrt -id $SubscriptionId -name $FrontEndCloudServiceName -cername $ProdManagementCertName -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60



