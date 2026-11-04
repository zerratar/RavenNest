# Stops a console program gracefully: a Ctrl+C sent to its own console, as pressing Ctrl+C in its window would.
# RavenNest saves everything when it stops this way; killing it (what LanLink's stop step and AppStarter's restarts
# do) skips that last save.
#
# Used by the LanLink deploy recipes on SERVER (C:/Users/woats/AppData/Local/LanLink/Scripts/stop-gracefully.ps1):
#   powershell -NoProfile -ExecutionPolicy Bypass -File stop-gracefully.ps1 -Name RavenNest -Wait 180
#
# AppStarter starts every program with its own console window, so the Ctrl+C reaches only that program. Exits 0 once
# every process with the name has exited (or none was running), 1 if one is still running after -Wait seconds, so the
# recipe's next step (LanLink's stop, a kill) can finish the job.
param(
    [Parameter(Mandatory = $true)][string]$Name,
    [int]$Wait = 120
)

$sig = @'
using System;
using System.Runtime.InteropServices;
public static class CtrlC
{
    [DllImport("kernel32.dll")] public static extern bool FreeConsole();
    [DllImport("kernel32.dll")] public static extern bool AttachConsole(uint pid);
    [DllImport("kernel32.dll")] public static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);
    [DllImport("kernel32.dll")] public static extern bool GenerateConsoleCtrlEvent(uint ctrlEvent, uint group);
}
'@
Add-Type -TypeDefinition $sig

$targets = @(Get-Process -Name $Name -ErrorAction SilentlyContinue)
if ($targets.Count -eq 0) { "$Name is not running"; exit 0 }

foreach ($p in $targets) {
    [CtrlC]::FreeConsole() | Out-Null
    if (-not [CtrlC]::AttachConsole([uint32]$p.Id)) { "could not reach the console of $Name ($($p.Id))"; continue }
    # this script shares the console for a moment: it must not stop itself
    [CtrlC]::SetConsoleCtrlHandler([IntPtr]::Zero, $true) | Out-Null
    [CtrlC]::GenerateConsoleCtrlEvent(0, 0) | Out-Null
    Start-Sleep -Milliseconds 500
    [CtrlC]::FreeConsole() | Out-Null
    "sent Ctrl+C to $Name ($($p.Id))"
}

$deadline = (Get-Date).AddSeconds($Wait)
foreach ($p in $targets) {
    $left = [int][Math]::Max(0, ($deadline - (Get-Date)).TotalMilliseconds)
    if ($p.WaitForExit($left)) { "$Name ($($p.Id)) stopped" }
    else { "$Name ($($p.Id)) is still running after $Wait s"; exit 1 }
}
exit 0
