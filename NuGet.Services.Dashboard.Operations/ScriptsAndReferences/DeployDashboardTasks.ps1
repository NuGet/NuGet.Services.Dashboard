param( 
  [Parameter(Mandatory=$true)] [string]$FrontEndStorageConnectionString,
  [Parameter(Mandatory=$true)] [string]$FrontEndLegacyDBConnectionString,
  [Parameter(Mandatory=$true)] [string]$SubscriptionId,
  [Parameter(Mandatory=$false)][string]$FrontEndCloudServiceName,
  [Parameter(Mandatory=$false)][string]$TrafficManagerProfileName,
  [Parameter(Mandatory=$false)][string]$ProdManagementCertName,
  [Parameter(Mandatory=$true)] [string]$PingdomUserName,
  [Parameter(Mandatory=$true)] [string]$PingdomPassword,
  [Parameter(Mandatory=$true)] [string]$PingdomAppKey,
  [Parameter(Mandatory=$true)] [string]$DashboardStorageConnectionString,
  [Parameter(Mandatory=$true)] [string]$DashboardStorageContainerName,
  [Parameter(Mandatory=$true)] [string]$WorkingDir,
  [Parameter(Mandatory=$true)] [string]$CurrentUserPassword,
  [Parameter(Mandatory=$false)][string]$EnvName,
  [Parameter(Mandatory=$false)][string]$ConsolidatedSearchServiceEndPoint
)

$time = [System.DateTime]::Now
$time = $time.AddMinutes(-($time.Minute))
 
function CreateTask() {
  param([string]$taskName, [string]$argument, [int]$interval)
  
  $settings = New-ScheduledTaskSettingsSet -MultipleInstances P
  $Action = New-ScheduledTaskAction -Execute "$WorkingDir\galops.exe" -WorkingDirectory $WorkingDir -Argument  $argument
  $trigger = New-ScheduledTaskTrigger -Once -At  $time  -RepetitionDuration  ([Timespan]::MaxValue) -RepetitionInterval (New-TimeSpan -Minutes $interval)
  Unregister-ScheduledTask -TaskName $taskName -TaskPath "\NuGetDashboard\$EnvName\" -ErrorAction SilentlyContinue -Confirm:$false
  Register-ScheduledTask -TaskName $taskName -TaskPath "\NuGetDashboard\$EnvName\" -User $env:USERDOMAIN\$env:USERNAME -Password $CurrentUserPassword -Action $Action -Trigger $trigger -Settings $settings
}

# Database related tasks
CreateTask "CreateTrendingOverviewReport" "cshr -db `"$FrontEndLegacyDBConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60

# Enable or remove based on the outcome of:
# https://github.com/NuGet/Engineering/issues/291
# CreateTask "CreateDataBaseOverviewReport" "cdrt -db `"$FrontEndLegacyDBConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 30
# CreateTask "CreateDataBase24HourDetailedReport" "cddrt -db `"$FrontEndLegacyDBConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName -n 24" 30

# ElmahError related tasks
CreateTask "CreateElmaherrorOverviewReport" "ceeort -ea `"$FrontEndStorageConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60
CreateTask "CreateElmaherror1HourDetailedReport" "ceedrt -ea `"$FrontEndStorageConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName -n 1" 30
CreateTask "CreateElmaherror6HourDetailedReport" "ceedrt -ea `"$FrontEndStorageConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName -n 6" 30
CreateTask "CreateElmaherror24HourDetailedReport" "ceedrt -ea `"$FrontEndStorageConnectionString`" -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName -n 24" 30

# Pingdom tasks
CreateTask "CreatePingdomHourlyReport" "cpdwr -user `"$PingdomUserName`" -password `"$PingdomPassword`" -appkey `"$PingdomAppKey`" -frequency Hourly -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60
CreateTask "CreatePingdomServiceDisruptionReport" "psdrt -user `"$PingdomUserName`" -password `"$PingdomPassword`" -appkey `"$PingdomAppKey`" -n 1 -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60

# SearchService tasks
CreateTask "CreateConsolidatedSearchIndexingStatusReportTask" "ccsisrt -db `"$FrontEndLegacyDBConnectionString`" -se $ConsolidatedSearchServiceEndPoint -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName -alsev1 120 -alsev2 30 -disn -disic" 10

# Misc
CreateTask "CreateRequestsCountOverviewReport" "crphrt -iis `"$FrontEndStorageConnectionString`" -retry 3 -servicename $FrontEndCloudServiceName  -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60 
CreateTask "CreateTrafficManagerStatusOverviewReport" "ctmort -id $SubscriptionId -name $TrafficManagerProfileName -cername $ProdManagementCertName -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 5
CreateTask "CreateV2GalleryInstanceCountReport" "ccsdrt -id $SubscriptionId -name $FrontEndCloudServiceName -cername $ProdManagementCertName -st `"$DashboardStorageConnectionString`" -ct $DashboardStorageContainerName" 60

# Clean-up
CreateTask "IISLogCleanUpTask" "clean" 1440
