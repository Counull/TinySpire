set WORKSPACE=..
set PROJECT_NAME=TinySpire
set LUBAN_DLL=%WORKSPACE%\Tools\Luban\Luban.dll
set CONF_ROOT=.

dotnet %LUBAN_DLL% ^
    -t all ^
    -d json2 ^
    -c cs-newtonsoft-json ^
    --conf %CONF_ROOT%\luban.conf ^
    -x outputCodeDir=%WORKSPACE%\%PROJECT_NAME%\Assets\Scripts\Core\Generated\Config ^
    -x outputDataDir=%WORKSPACE%\%PROJECT_NAME%\Assets\StreamingAssets\GameData

pause
