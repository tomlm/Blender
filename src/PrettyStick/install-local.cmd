dotnet tool uninstall -g PrettyStick.TUI
dotnet pack -c Release 
dotnet tool install -g PrettyStick.TUI --source nupkg