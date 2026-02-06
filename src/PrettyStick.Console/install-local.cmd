dotnet tool uninstall -g PrettyStick.Console
dotnet tool uninstall -g PrettyStick
dotnet pack -c Release 
dotnet tool install -g PrettyStick --source nupkg