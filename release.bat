:: DebugServer
mkdir Release
xcopy /E "VSRAD.DebugServer\bin\release\netcoreapp2.2\win-x64\publish" "Release\DebugServerW64\"
xcopy /E "VSRAD.DebugServer\bin\release\netcoreapp2.2\linux-x64\publish" "Release\DebugServerLinux64\"
:: RadeonAsmDebugger.vsix
copy "VSRAD.Package\bin\Release\RadeonAsmDebugger.vsix" "Release\"
:: Changelog and Readme
copy "CHANGELOG.md" "Release\"
copy "README.md" "Release\"
:: VSGRAD.BuildTools.dll
copy "VSRAD.BuildTools\bin\Release\VSRAD.BuildTools.dll" "Release\"
:: RADProject
xcopy /E "%LOCALAPPDATA%\CustomProjectSystems\RADProject" "Release\RADProject\"
:: install.bat
copy "install.bat" "Release\"