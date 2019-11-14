if exist "%LOCALAPPDATA%\CustomProjectSystems\RADProject" rd "%LOCALAPPDATA%\CustomProjectSystems\RADProject" /s /q
mkdir "%LOCALAPPDATA%\CustomProjectSystems\RADProject"
xcopy /E RADProject "%LOCALAPPDATA%\CustomProjectSystems\RADProject" /Y
if not exist "%LOCALAPPDATA%\CustomProjectSystems\RADProject\Tools" mkdir "%LOCALAPPDATA%\CustomProjectSystems\RADProject\Tools"
xcopy /E DebugServerW64 "%LOCALAPPDATA%\CustomProjectSystems\RADProject\Tools" /Y
if not exist "%LOCALAPPDATA%\Microsoft\MSBuild\VSRAD" mkdir "%LOCALAPPDATA%\Microsoft\MSBuild\VSRAD" /Y
xcopy VSRAD.BuildTools.dll "%LOCALAPPDATA%\Microsoft\MSBuild\VSRAD" /Y