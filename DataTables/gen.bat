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
    -x outputDataDir=%WORKSPACE%\%PROJECT_NAME%\Assets\GameData

:: 将手写 JSON 配置文件复制到 GameData，与 ConfigService 的 YooAsset 地址保持一致
copy /Y "%CONF_ROOT%\game-config.json" "%WORKSPACE%\%PROJECT_NAME%\Assets\GameData\game-config.json"

pause
