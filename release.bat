cd %~dp0
:: Publish DebugServer
cd VSRAD.DebugServer
dotnet publish -r win-x64 -c release --self-contained false
dotnet publish -r linux-x64 -c release --self-contained false
cd ..
:: DebugServer
mkdir Release
xcopy /E /Y "VSRAD.DebugServer\bin\release\netcoreapp2.2\win-x64\publish" "Release\DebugServerW64\"
xcopy /E /Y "VSRAD.DebugServer\bin\release\netcoreapp2.2\linux-x64\publish" "Release\DebugServerLinux64\"
:: RadeonAsmDebugger.vsix
copy /Y "VSRAD.Package\bin\Release\RadeonAsmDebugger.vsix" "Release\"
:: RadeonAsmSyntax.vsix
copy /Y "VSRAD.Syntax\bin\Release\RadeonAsmSyntax.vsix" "Release\"
:: Changelog and Readme
copy /Y "CHANGELOG.md" "Release\"
copy "/Y README.md" "Release\"
:: VSGRAD.BuildTools.dll
copy /Y "VSRAD.BuildTools\bin\Release\VSRAD.BuildTools.dll" "Release\"
:: RADProject
xcopy /E /Y "%LOCALAPPDATA%\CustomProjectSystems\RADProject" "Release\RADProject\"
:: install.bat
copy "install.bat" "Release\"