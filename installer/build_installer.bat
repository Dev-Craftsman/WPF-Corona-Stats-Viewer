SET PATH=%PATH%;C:\Program Files (x86)\Inno Setup 6
SET PROJECT_DIR=..\CoronaDailyStats\CoronaDailyStats

dotnet publish %PROJECT_DIR%\CoronaDailyStats.csproj /p:Configuration=Release /p:PublishProfile=%PROJECT_DIR%\Properties\PublishProfiles\FolderProfile.pubxml || GOTO ERROR
iscc /Qp /O"." /DPublishDir=%PROJECT_DIR%\bin\publish corona.iss || GOTO ERROR

exit /B 0

:ERROR
pause
echo "Failed to build installer!"
exit /B 1
