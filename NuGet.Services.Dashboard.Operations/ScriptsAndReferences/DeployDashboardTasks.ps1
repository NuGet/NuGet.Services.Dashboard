

param( 
  [Parameter(Mandatory=$true)][string]$FrontEndStorageConnectionString,
  [Parameter(Mandatory=$true)][string]$FrontEndLegacyDBConnectionString,
  [Parameter(Mandatory=$true)][string]$SubscriptionId,
  [Parameter(Mandatory=$false)][string]$FrontEndCloudServiceName="nuget-prod-0-v2gallery",
  [Parameter(Mandatory=$false)][string]$TrafficManagerProfileName="nuget-prod-v2gallery",  
  [Parameter(Mandatory=$true)][string]$WareHouseDBConnectionString,
  [Parameter(Mandatory=$true)][string]$FrontEndLegacyDBConnectionStringForFailOverDC,
  [Parameter(Mandatory=$true)][string]$FrontEndStorageConnectionStringForFailOverDC,
  [Parameter(Mandatory=$false)][string]$ProdManagementCertName="NuGetDash.cer",
  [Parameter(Mandatory=$true)][string]$PingdomUserName,
  [Parameter(Mandatory=$true)][string]$PingdomPassword,
  [Parameter(Mandatory=$true)][string]$PingdomAppKey,
  [Parameter(Mandatory=$false)][string]$SearchServiceEndPoint="https://api-search-0.nuget.org/search/diag",
  [Parameter(Mandatory=$false)][string]$SearchServiceAdminUser="admin",
  [Parameter(Mandatory=$true)][string]$SearchServiceAdminKey,
  [Parameter(Mandatory=$true)][string]$SearchServiceName,
  [Parameter(Mandatory=$true)][string]$WorkServiceEndPoint,
  [Parameter(Mandatory=$true)][string]$WorkServiceAdminUser,
  [Parameter(Mandatory=$true)][string]$WorkServiceAdminKey0,
  [Parameter(Mandatory=$true)][string]$WorkServiceAdminKey1,
  [Parameter(Mandatory=$true)][string]$SchedulerCloudServiceId,
  [Parameter(Mandatory=$true)][string]$SchedulerJobId,
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
CreateTask "CreateDataBaseSizeReport" "cdsrt -ldb  `"$FrontEndLegacyDBConnectionString`" -wdb `"$WareHouseDBConnectionString`"  -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 180

#ElmahError related tasks
CreateTask "CreateElmaherrorOverviewReport" "ceeort -ea `"$FrontEndStorageConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60
CreateTask "CreateElmaherror1HourDetailedReport" "ceedrt -ea `"$FrontEndStorageConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName -n 1" 30
CreateTask "CreateElmaherror6HourDetailedReport" "ceedrt -ea `"$FrontEndStorageConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName -n 6" 30
CreateTask "CreateElmaherror24HourDetailedReport" "ceedrt -ea `"$FrontEndStorageConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName -n 24" 30

#Pingdom Tasks
CreateTask "CreatePingdomHourlyReport" "cpdwr -user $PingdomUserName -password $PingdomPassword -appkey `"$PingdomAppKey`" -frequency Hourly -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60
CreateTask "CreatePackageResponeWeeklyDetailedReport" "cpdrt -user $PingdomUserName -password $PingdomPassword -appkey `"$PingdomAppKey`" -n 7 -id 958101 -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 180
CreateTask "CreatePingdomServiceDisruptionReport" "psdrt -user $PingdomUserName -password $PingdomPassword -appkey `"$PingdomAppKey`" -n 1 -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60

#worker Tasks
CreateTask "RunBackgroundChecksForWorkerJobs" "rbgc -db `"$FrontEndLegacyDBConnectionString`" -iis `"$FrontEndStorageConnectionString`" -name $WorkServiceAdminUser -key $WorkServiceAdminKey0 -url $WorkServiceEndpoint -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60
CreateTask "RunBackgroundCheckForFailoverDC" "rbgfdc -db `"$FrontEndLegacyDBConnectionStringForFailOverDC`" -pst `"$FrontEndStorageConnectionStringForFailOverDC`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60
CreateTask "CreateWorkJobDetailReport" "cwjdrt -name $WorkServiceAdminUser -key0 $WorkServiceAdminKey0 -key1 $WorkServiceAdminKey1 -lid $SubscriptionId -cid $SchedulerCloudServiceId -jid $SchedulerJobId -cername $ProdManagementCertName -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60


#SearchService Tasks
CreateTask "CreateLuceneIndexingLagReport" "csisrt -db `"$FrontEndLegacyDBConnectionString`" -se $SearchServiceEndPoint -sa $SearchServiceAdminUser -sk $SearchServiceAdminKey -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60
CreateTask "CreateSearchCloudServiceReport" "ccsdrt -id $SubscriptionId -name $SearchServiceName -cername $ProdManagementCertName -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 5

#Misc
CreateTask "CreateRequestsCountOverviewReport" "crphrt -iis `"$FrontEndStorageConnectionString`" -retry 3 -servicename $FrontEndCloudServiceName  -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60 
CreateTask "CreateTrafficManagerStatusOverviewReport" "ctmort -id $SubscriptionId -name $TrafficManagerProfileName -cername $ProdManagementCertName -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 5
CreateTask "CreateV2GalleryInstanceCountReport" "ccsdrt -id $SubscriptionId -name $FrontEndCloudServiceName -cername $ProdManagementCertName -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60
CreateTask "CreateVsTrendingReportFor30Day" "cvtrt -db `"$WareHouseDBConnectionString`" -n 30 -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 180
CreateTask "CreateVsTrendingReportFor1Day" "cvtrt -db `"$WareHouseDBConnectionString`" -n 1 -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 180


