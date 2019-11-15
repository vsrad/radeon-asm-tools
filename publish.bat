set config=%1

cd %~dp0
:: Publish DebugServer
cd VSRAD.DebugServer
dotnet publish -r win-x64 -c %config% --self-contained false
dotnet publish -r linux-x64 -c %config% --self-contained false
cd ..
:: DebugServer
mkdir %config%
xcopy /E /Y "VSRAD.DebugServer\bin\%config%\netcoreapp2.2\win-x64\publish" "%config%\DebugServerW64\"
xcopy /E /Y "VSRAD.DebugServer\bin\%config%\netcoreapp2.2\linux-x64\publish" "%config%\DebugServerLinux64\"
:: RadeonAsmDebugger.vsix
copy /Y "VSRAD.Package\bin\%config%\RadeonAsmDebugger.vsix" "%config%\"
:: RadeonAsmSyntax.vsix
copy /Y "VSRAD.Syntax\bin\%config%\RadeonAsmSyntax.vsix" "%config%\"
:: Changelog and Readme
copy /Y "CHANGELOG.md" "%config%\"
copy /Y "README.md" "%config%\"
:: VSGRAD.BuildTools.dll
copy /Y "VSRAD.BuildTools\bin\%config%\VSRAD.BuildTools.dll" "%config%\"
:: RADProject
xcopy /E /Y "%LOCALAPPDATA%\CustomProjectSystems\RADProject" "%config%\RADProject\"
:: install.bat
copy "install.bat" "%config%\"
