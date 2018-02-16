@echo off
title Setting Shoko Server Permissions

if exist "C:\ProgramData\ShokoServer" (
  echo Shoko Server folder found, no need to set permissions.
)
  
if not exist "C:\ProgramData\ShokoServer" (
  netsh advfirewall firewall add rule name="Shoko Server Port" dir=in action=allow protocol=TCP localport=8111
  netsh http add urlacl url=http://+:8111/JMMServerPlex sddl=D:(A;;GA;;;S-1-1-0)
  netsh http add urlacl url=http://+:8111/JMMServerStreaming sddl=D:(A;;GA;;;S-1-1-0)
  netsh http add urlacl url=http://+:8111/JMMServerImage sddl=D:(A;;GA;;;S-1-1-0)
  netsh http add urlacl url=http://+:8111/ sddl=D:(A;;GA;;;S-1-1-0)
  mkdir C:\ProgramData\ShokoServer	
  icacls C:\ProgramData\ShokoServer /inheritance:r
  icacls C:\ProgramData\ShokoServer /grant *S-1-1-0:(OI)(CI)F /T /inheritance:e
)

echo.
echo.
echo Shoko Server permissions correctly applied.  