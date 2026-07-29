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

:: 将手写 JSON 配置文件复制到 StreamingAssets，与 Luban 输出走同一条 YooAsset 加载管线
copy /Y "%CONF_ROOT%\game-config.json" "%WORKSPACE%\%PROJECT_NAME%\Assets\StreamingAssets\GameData\game-config.json"

pause
