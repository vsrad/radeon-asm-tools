if exist "%LOCALAPPDATA%\CustomProjectSystems\ArthurProject" (
    rd "%LOCALAPPDATA%\CustomProjectSystems\ArthurProject" /s /q
)

if exist "%LOCALAPPDATA%\CustomProjectSystems\GCNProject" (
    rd "%LOCALAPPDATA%\CustomProjectSystems\GCNProject" /s /q
)

if not exist "%LOCALAPPDATA%\CustomProjectSystems\RADProject" (
    mkdir "%LOCALAPPDATA%\CustomProjectSystems\RADProject"
) else (
    rd "%LOCALAPPDATA%\CustomProjectSystems\RADProject" /s /q
    mkdir "%LOCALAPPDATA%\CustomProjectSystems\RADProject"
)
xcopy /E RADProject "%LOCALAPPDATA%\CustomProjectSystems\RADProject" /Y
if not exist "%LOCALAPPDATA%\CustomProjectSystems\RADProject\Tools" mkdir "%LOCALAPPDATA%\CustomProjectSystems\RADProject\Tools"
xcopy /E DebugServerW64 "%LOCALAPPDATA%\CustomProjectSystems\RADProject\Tools" /Y
if not exist "%LOCALAPPDATA%\Microsoft\MSBuild\VSRAD" mkdir "%LOCALAPPDATA%\Microsoft\MSBuild\VSRAD" /Y
xcopy VSRAD.BuildTools.dll "%LOCALAPPDATA%\Microsoft\MSBuild\VSRAD" /Y